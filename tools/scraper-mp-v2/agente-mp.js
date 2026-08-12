#!/usr/bin/env node

import { fileURLToPath } from 'url';
import path from 'path';
import fs from 'fs';
import dotenv from 'dotenv';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const hasSystemDb = process.env.DB_HOST && process.env.DB_HOST !== 'localhost';
dotenv.config({ path: path.join(__dirname, '.env'), override: !hasSystemDb });
dotenv.config({ path: path.join(__dirname, '..', '..', '.env'), override: !hasSystemDb });

import { launch, close, esperarConDelay } from './modulos/browser.js';
import { login } from './modulos/login.js';
import { buscarLicitaciones } from './modulos/buscar.js';
import { extraerDatosLicitacion, cerrarFicha } from './modulos/licitacion.js';
import { descargarActaEvaluacion } from './modulos/adjuntos.js';
import { extraerCuadroOfertas } from './modulos/cuadroOfertas.js';
import { crearCarpetaLote, crearCarpetaLicitacion, guardarDatosLicitacion, guardarResumen, guardarReporteTexto } from './modulos/storage.js';
import { initDB, closeDB, upsertLicitacion, registrarAdjunto, obtenerUltimaSync, iniciarSyncLog, finalizarSyncLog, tieneAnalisisCompletado, licitacionYaExiste, guardarEstadoSesion, obtenerEstadoSesion, limpiarEstadoSesion } from './modulos/db.js';
import { pipelineAnalisisCompleto, guardarOfertasCompetidor } from './modulos/api-client.js';
import { isDaemonMode, isIncrementalMode, checkExistingProcess, setupSignalHandlers, startDaemon, removePidFile } from './modulos/scheduler.js';

const RUT = process.env.MP_RUT;
const PASSWORD = process.env.MP_PASSWORD;
const HEADLESS = process.env.MP_HEADLESS === 'true';
const DELAY_MS = parseInt(process.env.MP_DELAY_MS || '2000', 10);
const MAX_REINTENTOS = parseInt(process.env.MP_MAX_REINTENTOS || '3', 10);
const ANALISIS_IA = process.env.MP_ANALISIS_IA === 'true';
const CARPETA_BASE = process.env.MP_CARPETA_SALIDA || path.join(__dirname, 'descargas');

// Flags de fase de ejecucion (v2)
const ONLY_COMPETIDORES = process.argv.includes('--only-competidores');
const ONLY_ADJUNTOS = process.argv.includes('--only-adjuntos');
// Backfill puntual (manual, no usado por el ciclo automatico): las licitaciones con analisis
// ya completado se saltan sin abrir su ficha (linea ~130) porque para el pipeline de Acta/IA
// no hay nada nuevo que hacer con ellas -- pero eso tambien bloqueaba para siempre recuperar
// el Cuadro de Ofertas de licitaciones viejas, ya analizadas, que nunca lo tuvieron (bug de
// cuadroOfertas.js corregido en 660c8a0). Este flag las vuelve a incluir SOLO para extraer
// el cuadro de ofertas, sin tocar Acta/IA (igual que --only-competidores).
const BACKFILL_COMPETIDORES = process.argv.includes('--backfill-competidores');


async function executeCycle() {
  const horaInicio = Date.now();
  console.log(`\n[CICLO] Inicio: ${new Date().toISOString()}`);

  await initDB();

  const incremental = isIncrementalMode();
  if (incremental) {
    const ultimaSync = await obtenerUltimaSync();
    const fechaDesde = new Date(new Date(ultimaSync).getTime() + 60000);
    const fechaStr = `${String(fechaDesde.getDate()).padStart(2, '0')}-${String(fechaDesde.getMonth() + 1).padStart(2, '0')}-${fechaDesde.getFullYear()}`;
    process.env.MP_FECHA_DESDE = fechaStr;
    console.log(`[CICLO] Modo incremental: desde ${fechaStr} (ultima sync: ${ultimaSync})`);
  }

  const { syncId, error: syncError } = await iniciarSyncLog(
    process.env.MP_FECHA_DESDE || null,
    new Date().toISOString()
  );
  if (!syncId) {
    console.log(`[CICLO] Error iniciando sync log: ${syncError}`);
  }

  let browser, context, page;
  const resultados = [];

  try {
    console.log('\n[CICLO] Paso 1/5: Iniciando navegador...');
    // Carga de cookies desde BD en la V2
    const sessionState = await obtenerEstadoSesion();
    const browserInstance = await launch(HEADLESS, sessionState);
    browser = browserInstance.browser;
    context = browserInstance.context;
    page = browserInstance.page;

    console.log('\n[CICLO] Paso 2/5: Login...');
    try {
      await login(page, context);
    } catch (loginErr) {
      // Marcadores parseables para el wrapper .NET (mismo patron que
      // ESTRUCTURA_CAMBIO_DETECTADA en ScraperBackgroundService.NotificarResultadoAsync):
      // distinguen "credenciales/flujo de login roto" de "bloqueo anti-robot" en stdout.
      if (loginErr.isRobotBlock) {
        console.log('[CICLO] BLOQUEO_ROBOT: true -- bloqueo anti-robot durante el login, cortando ciclo.');
      } else {
        console.log('[CICLO] LOGIN_FALLIDO: true -- el login no llego a una sesion autenticada verificada.');
      }
      // La sesion persistida pudo quedar envenenada (cookies de una sesion bloqueada o
      // expirada): invalidarla para que el proximo ciclo haga login limpio desde cero.
      await limpiarEstadoSesion().catch(() => {});
      throw loginErr;
    }

    console.log('[CICLO] Login exitoso (sesion verificada)');
    // Guardar el estado de sesion SOLO despues de la asercion positiva de login.js
    // (antes se guardaba tras un "true" heuristico y podia persistir una sesion invalida).
    const nuevoEstado = await context.storageState();
    await guardarEstadoSesion(nuevoEstado);
    await esperarConDelay(DELAY_MS);

    console.log('\n[CICLO] Paso 3/5: Buscando licitaciones...');
    const licitaciones = await buscarLicitaciones(page, context);

    if (licitaciones.length === 0) {
      console.log('\n[CICLO] No se encontraron licitaciones.');
      // Antes se pasaba CARPETA_BASE directo a cerrarYGenerar (sin crear la carpeta del lote),
      // y guardarResumen fallaba con ENOENT al intentar escribir resumen.json en una carpeta
      // que nunca se creó -- el ciclo de 0 resultados quedaba con un stack trace en los logs
      // aunque igual terminara "exitosamente". Se alinea con el camino normal (línea ~107),
      // que sí llama crearCarpetaLote() antes de generar el resumen.
      const carpetaLoteVacio = crearCarpetaLote(CARPETA_BASE);
      await cerrarYGenerar(browser, context, page, resultados, carpetaLoteVacio, syncId, horaInicio);
      return;
    }

    console.log(`[CICLO] ${licitaciones.length} licitaciones encontradas`);

    const offset = parseInt(process.env.MP_OFFSET || '0', 10);
    const limite = parseInt(process.env.MP_LIMIT || '0', 10);
    const licitacionesAcotadas = limite > 0
      ? licitaciones.slice(offset, offset + limite)
      : licitaciones.slice(offset);
    if (offset > 0 || limite > 0) {
      console.log(`[CICLO] MP_OFFSET=${offset} MP_LIMIT=${limite || 'sin limite'}: acotando a ${licitacionesAcotadas.length} de ${licitaciones.length} licitaciones (indices ${offset}-${offset + licitacionesAcotadas.length - 1}, modo de prueba)`);
    }

    // Pre-filtro: si ya existe analisis completo para el codigo, no hace falta ni abrir la
    // ficha ni gastar el cupo limitado de "Ver Adjuntos" en ella -- se detecto el 2026-07-07
    // que reprocesar licitaciones ya analizadas gastaba cupo sin necesidad.
    console.log('\n[CICLO] Verificando cuales ya tienen analisis completo (para no gastar cupo en ellas)...');
    const licitacionesAProcesar = [];
    let saltadasPreviamente = 0;
    for (const lic of licitacionesAcotadas) {
      const idExistente = lic.codigo ? await licitacionYaExiste(lic.codigo) : null;
      const yaAnalizada = idExistente && await tieneAnalisisCompletado(idExistente);
      if (yaAnalizada && !BACKFILL_COMPETIDORES) {
        saltadasPreviamente++;
      } else {
        licitacionesAProcesar.push(lic);
      }
    }
    console.log(`[CICLO] ${saltadasPreviamente} ya analizadas (omitidas sin abrir su ficha), ${licitacionesAProcesar.length} pendientes de procesar`);

    const carpetaLote = crearCarpetaLote(CARPETA_BASE);
    console.log(`\n[CICLO] Paso 4/5: Procesando ${licitacionesAProcesar.length} licitaciones pendientes...`);

    const maxFallosConsecutivos = parseInt(process.env.MP_MAX_FALLOS_CONSECUTIVOS || '2', 10);
    let fallosConsecutivos = 0;
    let detenidoPorFallos = false;
    let estructuraCambioDetectada = false;

    for (let i = 0; i < licitacionesAProcesar.length; i++) {
      const lic = licitacionesAProcesar[i];
      console.log(`\n${'─'.repeat(50)}`);
      console.log(`[CICLO] ${i + 1}/${licitacionesAProcesar.length}: ${(lic.codigo || lic.nombre || 'sin código').substring(0, 50)}`);
      console.log(`${'─'.repeat(50)}`);

      let fichaPage = null;
      let isPopup = false;
      let estaFallo = false;

      try {
        const result = await extraerDatosLicitacion(page, context, lic);
        const datos = result.datos;
        fichaPage = result.fichaPage;
        isPopup = result.isPopup;

        const carpetaLicitacion = crearCarpetaLicitacion(
          carpetaLote,
          datos.codigo || datos.nombre || `licitacion-${i + 1}`
        );
        datos.carpetaLicitacion = carpetaLicitacion;

        guardarDatosLicitacion(carpetaLicitacion, datos);

        let licitacionDbId = null;
        if (datos.estado !== 'error') {
          const upsertResult = await upsertLicitacion(datos);
          licitacionDbId = upsertResult.licitacionId;
          datos.licitacionDbId = licitacionDbId;
        }

        if (fichaPage) {
          if (ONLY_ADJUNTOS) {
            console.log('[CICLO] Modo --only-adjuntos: omitiendo extraccion de cuadro de ofertas.');
          } else {
            // 024-inteligencia-competencia-alertas / US1: se intenta siempre, en cualquier
            // licitacion visitada -- cuadroOfertas.js ya maneja con gracia el caso de que el
            // icono "Cuadro de ofertas" no exista para este tipo/estado de licitacion (Compra
            // Agil, Trato Directo, o una licitacion que aun no cerro), sin frenar el resto del
            // ciclo (research.md R3).
            if (licitacionDbId) {
              const resultCuadroOfertas = await extraerCuadroOfertas(fichaPage, context, datos, carpetaLicitacion);
              if (resultCuadroOfertas.ofertas?.length > 0) {
                await guardarOfertasCompetidor(licitacionDbId, resultCuadroOfertas.ofertas);
              }
            }
          }

          if (ONLY_COMPETIDORES || BACKFILL_COMPETIDORES) {
            console.log('[CICLO] Modo --only-competidores/--backfill-competidores: omitiendo descarga de Acta de Evaluacion y pipeline IA.');
            datos.actaDescargada = false;
          } else {
            console.log(`\n[CICLO] Buscando Acta de Evaluacion...`);
            const resultAdjuntos = await descargarActaEvaluacion(
              fichaPage, context, datos, carpetaLicitacion
            );

            datos.actaEvaluacion = resultAdjuntos.actaEvaluacion;
            datos.actaDescargada = resultAdjuntos.actaDescargada;
            datos.errorActa = resultAdjuntos.error;
            datos.todosAdjuntos = resultAdjuntos.todosAdjuntos;
            datos.isRobotBlock = resultAdjuntos.isRobotBlock;

            if (resultAdjuntos.isRobotBlock) {
              console.log('[CICLO] DETECTADO BLOQUEO DE ROBOT EN ADJUNTOS: finalizando el ciclo para evitar quemar la sesion/IP.');
              estaFallo = true;
              detenidoPorFallos = true;
              break;
            }

            if (resultAdjuntos.estructuraCambio) {
              // Marcador parseable: si este proceso corre como subproceso de
              // ScraperBackgroundService.cs (.NET), NotificarResultadoAsync lo detecta en stdout
              // y dispara una alerta a Telegram distinta de "cupo agotado" (QA BUG-003).
              console.log('[CICLO] ESTRUCTURA_CAMBIO_DETECTADA: true -- cortando ciclo de inmediato, no es cupo agotado');
              estructuraCambioDetectada = true;
            }

            if (licitacionDbId && resultAdjuntos.todosAdjuntos) {
              for (const adj of resultAdjuntos.todosAdjuntos) {
                await registrarAdjunto(licitacionDbId, {
                  nombre: adj.nombre,
                  grid: adj.grid,
                  esActa: adj.nombre?.toLowerCase().includes('acta') && adj.nombre?.toLowerCase().includes('evaluaci'),
                  rutaStorage: adj.rutaStorage || '',
                  rutaLocal: adj.rutaLocal || '',
                  tamanioBytes: adj.tamanioBytes,
                  mimeType: adj.mimeType,
                });
              }
            }

            if (resultAdjuntos.actaDescargada) {
              console.log(`[CICLO] Acta descargada: ${resultAdjuntos.actaEvaluacion}`);
              fallosConsecutivos = 0;

              const yaAnalizada = licitacionDbId && await tieneAnalisisCompletado(licitacionDbId);
              if (yaAnalizada) {
                console.log(`[CICLO] Ya existe un analisis completado para esta licitacion, se omite Gemini (evita gasto duplicado)`);
              }

              if (licitacionDbId && ANALISIS_IA && !yaAnalizada) {
                console.log(`[CICLO] Iniciando pipeline IA para acta...`);
                const iaResult = await pipelineAnalisisCompleto(
                  licitacionDbId,
                  datos.nombre || datos.codigo || 'Sin nombre',
                  resultAdjuntos.actaEvaluacion,
                  path.basename(resultAdjuntos.actaEvaluacion || 'acta-evaluacion.pdf')
                );

                if (iaResult.workspaceId) {
                  await registrarAdjunto(licitacionDbId, {
                    nombre: 'acta-evaluacion',
                    esActa: true,
                    rutaStorage: resultAdjuntos.actaEvaluacion || '',
                    rutaLocal: resultAdjuntos.actaEvaluacion || '',
                    analisisEstado: 'procesando',
                    workspaceId: iaResult.workspaceId,
                  });
                  datos.analisisWorkspaceId = iaResult.workspaceId;
                  console.log(`[CICLO] Pipeline IA en progreso (workspace: ${iaResult.workspaceId})`);
                }
              }
            } else {
              console.log(`[CICLO] Sin Acta: ${resultAdjuntos.error || 'No encontrada'}`);
              // "Sin acta" solo cuenta como fallo de cupo si la grilla de adjuntos NO llego a
              // cargar (403/timeout). Si la grilla cargo bien y simplemente no hay Acta de
              // Evaluacion (desiertas, revocadas, publicadas aun sin acta), el cupo de
              // "Ver Adjuntos" esta claramente operativo -- contarlo como fallo cortaba la
              // sesion con esperas de 20 min sin motivo.
              const grillaCargo = Array.isArray(resultAdjuntos.todosAdjuntos) && resultAdjuntos.todosAdjuntos.length > 0;
              if (grillaCargo) {
                fallosConsecutivos = 0;
              } else {
                estaFallo = true;
              }
            }
          }
        } else {
          console.log(`[CICLO] No se pudo abrir la ficha, saltando descarga`);
          datos.errorActa = 'No se pudo abrir la ficha';
          estaFallo = true;
        }

        guardarDatosLicitacion(carpetaLicitacion, datos);
        resultados.push(datos);

      } catch (e) {
        console.log(`[CICLO] Error procesando licitacion ${i + 1}: ${e.message}`);
        estaFallo = true;
        resultados.push({
          ...lic,
          estado: 'error',
          error: e.message,
          actaDescargada: false,
        });
      } finally {
        if (fichaPage || !isPopup) {
          await cerrarFicha(page, fichaPage, isPopup).catch(e =>
            console.log(`[CICLO] Error cerrando ficha: ${e.message}`)
          );
        }
      }

      if (estaFallo) {
        fallosConsecutivos++;
      }

      if (estructuraCambioDetectada) {
        console.log('\n[CICLO] Cambio de estructura detectado -- cortando sesion de inmediato (sin esperar el umbral de fallos consecutivos).');
        break;
      }

      // Deteccion de cupo agotado: el 2026-07-07 se confirmo con dos corridas independientes
      // (sesion/navegador/login nuevos en ambas) que el 403 en "Ver Adjuntos" es un limite de
      // VOLUMEN acumulado (cuenta o IP) por ventana de tiempo -- no se resuelve con pausas
      // cortas dentro de la misma sesion, solo con tiempo real de espera. Insistir con el resto
      // de la lista una vez agotado el cupo solo genera fallos garantizados (y probablemente
      // sigue gastando el mismo cupo ya agotado). Se corta la sesion apenas se detectan
      // MP_MAX_FALLOS_CONSECUTIVOS fallos seguidos, en vez de agotar toda la lista pendiente.
      if (fallosConsecutivos >= maxFallosConsecutivos) {
        console.log(`\n[CICLO] ${fallosConsecutivos} fallos consecutivos detectados -- probable cupo agotado. Cortando sesion en vez de seguir insistiendo.`);
        detenidoPorFallos = true;
        break;
      }

      if (i < licitacionesAProcesar.length - 1) {
        // Enfriamiento mas largo cada N licitaciones (ademas del delay normal entre cada una).
        const enfriamientoCada = parseInt(process.env.MP_ENFRIAMIENTO_CADA || '8', 10);
        const enfriamientoMs = parseInt(process.env.MP_ENFRIAMIENTO_MS || '60000', 10);
        if (enfriamientoCada > 0 && (i + 1) % enfriamientoCada === 0) {
          console.log(`[CICLO] Enfriamiento largo: ${enfriamientoMs}ms tras ${i + 1} licitaciones...`);
          await esperarConDelay(enfriamientoMs);
        } else {
          console.log(`[CICLO] Esperando ${DELAY_MS}ms...`);
          await esperarConDelay(DELAY_MS);
        }
      }
    }

    console.log('\n[CICLO] Paso 5/5: Generando reporte...');
    await cerrarYGenerar(browser, context, page, resultados, carpetaLote, syncId, horaInicio);

    if (estructuraCambioDetectada) {
      // A diferencia de "cupo agotado", reintentar con una sesion nueva no arregla un cambio
      // real de estructura del sitio -- se corta el orquestador y se deja la alerta a cargo
      // del wrapper .NET (ver ESTRUCTURA_CAMBIO_DETECTADA arriba).
      return { detenidoPorFallos: false, estructuraCambio: true };
    }
    if (detenidoPorFallos) {
      // Senal para el orquestador de sesiones (ejecutarConReintentosDeSesion): hay que
      // esperar un enfriamiento largo real y reintentar con una sesion/login nuevos.
      return { detenidoPorFallos: true };
    }
    return { detenidoPorFallos: false };

  } catch (e) {
    console.error(`\n[CICLO] ERROR FATAL: ${e.message}`);
    console.error(e.stack);

    if (page) {
      try {
        const carpeta = crearCarpetaLote(CARPETA_BASE);
        const screenshotPath = path.join(carpeta, `error-fatal-${Date.now()}.png`);
        await page.screenshot({ path: screenshotPath, fullPage: true });
        console.log(`[CICLO] Screenshot de error: ${screenshotPath}`);
      } catch (se) {}
    }

    if (syncId) {
      await finalizarSyncLog(syncId, {
        registrosProcesados: resultados.length,
        errores: 1,
        erroresDetalle: [e.message],
        estado: 'error',
        duracionMs: Date.now() - horaInicio,
      }).catch(() => {});
    }

    if (browser) {
      await close(browser, context, page);
    }

    await closeDB();

    if (e.isRobotBlock) {
      // Un bloqueo anti-robot se comporta como el cupo agotado: solo lo arregla esperar
      // de verdad y volver con sesion/navegador nuevos -> dejar que el orquestador reintente.
      return { detenidoPorFallos: true };
    }
    if (e.isLoginFallido) {
      // Credenciales rechazadas o flujo de login roto: reintentar en loop solo arriesga
      // bloquear la cuenta en Keycloak. Se corta el orquestador (el marcador LOGIN_FALLIDO
      // ya quedo en stdout para la alerta del wrapper .NET).
      return { loginFallido: true };
    }
  }
}

async function cerrarYGenerar(browser, context, page, resultados, carpetaLote, syncId, horaInicio) {
  if (syncId) {
    const exitosas = resultados.filter(r => r.estado === 'completo' && r.actaDescargada);
    const sinActa = resultados.filter(r => r.estado === 'completo' && !r.actaDescargada);
    const errores = resultados.filter(r => r.estado === 'error');
    const analizados = resultados.filter(r => r.analisisWorkspaceId);

    await finalizarSyncLog(syncId, {
      registrosProcesados: resultados.length,
      nuevos: exitosas.length,
      errores: errores.length,
      erroresDetalle: errores.map(e => ({ codigo: e.codigo, error: e.error })),
      totalLicitaciones: resultados.length,
      totalConActa: exitosas.length,
      totalSinActa: sinActa.length,
      totalAnalizados: analizados.length,
      duracionMs: Date.now() - horaInicio,
      // 0 resultados legítimos (5/5 estados leídos, 0 licitaciones) NO es un fallo: con
      // resultados vacíos, "errores.length === resultados.length" es 0===0 → 'error' falso
      // (detectado en prod 2026-08-12 tras el fix 035, ciclo completado pero sync log marcado
      // como error). El estado 'error' queda solo cuando HAY resultados y TODOS fallaron.
      estado: resultados.length > 0 && errores.length === resultados.length ? 'error' : 'completado',
    });
  }

  const resumen = guardarResumen(carpetaLote, resultados);
  guardarReporteTexto(carpetaLote, resumen);

  console.log('\n' + '='.repeat(70));
  console.log('  REPORTE FINAL');
  console.log('='.repeat(70));
  console.log(`  Total licitaciones: ${resumen.estadisticas.totalLicitaciones}`);
  console.log(`  Con Acta: ${resumen.estadisticas.exitosas}`);
  console.log(`  Sin Acta: ${resumen.estadisticas.sinActa}`);
  console.log(`  Con error: ${resumen.estadisticas.conError}`);
  console.log(`  Carpeta: ${carpetaLote}`);
  console.log('='.repeat(70));

  await close(browser, context, page);
  await closeDB();
  console.log('[CICLO] Proceso completado.');
}

/**
 * Orquestador de sesiones: corre executeCycle() en un loop, y si una sesion se corta por
 * cupo agotado (deteccion de fallos consecutivos en "Ver Adjuntos"), espera un enfriamiento
 * largo real y arranca una sesion/navegador/login completamente nuevos, continuando con lo
 * que quede pendiente (el pre-filtro de "ya analizada" al inicio de cada ciclo se encarga de
 * no repetir lo ya hecho). Pensado para dejar el proceso 100% desatendido -- ver
 * specs/021-scraper-tivit-hardening/spec.md.
 */
async function ejecutarConReintentosDeSesion() {
  const maxCiclos = parseInt(process.env.MP_MAX_CICLOS_SESION || '10', 10);
  const esperaEntreSesionesMs = parseInt(process.env.MP_ESPERA_ENTRE_SESIONES_MS || String(20 * 60 * 1000), 10);

  for (let ciclo = 1; ciclo <= maxCiclos; ciclo++) {
    console.log(`\n${'='.repeat(70)}\n[SESION] Ciclo ${ciclo}/${maxCiclos}\n${'='.repeat(70)}`);
    const resultado = await executeCycle();

    if (resultado && resultado.estructuraCambio) {
      console.log('[SESION] Cambio de estructura del sitio detectado -- no se reintenta con una sesion nueva (no se va a arreglar solo). Deteniendo orquestador.');
      return;
    }

    if (resultado && resultado.loginFallido) {
      console.log('[SESION] Login fallido (credenciales o flujo roto) -- reintentar en loop arriesga bloquear la cuenta. Deteniendo orquestador.');
      return;
    }

    if (!resultado || !resultado.detenidoPorFallos) {
      console.log('[SESION] Ciclo completado sin cortarse por cupo agotado -- no hacen falta mas sesiones.');
      return;
    }

    if (ciclo < maxCiclos) {
      console.log(`[SESION] Cupo agotado. Esperando ${esperaEntreSesionesMs}ms antes de abrir una sesion nueva...`);
      await esperarConDelay(esperaEntreSesionesMs);
    } else {
      console.log(`[SESION] Se alcanzo el maximo de ${maxCiclos} ciclos de sesion sin terminar. Deteniendo por ahora.`);
    }
  }
}

if (isDaemonMode()) {
  if (checkExistingProcess()) {
    process.exit(0);
  }

  const intervalHours = parseInt(process.env.SCRAPER_INTERVAL_HOURS || '12', 10);
  const intervalMs = intervalHours * 60 * 60 * 1000;

  setupSignalHandlers(async () => {
    await closeDB();
    removePidFile();
  });

  startDaemon(ejecutarConReintentosDeSesion, () => intervalMs);
} else if (process.env.MP_LIMIT || process.env.MP_OFFSET) {
  // Modo de prueba acotado (MP_LIMIT/MP_OFFSET): un solo ciclo, sin loop de reintento de
  // sesiones -- para pruebas puntuales no queremos que el proceso quede esperando 20 minutos.
  executeCycle().catch(async (e) => {
    console.error('[AGENTE] Error fatal:', e);
    await closeDB().catch(() => {});
    process.exit(1);
  });
} else {
  // Modo por defecto (sin --daemon, sin MP_LIMIT/MP_OFFSET): corre con el orquestador de
  // sesiones -- si el cupo de "Ver Adjuntos" se agota, espera un enfriamiento largo real y
  // continua solo con una sesion/login nuevos, hasta cubrir todo lo pendiente. Este es el
  // modo pensado para dejar el proceso desatendido.
  ejecutarConReintentosDeSesion().catch(async (e) => {
    console.error('[AGENTE] Error fatal:', e);
    await closeDB().catch(() => {});
    process.exit(1);
  });
}