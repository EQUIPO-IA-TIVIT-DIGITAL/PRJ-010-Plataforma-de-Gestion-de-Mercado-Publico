#!/usr/bin/env node
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
const hasSystemDb = process.env.DB_HOST && process.env.DB_HOST !== 'localhost';
dotenv.config({ path: path.join(__dirname, '.env'), override: !hasSystemDb });
dotenv.config({ path: path.join(__dirname, '..', '..', '.env'), override: !hasSystemDb });

import { launch, close, esperarConDelay, screenshotOnError } from './modulos/browser.js';
import { login } from './modulos/login.js';
import { initDB, closeDB, obtenerEstadoSesion, guardarEstadoSesion, limpiarEstadoSesion } from './modulos/db.js';

const FICHA_URL_TEMPLATE = 'https://www.mercadopublico.cl/Procurement/Modules/RFB/DetailsAcquisition.aspx?idlicitacion={codigo}';
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
      const pagesBefore = context.pages().length;
      const imgAdjuntos = fichaPage.locator('#imgAdjuntos');
      await imgAdjuntos.waitFor({ state: 'visible', timeout: 10000 }).catch(() => {});
      await imgAdjuntos.click();
      await esperarConDelay(3000);

      const pagesAfter = context.pages();
      if (pagesAfter.length <= pagesBefore) {
        if (intento < MAX_REINTENTOS) { await esperarConDelay(2000); continue; }
        return { ok: false, error: 'Sin ventana de adjuntos', filas: [] };
      }

      adjuntosPage = pagesAfter[pagesAfter.length - 1];
      await adjuntosPage.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {});
      let adjUrl = adjuntosPage.url();

      if (adjUrl.includes('403')) {
        await esperarConDelay(4000);
        await adjuntosPage.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {});
        adjUrl = adjuntosPage.url();
      }

      if (adjUrl.includes('403') || adjUrl.includes('.html') || adjUrl.includes('error')) {
        await adjuntosPage.close().catch(() => {});
        if (intento < MAX_REINTENTOS) { await esperarConDelay(5000); continue; }
        return { ok: false, error: 'Redirección a error/403 en ventana de adjuntos', filas: [] };
      }

      await adjuntosPage.waitForLoadState('domcontentloaded', { timeout: 20000 }).catch(() => {});
      await esperarConDelay(2000);

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

      const evalResult = await adjuntosPage.evaluate(() => {
        const table = document.getElementById('DWNL_grdId');
        if (!table) return { filas: null };
        const rows = table.querySelectorAll('tr');
        const resultados = [];
        for (let i = 1; i < rows.length; i++) {
          const cells = rows[i].querySelectorAll('td');
          if (cells.length < 7) continue;
          const nombre = cells[1]?.textContent?.trim() || '';
          const tipo = cells[2]?.textContent?.trim() || '';
          const descripcion = cells[3]?.textContent?.trim() || '';
          const tamanio = cells[4]?.textContent?.trim() || '';
          const fecha = cells[5]?.textContent?.trim() || '';
          const verBtn = cells[6]?.querySelector('input[type="image"]');
          const btnId = verBtn?.id || '';
          if (!nombre || !btnId) continue;
          resultados.push({ nombre, tipo, descripcion, tamanio, fecha, btnId, esActa: tipo === 'Acta de Evaluación' });
        }
        return { filas: resultados };
      });

      if (evalResult.filas === null) {
        await adjuntosPage.close().catch(() => {});
        return { ok: false, error: 'Estructura de la página de adjuntos cambió (grid DWNL_grdId ausente)', filas: [] };
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

    for (const fila of filas) {
      try {
        const downloadPromise = adjuntosPage.waitForEvent('download', { timeout: 30000 }).catch(e => null);
        const verBtn = adjuntosPage.locator(`#${fila.btnId}`);
        await verBtn.click();
        const download = await downloadPromise;

        if (!download) {
          console.log(`[DESCARGA] Sin evento de descarga para "${fila.nombre}"`);
          errores++;
          continue;
        }

        const nombreArchivo = sanitizarNombre(download.suggestedFilename() || fila.nombre);
        const rutaLocal = path.join(carpeta, nombreArchivo);
        await download.saveAs(rutaLocal);

        // Ruta relativa al storage del backend (LocalStorageService / GCS): misma forma
        // que usa AdjuntosHttpExtractor (licitaciones/{codigo}/adjuntos).
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
    const sessionState = await obtenerEstadoSesion();
    const instancia = await launch(HEADLESS, sessionState);
    browser = instancia.browser;
    context = instancia.context;
    page = instancia.page;

    try {
      await login(page, context);
    } catch (loginErr) {
      if (loginErr.isRobotBlock) {
        console.log('[DESCARGA] BLOQUEO_ROBOT: true -- bloqueo anti-robot durante login');
      }
      await limpiarEstadoSesion().catch(() => {});
      throw loginErr;
    }

    const nuevoEstado = await context.storageState();
    await guardarEstadoSesion(nuevoEstado);

    const fichaUrl = FICHA_URL_TEMPLATE.replace('{codigo}', encodeURIComponent(codigo));
    console.log(`[DESCARGA] Abriendo ficha: ${fichaUrl}`);
    await page.goto(fichaUrl, { waitUntil: 'domcontentloaded', timeout: 45000 });

    const hayAdjuntos = await page.locator('#imgAdjuntos').count().catch(() => 0);
    if (!hayAdjuntos) {
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
