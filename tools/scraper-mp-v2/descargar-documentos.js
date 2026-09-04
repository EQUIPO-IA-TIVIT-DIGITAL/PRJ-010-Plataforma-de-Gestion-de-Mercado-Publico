#!/usr/bin/env node
// @DEPRECATED — ADR-015 (2026-08-25): Bloqueo verificado reCAPTCHA Enterprise + WAF Volterra en ViewAttachment.
// Conservado como referencia. No invocar en prod. Modo por defecto es carga manual (Extraccion:ModoDescarga=manual).
// Ver docs/adr/ADR-015-carga-manual-pliegos.md y docs/specs/038-carga-manual-pliegos.feature-spec.md
// descargar-documentos.js — Descarga TODOS los adjuntos de una licitación puntual,
// bajo demanda (036-flujo-comercial-ofertas, spec docs/api-first/licitaciones-documentos.md).
//
// Invocado por el backend .NET (AdjuntoDescargaService) con:
//   node descargar-documentos.js --codigo="729-134-LE26" --licitacionId=123
//
// Reutiliza la sesión persistida de Mercado Público (login.js + storageState en BD).
// Para cada archivo: descarga → SHA-256 → CALL usp_Adjuntos_UpsertConHash (versión + hash).
// Al final marca el estado de la extracción (completado | error).
//
// Salida con marcadores parseables para el wrapper .NET:
//   [DESCARGA] resultado=exito descargados=N errores=N
//   [DESCARGA] resultado=error motivo=...

import { fileURLToPath } from 'url';
import path from 'path';
import fs from 'fs';
import crypto from 'crypto';
import dotenv from 'dotenv';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
dotenv.config({ path: path.join(__dirname, '..', '..', '.env') });
dotenv.config({ path: path.join(__dirname, '.env'), override: true });

import { launch, close, esperarConDelay, screenshotOnError, clickHumano } from './modulos/browser.js';
import { initDB, closeDB, obtenerEstadoSesion, guardarEstadoSesion } from './modulos/db.js';
import { login } from './modulos/login.js';

// Con sesión de proveedor (portal interno), la URL canónica es DetailsAcquisition.aspx.
// La ficha pública fichaLicitacion.html queda solo como fallback sin sesión.
const FICHA_URL_INTERNA = 'https://www.mercadopublico.cl/Procurement/Modules/RFB/DetailsAcquisition.aspx?idlicitacion={codigo}';
const FICHA_URL_PUBLICA = 'https://www.mercadopublico.cl/fichaLicitacion.html?idlicitacion={codigo}';
const HEADLESS = process.env.MP_HEADLESS === 'true';
const MAX_REINTENTOS = parseInt(process.env.MP_MAX_REINTENTOS || '3', 10);
const ADJUNTOS_DIR = process.env.ADJUNTOS_DIR || path.join(__dirname, 'descargas');

function parseArgs() {
  const argv = process.argv.slice(2);
  const get = (key) => {
    const arg = argv.find(a => a.startsWith(`--${key}=`));
    return arg ? arg.slice(`--${key}=`.length).replace(/^"|"$/g, '') : null;
  };
  return { codigo: get('codigo'), licitacionId: get('licitacionId') };
}

function mimeDesdeNombre(nombre) {
  const ext = path.extname(nombre || '').toLowerCase();
  const map = {
    '.pdf': 'application/pdf',
    '.doc': 'application/msword',
    '.docx': 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    '.xls': 'application/vnd.ms-excel',
    '.xlsx': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    '.zip': 'application/zip',
    '.rar': 'application/vnd.rar',
    '.txt': 'text/plain',
  };
  return map[ext] || 'application/octet-stream';
}

function sanitizarNombre(nombre) {
  const base = (nombre || '').replace(/[<>:"/\\|?*\u0000-\u001F]/g, '_').trim();
  return base || `adjunto-${Date.now()}`;
}

async function sha256DeArchivo(ruta) {
  const data = await fs.promises.readFile(ruta);
  return crypto.createHash('sha256').update(data).digest('hex');
}

async function registrarLogExtraccion(licitacionId, estado, documentos, error, duracionMs) {
  const p = initDB();
  const client = await p.connect();
  try {
    await client.query(
      `SELECT * FROM usp_ExtraccionLog_Registrar($1, $2, $3, $4, $5, $6, $7, $8)`,
      [Number(licitacionId), 'navegador', estado, documentos, false, false, error || null, Math.round(duracionMs)]
    );
  } catch (e) {
    console.log(`[DESCARGA] No se pudo registrar log de extracción: ${e.message}`);
  } finally {
    client.release();
  }
}

async function marcarFinalizada(licitacionId, estado, error) {
  const p = initDB();
  const client = await p.connect();
  try {
    await client.query(
      `CALL usp_Adjuntos_MarcarDescargaFinalizada($1, $2, $3, NULL)`,
      [Number(licitacionId), estado, error || null]
    );
  } catch (e) {
    console.log(`[DESCARGA] No se pudo marcar estado final: ${e.message}`);
  } finally {
    client.release();
  }
}

async function upsertConHash(licitacionId, fila, rutaLocal, rutaStorage) {
  const p = initDB();
  const client = await p.connect();
  try {
    const sha = await sha256DeArchivo(rutaLocal);
    const stats = fs.statSync(rutaLocal);
    const tipo = fila.esActa ? 'acta_evaluacion' : 'anexo';

    const result = await client.query(
      `CALL usp_Adjuntos_UpsertConHash(
        $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, NULL, NULL, NULL, NULL
      )`,
      [
        Number(licitacionId),
        tipo,
        path.basename(rutaStorage),
        rutaStorage,
        fila.nombre || path.basename(rutaStorage),
        rutaLocal,
        stats.size,
        mimeDesdeNombre(rutaStorage),
        !!fila.esActa,
        sha,
        fila.fecha || null,
      ]
    );

    const p_version = result.rows[0]?.p_version || 0;
    const p_creado = result.rows[0]?.p_creado || false;
    const p_error = result.rows[0]?.p_error_msg || '';
    if (p_error && !p_error.startsWith('SYS')) {
      return { sha, version: p_version, creado: p_creado, error: null };
    }
    return { sha, version: p_version, creado: p_creado, error: p_error || null };
  } catch (e) {
    return { sha: null, version: 0, creado: false, error: e.message };
  } finally {
    client.release();
  }
}

async function abrirGrillaAdjuntos(fichaPage, context, codigo) {
  for (let intento = 1; intento <= MAX_REINTENTOS; intento++) {
    let adjuntosPage = null;
    try {
      const imgAdjuntos = fichaPage.locator('#imgAdjuntos');
      await imgAdjuntos.waitFor({ state: 'visible', timeout: 15000 }).catch(() => {});

      // 1. PRIMERO: click real con emulación humana en "Ver Adjuntos" y capturar el popup
      const popupPromise = context.waitForEvent('page', { timeout: 15000 }).catch(() => null);
      await clickHumano(fichaPage, imgAdjuntos, 400);
      adjuntosPage = await popupPromise;

      if (!adjuntosPage) {
        const pages = context.pages();
        if (pages.length > 1) {
          adjuntosPage = pages[pages.length - 1];
        }
      }

      if (!adjuntosPage || adjuntosPage === fichaPage) {
        // 2. FALLBACK: Disparar window.open desde el contexto de la ficha para preservar opener y referer
        const urlRelativa = await fichaPage.evaluate(() => {
          const el = document.getElementById('imgAdjuntos');
          if (!el) return null;
          const onclick = el.getAttribute('onclick') || '';
          const match = onclick.match(/open\(['"]([^'"]+)['"]/);
          return match && match[1] ? match[1] : null;
        });

        if (urlRelativa) {
          console.log(`[DESCARGA] Fallback: disparando window.open en contexto de ficha previa...`);
          const fallbackPopupPromise = context.waitForEvent('page', { timeout: 15000 }).catch(() => null);
          await fichaPage.evaluate((u) => { window.open(u, 'MercadoPublicoPopup'); }, urlRelativa);
          adjuntosPage = await fallbackPopupPromise;
        }
      }

      if (!adjuntosPage || adjuntosPage === fichaPage) {
        if (intento < MAX_REINTENTOS) { await esperarConDelay(2000); continue; }
        return { ok: false, error: 'Sin ventana de adjuntos', filas: [] };
      }

      await adjuntosPage.waitForURL(url => url.href.includes('Attachment') || url.href.includes('ViewAttachment'), { timeout: 20000 }).catch(() => {});
      await adjuntosPage.waitForLoadState('domcontentloaded', { timeout: 20000 }).catch(() => {});
      await esperarConDelay(2000);

      let adjUrl = adjuntosPage.url();
      console.log(`[DESCARGA] Ventana adjuntos: ${adjUrl.substring(0, 150)}`);

      // El popup de MP puede pasar por una redirección intermedia a 403.html antes de llegar a la
      // URL real de ViewAttachment.aspx (documentado en modulos/adjuntos.js del daemon, que
      // funciona en Cloud Run). No declarar error aún: esperar a que la navegación se asiente y
      // re-chequear la URL antes de rendirse.
      if (adjUrl.includes('403') || adjUrl.includes('.html') || adjUrl.includes('error')) {
        console.log('[DESCARGA] URL intermedia 403/error detectada — esperando posible redirección final...');
        await esperarConDelay(5000);
        await adjuntosPage.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {});
        adjUrl = adjuntosPage.url();
        console.log(`[DESCARGA] Ventana adjuntos (tras 403): ${adjUrl.substring(0, 150)}`);
      }

      if (adjUrl.includes('403') || adjUrl.includes('.html') || adjUrl.includes('error')) {
        await screenshotOnError(adjuntosPage, ADJUNTOS_DIR, `adjuntos-403-${codigo}`);
        await adjuntosPage.close().catch(() => {});
        if (intento < MAX_REINTENTOS) { await esperarConDelay(5000); continue; }
        return { ok: false, error: 'Redirección a error/403 en ventana de adjuntos', filas: [] };
      }

      const robotBlock = await adjuntosPage.evaluate(() => {
        const hasRobotImg = document.querySelector('img[src*="robot.png"]') !== null;
        const hasAccesoDenegado = document.body.innerText.toLowerCase().includes('acceso denegado');
        return hasRobotImg || hasAccesoDenegado || document.title.toLowerCase().includes('acceso denegado');
      });

      if (robotBlock) {
        await screenshotOnError(adjuntosPage, ADJUNTOS_DIR, `adjuntos-robot-${codigo}`);
        await adjuntosPage.close().catch(() => {});
        return { ok: false, error: 'Bloqueo anti-bot de Mercado Público (cupo de "Ver Adjuntos" o acceso denegado)', filas: [] };
      }

      // Esperar activamente a que la tabla de adjuntos esté presente en el DOM
      await adjuntosPage.waitForSelector('table, #DWNL_grdId, body', { timeout: 15000 }).catch(() => {});
      await esperarConDelay(1000);

      const evalResult = await adjuntosPage.evaluate(() => {
        // Diagnóstico: capturar contexto de la página ANTES de decidir (para detectar
        // anti-bot/reCAPTCHA/estructura distinta en Cloud Run sin adivinar).
        const diag = {
          url: location.href.substring(0, 150),
          title: document.title,
          bodyPreview: (document.body?.innerText || '').replace(/\s+/g, ' ').trim().substring(0, 400),
          tablas: Array.from(document.querySelectorAll('table')).map(t => t.id || t.className || 'sin-id').slice(0, 10),
          inputImages: document.querySelectorAll('input[type="image"]').length,
          iframes: document.querySelectorAll('iframe').length,
        };

        // 1. Localizar tabla de adjuntos por selectores prioritarios (mismo set ampliado
        //    que modulos/adjuntos.js del daemon, que funciona en Cloud Run)
        let table = document.getElementById('DWNL_grdId')
          || document.querySelector('table[id*="DWNL_grdId"]')
          || document.querySelector('table[id*="grdId"]')
          || document.querySelector('table[id*="DWNL"]')
          || document.querySelector('table[id*="Adjuntos"]');

        // 2. Si no encontró por ID, buscar tablas con inputs tipo imagen o de descarga
        if (!table) {
          const allTables = Array.from(document.querySelectorAll('table'));
          table = allTables.find(t =>
            t.querySelector('input[type="image"]') ||
            t.querySelector('input[name*="DWNL"]') ||
            t.querySelector('input[id*="DWNL"]')
          );
        }

        const bodyText = (document.body?.innerText || '').toLowerCase();
        const noAdjuntosKeywords = [
          'no se encontraron registros',
          'no existen registros',
          'no existen archivos adjuntos',
          'sin archivos',
          'sin adjuntos',
          'no registra adjuntos',
          '0 registros'
        ];
        const tieneMensajeVacio = noAdjuntosKeywords.some(kw => bodyText.includes(kw));

        if (!table) {
          if (tieneMensajeVacio || !document.querySelector('input[type="image"]')) {
            return { filas: [], ok: true, diag };
          }
          return { filas: null, ok: false, error: 'Estructura de la página de adjuntos no reconocida', diag };
        }

        const rows = table.querySelectorAll('tr');
        if (rows.length <= 1) {
          return { filas: [], ok: true, diag };
        }

        const resultados = [];
        for (let i = 1; i < rows.length; i++) {
          const cells = rows[i].querySelectorAll('td');
          if (cells.length < 2) continue;

          const verBtn = rows[i].querySelector('input[type="image"]')
            || rows[i].querySelector('input[type="submit"]')
            || rows[i].querySelector('input[type="button"]')
            || rows[i].querySelector('a');

          const btnId = verBtn?.id || verBtn?.getAttribute('name') || '';

          let nombre = '';
          let tipo = '';
          let descripcion = '';
          let tamanio = '';
          let fecha = '';

          if (cells.length >= 7) {
            nombre = cells[1]?.textContent?.trim() || '';
            tipo = cells[2]?.textContent?.trim() || '';
            descripcion = cells[3]?.textContent?.trim() || '';
            tamanio = cells[4]?.textContent?.trim() || '';
            fecha = cells[5]?.textContent?.trim() || '';
          } else {
            nombre = cells[0]?.textContent?.trim() || cells[1]?.textContent?.trim() || '';
            tipo = cells[1]?.textContent?.trim() || '';
          }

          if (!nombre && !btnId) continue;
          if (!nombre) nombre = `documento_${i}`;

          const esActa = tipo.toLowerCase().includes('acta') && tipo.toLowerCase().includes('evaluaci')
            || nombre.toLowerCase().includes('acta') && nombre.toLowerCase().includes('evaluaci');

          resultados.push({
            nombre,
            tipo,
            descripcion,
            tamanio,
            fecha,
            btnId,
            esActa
          });
        }

        return { filas: resultados, ok: true, diag };
      });

      console.log(`[DESCARGA] Filas encontradas en grilla: ${evalResult.filas ? evalResult.filas.length : 0}`);
      if (evalResult.diag && (!evalResult.filas || evalResult.filas.length === 0)) {
        console.log(`[DESCARGA][DIAG] url=${evalResult.diag.url}`);
        console.log(`[DESCARGA][DIAG] title=${evalResult.diag.title}`);
        console.log(`[DESCARGA][DIAG] body=${evalResult.diag.bodyPreview}`);
        console.log(`[DESCARGA][DIAG] tablas=${JSON.stringify(evalResult.diag.tablas)} inputImages=${evalResult.diag.inputImages} iframes=${evalResult.diag.iframes}`);
      }

      if (!evalResult.ok || evalResult.filas === null) {
        await screenshotOnError(adjuntosPage, ADJUNTOS_DIR, `adjuntos-error-estructura-${codigo}`);
        await adjuntosPage.close().catch(() => {});
        if (intento < MAX_REINTENTOS) { await esperarConDelay(3000); continue; }
        return { ok: false, error: evalResult.error || 'Estructura de la página de adjuntos no reconocida', filas: [] };
      }

      return { ok: true, page: adjuntosPage, filas: evalResult.filas };
    } catch (e) {
      if (adjuntosPage && !adjuntosPage.isClosed()) {
        await adjuntosPage.close().catch(() => {});
      }
      if (intento < MAX_REINTENTOS) { await esperarConDelay(3000); continue; }
      return { ok: false, error: e.message, filas: [] };
    }
  }
  return { ok: false, error: 'Máximos reintentos excedido', filas: [] };
}

async function descargarTodos(licitacionId, codigo, fichaPage, context, carpetaBase) {
  const { ok, page: adjuntosPage, filas, error } = await abrirGrillaAdjuntos(fichaPage, context, codigo);
  if (!ok) {
    if (adjuntosPage && !adjuntosPage.isClosed()) await adjuntosPage.close().catch(() => {});
    return { ok: false, error };
  }

  try {
    if (!filas.length) {
      console.log('[DESCARGA] La licitación no tiene adjuntos en la grilla');
      await adjuntosPage.close().catch(() => {});
      return { ok: true, descargados: 0, errores: 0 };
    }

    const carpeta = path.join(carpetaBase, 'licitaciones', codigo, 'adjuntos');
    fs.mkdirSync(carpeta, { recursive: true });

    let descargados = 0;
    let errores = 0;

    for (let idx = 0; idx < filas.length; idx++) {
      const fila = filas[idx];
      try {
        const downloadPromise = adjuntosPage.waitForEvent('download', { timeout: 30000 }).catch(e => null);
        
        let verBtn = null;
        if (fila.btnId) {
          verBtn = adjuntosPage.locator(`[id="${fila.btnId}"], [name="${fila.btnId}"]`).first();
        }
        if (!verBtn || !(await verBtn.count().catch(() => 0))) {
          verBtn = adjuntosPage.locator('#DWNL_grdId input[type="image"], input[type="image"]').nth(idx);
        }

        await verBtn.click({ noWaitAfter: true, force: true });
        const download = await downloadPromise;

        if (!download) {
          console.log(`[DESCARGA] Sin evento de descarga para "${fila.nombre}"`);
          errores++;
          continue;
        }

        const sug = download.suggestedFilename() || '';
        const extSug = path.extname(sug);
        const nombreArchivo = sanitizarNombre(fila.nombre) + (path.extname(fila.nombre) ? '' : extSug);
        const rutaLocal = path.join(carpeta, nombreArchivo);
        await download.saveAs(rutaLocal);

        const rutaStorage = path.join('licitaciones', codigo, 'adjuntos', nombreArchivo).replace(/\\/g, '/');

        const res = await upsertConHash(licitacionId, fila, rutaLocal, rutaStorage);
        if (res.error) {
          console.log(`[DESCARGA] Error registrando "${fila.nombre}": ${res.error}`);
          errores++;
        } else {
          descargados++;
          console.log(`[DESCARGA] OK "${fila.nombre}" (v${res.version}, sha=${(res.sha || '').substring(0, 12)}...)`);
        }
      } catch (e) {
        console.log(`[DESCARGA] Error descargando "${fila.nombre}": ${e.message}`);
        errores++;
      }
    }

    await adjuntosPage.close().catch(() => {});
    return { ok: true, descargados, errores };
  } catch (e) {
    await adjuntosPage.close().catch(() => {});
    return { ok: false, error: e.message };
  }
}

async function main() {
  const { codigo, licitacionId } = parseArgs();

  if (!codigo || !licitacionId) {
    console.log('[DESCARGA] resultado=error motivo=Se requieren --codigo y --licitacionId');
    process.exit(2);
  }

  console.log(`[DESCARGA] Inicio: licitación ${codigo} (id=${licitacionId})`);
  const inicioMs = Date.now();
  await initDB();

  let browser, context, page;
  try {
    // Flujo con sesión de proveedor (patrón del daemon, agente-mp.js): cargar la sesión
    // persistida desde BD, pasarla al navegador, y verificar/renovar con login() si expiró.
    // Contexto: la ficha pública responde 200 SIN sesión pero el anti-bot de Mercado Público
    // devuelve 403.html en "Ver Adjuntos" desde IPs de datacenter (Cloud Run) incluso con
    // Chromium headed/Xvfb. Con sesión real + portal interno (DetailsAcquisition.aspx) el
    // reCAPTCHA Enterprise de ViewAttachmentLC.aspx se resuelve con el fingerprint del daemon.
    const sessionState = await obtenerEstadoSesion();
    const instancia = await launch(HEADLESS, sessionState);
    browser = instancia.browser;
    context = instancia.context;
    page = instancia.page;

    // MP_PROXY (opcional): si se define, se crea un contexto con proxy para que el tráfico salga
    // por una IP que Mercado Público no bloquee (403.html en "Ver Adjuntos" desde IPs de
    // datacenter como Cloud Run). Formato: http://usuario:pass@host:puerto
    if (process.env.MP_PROXY) {
      console.log(`[DESCARGA] Usando proxy: ${process.env.MP_PROXY.split('@').pop()}`);
      await context.close().catch(() => {});
      const ctxProxy = await browser.newContext({
        viewport: { width: 1920, height: 1080 },
        acceptDownloads: true,
        locale: 'es-CL',
        timezoneId: 'America/Santiago',
        proxy: { server: process.env.MP_PROXY },
      });
      ctxProxy.setDefaultTimeout(30000);
      ctxProxy.setDefaultNavigationTimeout(45000);
      context = ctxProxy;
      page = await context.newPage();
    }

    if (sessionState) {
      console.log('[DESCARGA] Sesión persistida encontrada — verificando con login()...');
      try {
        await login(page, context);
        const nuevoEstado = await context.storageState();
        await guardarEstadoSesion(nuevoEstado);
        console.log('[DESCARGA] Sesión verificada/renovada correctamente');
      } catch (loginErr) {
        console.log(`[DESCARGA] ADVERTENCIA: login() falló (${loginErr.message}) — continuando sin sesión renovada`);
        if (loginErr.isRobotBlock) {
          console.log('[DESCARGA] Bloqueo anti-robot en login');
        }
      }
    }

    // Siempre entrar a través de fichaLicitacion.html para que el router de Mercado Público
    // convierta el código en el token ?qs= autorizado antes de abrir la ficha oficial
    const fichaUrl = FICHA_URL_PUBLICA.replace('{codigo}', encodeURIComponent(codigo));
    console.log(`[DESCARGA] Abriendo ficha pública para resolución de token qs: ${fichaUrl}`);
    await page.goto(fichaUrl, { waitUntil: 'commit', timeout: 60000 }).catch(async (e) => {
      console.log(`[DESCARGA] Primer goto con timeout: ${e.message.split('\n')[0]} — reintentando...`);
      await page.goto(fichaUrl, { waitUntil: 'commit', timeout: 60000 }).catch(() => {});
    });

    // Esperar la redirección automática hacia DetailsAcquisition.aspx?qs=
    await page.waitForURL(url => url.href.toLowerCase().includes('detailsacquisition') || url.href.toLowerCase().includes('rfb') || url.href.toLowerCase().includes('procurement'), { timeout: 30000 }).catch(() => {});
    console.log(`[DESCARGA] Ficha oficial resuelta con token: ${page.url().substring(0, 100)}...`);

    // Esperar a que los elementos clave de la ficha oficial estén cargados
    await page.locator('#imgAdjuntos, #lblFicha, #txtNumeroLicitacion, .cssFichaTabla').first().waitFor({ state: 'attached', timeout: 30000 }).catch(() => {});
    await esperarConDelay(2000);

    const imgAdjuntos = page.locator('#imgAdjuntos');
    const hayAdjuntos = await imgAdjuntos.count().catch(() => 0);
    if (!hayAdjuntos) {
      const urlActual = page.url();
      const esFichaValida = urlActual.toLowerCase().includes('detailsacquisition') || urlActual.toLowerCase().includes('rfb');
      if (!esFichaValida) {
        console.log(`[DESCARGA] Error: La ficha oficial no redirigió correctamente (URL=${urlActual})`);
        await marcarFinalizada(licitacionId, 'error', 'No se pudo cargar la ficha oficial de la licitación en Mercado Público');
        await registrarLogExtraccion(licitacionId, 'fallo', 0, 'Timeout en carga de ficha', Date.now() - inicioMs);
        return;
      }
      console.log('[DESCARGA] La ficha no tiene "Ver Adjuntos" (licitación sin documentos)');
      await marcarFinalizada(licitacionId, 'completado', null);
      await registrarLogExtraccion(licitacionId, 'sin_adjuntos', 0, null, Date.now() - inicioMs);
      console.log('[DESCARGA] resultado=exito descargados=0 errores=0');
      return;
    }

    const resultado = await descargarTodos(licitacionId, codigo, page, context, ADJUNTOS_DIR);

    if (!resultado.ok) {
      await marcarFinalizada(licitacionId, 'error', resultado.error);
      await registrarLogExtraccion(licitacionId, 'fallo', 0, resultado.error, Date.now() - inicioMs);
      console.log(`[DESCARGA] resultado=error motivo=${resultado.error}`);
      return;
    }

    await marcarFinalizada(licitacionId, 'completado', null);
    await registrarLogExtraccion(licitacionId, 'exito', resultado.descargados, null, Date.now() - inicioMs);
    console.log(`[DESCARGA] resultado=exito descargados=${resultado.descargados} errores=${resultado.errores}`);
  } catch (e) {
    console.log(`[DESCARGA] resultado=error motivo=${e.message}`);
    await marcarFinalizada(licitacionId, 'error', e.message).catch(() => {});
    await registrarLogExtraccion(licitacionId, 'fallo', 0, e.message, Date.now() - inicioMs).catch(() => {});
    process.exitCode = 1;
  } finally {
    await close(browser, context, page).catch(() => {});
    await closeDB().catch(() => {});
  }
}

main();
