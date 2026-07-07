#!/usr/bin/env node
// SPIKE de descubrimiento (research.md R1 de 016-extraccion-documentos-api, tarea T004).
// Loguea, busca UNA licitación adjudicada, abre su ficha, abre la ventana de adjuntos y
// descarga el primer documento, registrando en stderr (no en el HTML/JSON de salida) la
// URL exacta y los parámetros del postback — para completar contracts/internal-api.md.
//
// Uso: MP_RUT=... MP_PASSWORD=... node spike-adjuntos.js
// Salida: JSON con { listadoUrl, camposOcultos, filas, postback: { url, method, bodyKeys } }

import { fileURLToPath } from 'url';
import path from 'path';
import dotenv from 'dotenv';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

dotenv.config({ path: path.join(__dirname, '.env'), override: true });
dotenv.config({ path: path.join(__dirname, '..', '..', '.env') });

import { launch, close, esperarConDelay } from './modulos/browser.js';
import { login } from './modulos/login.js';
import { buscarLicitaciones } from './modulos/buscar.js';

const HEADLESS = process.env.MP_HEADLESS !== 'false';

function log(...args) { console.error(...args); }

async function main() {
  let browser, context, page;
  const hallazgos = { listadoUrl: null, camposOcultos: [], filas: [], postback: null, error: null };

  try {
    const launched = await launch(HEADLESS);
    browser = launched.browser; context = launched.context; page = launched.page;

    await login(page, context);
    log('[SPIKE] Login OK, buscando licitaciones adjudicadas...');

    const licitaciones = await buscarLicitaciones(page, context);
    log(`[SPIKE] ${licitaciones.length} licitaciones encontradas`);
    if (licitaciones.length === 0) throw new Error('Sin resultados de busqueda');

    let adjPage = null;
    let intentosLicitacion = 0;

    for (const candidata of licitaciones.slice(0, 5)) {
      intentosLicitacion++;
      log(`[SPIKE] Probando licitacion ${intentosLicitacion}/5: ${candidata.codigo}`);

      const pagesBefore = context.pages().length;
      await page.evaluate((onclick) => { eval(onclick); }, candidata.onclick).catch(async () => {
        await page.goto(candidata.urlFicha, { waitUntil: 'networkidle', timeout: 45000 });
      });
      await esperarConDelay(3000);

      let fichaPage = page;
      const pagesAfterFicha = context.pages();
      if (pagesAfterFicha.length > pagesBefore) {
        fichaPage = pagesAfterFicha[pagesAfterFicha.length - 1];
      }
      await fichaPage.waitForLoadState('domcontentloaded', { timeout: 20000 }).catch(() => {});

      for (let reintento = 1; reintento <= 3; reintento++) {
        log(`[SPIKE] Click en #imgAdjuntos (intento ${reintento}/3)...`);
        const pagesBeforeAdj = context.pages().length;
        const imgAdjuntos = fichaPage.locator('#imgAdjuntos');
        const visible = await imgAdjuntos.waitFor({ state: 'visible', timeout: 15000 }).then(() => true).catch(() => false);
        if (!visible) { log('[SPIKE] #imgAdjuntos no visible en esta ficha'); break; }

        await imgAdjuntos.click();
        await esperarConDelay(3000);

        const pagesAfterAdj = context.pages();
        if (pagesAfterAdj.length <= pagesBeforeAdj) { log('[SPIKE] No se abrio ventana nueva, reintentando...'); await esperarConDelay(2000); continue; }

        const candidataAdjPage = pagesAfterAdj[pagesAfterAdj.length - 1];
        const url = candidataAdjPage.url();
        log(`[SPIKE] Ventana abierta: ${url.substring(0, 100)}`);

        if (url.includes('403') || url.includes('error')) {
          log('[SPIKE] 403/error, cerrando y reintentando...');
          await candidataAdjPage.close().catch(() => {});
          await esperarConDelay(3000);
          continue;
        }

        adjPage = candidataAdjPage;
        break;
      }

      if (adjPage) break;
      if (fichaPage !== page) await fichaPage.close().catch(() => {});
    }

    if (!adjPage) throw new Error('No se pudo abrir la ventana de adjuntos en ninguna de las licitaciones probadas (403 persistente)');

    hallazgos.listadoUrl = adjPage.url();
    log(`[SPIKE] URL ventana adjuntos (exito): ${hallazgos.listadoUrl}`);

    await adjPage.waitForLoadState('domcontentloaded', { timeout: 20000 }).catch(() => {});
    await esperarConDelay(2000);

    hallazgos.camposOcultos = await adjPage.evaluate(() =>
      Array.from(document.querySelectorAll('input[type=hidden]')).map(el => el.name || el.id));

    hallazgos.filas = await adjPage.evaluate(() => {
      const table = document.getElementById('DWNL_grdId');
      if (!table) return [];
      const rows = table.querySelectorAll('tr');
      const res = [];
      for (let i = 1; i < rows.length; i++) {
        const cells = rows[i].querySelectorAll('td');
        if (cells.length < 7) continue;
        const btn = cells[6].querySelector('input[type="image"]');
        res.push({
          nombre: cells[1]?.textContent?.trim(),
          tipo: cells[2]?.textContent?.trim(),
          botonName: btn?.getAttribute('name'),
          botonId: btn?.id,
        });
      }
      return res;
    });

    log(`[SPIKE] Campos ocultos: ${JSON.stringify(hallazgos.camposOcultos)}`);
    log(`[SPIKE] Filas encontradas: ${hallazgos.filas.length}`);

    if (hallazgos.filas.length > 0 && hallazgos.filas[0].botonName) {
      log('[SPIKE] Capturando el POST del postback al descargar el primer adjunto...');
      const postbackPromise = adjPage.waitForRequest(
        req => req.method() === 'POST' && req.url() === hallazgos.listadoUrl,
        { timeout: 15000 }
      ).catch(() => null);

      const boton = adjPage.locator(`#${hallazgos.filas[0].botonId}`);
      if (await boton.count() > 0) {
        await boton.click().catch(() => {});
      }

      const postReq = await postbackPromise;
      if (postReq) {
        hallazgos.postback = {
          url: postReq.url(),
          method: postReq.method(),
          bodyKeys: (postReq.postData() || '').split('&').map(p => p.split('=')[0]),
        };
        log(`[SPIKE] Postback capturado. Body keys: ${hallazgos.postback.bodyKeys.join(', ')}`);
      } else {
        log('[SPIKE] No se capturo un POST — puede que la descarga sea GET directo o via nueva ventana');
      }
    }

    await adjPage.close().catch(() => {});
  } catch (e) {
    hallazgos.error = e.message;
    log(`[SPIKE] ERROR: ${e.message}`);
  } finally {
    await close(browser, context, page);
  }

  process.stdout.write(JSON.stringify(hallazgos, null, 2));
}

main();
