#!/usr/bin/env node

import { fileURLToPath } from 'url';
import path from 'path';
import fs from 'fs';
import dotenv from 'dotenv';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

dotenv.config({ path: path.join(__dirname, '.env'), override: true });
dotenv.config({ path: path.join(__dirname, '..', '..', '.env') });

import { launch, close, esperarConDelay } from './modulos/browser.js';
import { login } from './modulos/login.js';
import { buscarLicitaciones } from './modulos/buscar.js';
import { extraerDatosLicitacion, cerrarFicha } from './modulos/licitacion.js';
import { descargarActaEvaluacion } from './modulos/adjuntos.js';
import { crearCarpetaLote, crearCarpetaLicitacion, guardarDatosLicitacion, guardarResumen, guardarReporteTexto } from './modulos/storage.js';
import { initDB, closeDB, upsertLicitacion, registrarAdjunto, obtenerUltimaSync, iniciarSyncLog, finalizarSyncLog } from './modulos/db.js';
import { pipelineAnalisisCompleto } from './modulos/api-client.js';
import { isDaemonMode, isIncrementalMode, checkExistingProcess, setupSignalHandlers, startDaemon, removePidFile } from './modulos/scheduler.js';

const RUT = process.env.MP_RUT;
const PASSWORD = process.env.MP_PASSWORD;
const HEADLESS = process.env.MP_HEADLESS === 'true';
const DELAY_MS = parseInt(process.env.MP_DELAY_MS || '2000', 10);
const MAX_REINTENTOS = parseInt(process.env.MP_MAX_REINTENTOS || '3', 10);
const ANALISIS_IA = process.env.MP_ANALISIS_IA === 'true';
const CARPETA_BASE = process.env.MP_CARPETA_SALIDA || path.join(__dirname, 'descargas');

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
    const browserInstance = await launch(HEADLESS);
    browser = browserInstance.browser;
    context = browserInstance.context;
    page = browserInstance.page;

    console.log('\n[CICLO] Paso 2/5: Login...');
    await login(page, context);
    console.log('[CICLO] Login exitoso');
    await esperarConDelay(DELAY_MS);

    console.log('\n[CICLO] Paso 3/5: Buscando licitaciones...');
    const licitaciones = await buscarLicitaciones(page, context);

    if (licitaciones.length === 0) {
      console.log('\n[CICLO] No se encontraron licitaciones.');
      await cerrarYGenerar(browser, context, page, resultados, CARPETA_BASE, syncId, horaInicio);
      return;
    }

    console.log(`[CICLO] ${licitaciones.length} licitaciones encontradas`);

    const carpetaLote = crearCarpetaLote(CARPETA_BASE);
    console.log(`\n[CICLO] Paso 4/5: Procesando ${licitaciones.length} licitaciones...`);

    for (let i = 0; i < licitaciones.length; i++) {
      const lic = licitaciones[i];
      console.log(`\n${'─'.repeat(50)}`);
      console.log(`[CICLO] ${i + 1}/${licitaciones.length}: ${(lic.codigo || lic.nombre || 'sin código').substring(0, 50)}`);
      console.log(`${'─'.repeat(50)}`);

      let fichaPage = null;
      let isPopup = false;

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
          console.log(`\n[CICLO] Buscando Acta de Evaluacion...`);
          const resultAdjuntos = await descargarActaEvaluacion(
            fichaPage, context, datos, carpetaLicitacion
          );

          datos.actaEvaluacion = resultAdjuntos.actaEvaluacion;
          datos.actaDescargada = resultAdjuntos.actaDescargada;
          datos.errorActa = resultAdjuntos.error;
          datos.todosAdjuntos = resultAdjuntos.todosAdjuntos;

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

            if (licitacionDbId && ANALISIS_IA) {
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
          }
        } else {
          console.log(`[CICLO] No se pudo abrir la ficha, saltando descarga`);
          datos.errorActa = 'No se pudo abrir la ficha';
        }

        guardarDatosLicitacion(carpetaLicitacion, datos);
        resultados.push(datos);

      } catch (e) {
        console.log(`[CICLO] Error procesando licitacion ${i + 1}: ${e.message}`);
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

      if (i < licitaciones.length - 1) {
        console.log(`[CICLO] Esperando ${DELAY_MS}ms...`);
        await esperarConDelay(DELAY_MS);
      }
    }

    console.log('\n[CICLO] Paso 5/5: Generando reporte...');
    await cerrarYGenerar(browser, context, page, resultados, carpetaLote, syncId, horaInicio);

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
      estado: errores.length === resultados.length ? 'error' : 'completado',
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

  startDaemon(executeCycle, () => intervalMs);
} else {
  executeCycle().catch(async (e) => {
    console.error('[AGENTE] Error fatal:', e);
    await closeDB().catch(() => {});
    process.exit(1);
  });
}