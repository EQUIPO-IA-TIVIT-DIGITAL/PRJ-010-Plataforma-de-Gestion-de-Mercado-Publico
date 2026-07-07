#!/usr/bin/env node
// Segunda parte del spike: ¿se puede llegar a la ficha de una licitación por HTTP directo
// dado solo su código externo (sin pasar por la búsqueda del portal), y desde ahí extraer
// el token `enc` de #imgAdjuntos para armar la URL de adjuntos 100% por HTTP?
//
// Uso: MP_RUT=... MP_PASSWORD=... node spike-ficha-directa.js <codigoLicitacion>

import { fileURLToPath } from 'url';
import path from 'path';
import dotenv from 'dotenv';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
dotenv.config({ path: path.join(__dirname, '.env'), override: true });
dotenv.config({ path: path.join(__dirname, '..', '..', '.env') });

import { launch, close } from './modulos/browser.js';
import { login } from './modulos/login.js';

const HEADLESS = process.env.MP_HEADLESS !== 'false';
const CODIGO = process.argv[2] || '2153-41-LP26';

function log(...args) { console.error(...args); }

async function main() {
  let browser, context, page;
  const resultado = { intentos: [] };

  try {
    const launched = await launch(HEADLESS);
    browser = launched.browser; context = launched.context; page = launched.page;
    await login(page, context);

    const cookies = await context.cookies();
    const cookieHeader = cookies.map(c => `${c.name}=${c.value}`).join('; ');

    const candidatosUrl = [
      `https://www.mercadopublico.cl/Procurement/Modules/RFB/DetailsAcquisition.aspx?idlicitacion=${CODIGO}`,
      `https://www.mercadopublico.cl/Procurement/Modules/RFB/DetailsAcquisition.aspx?enc=${CODIGO}`,
      `https://www.mercadopublico.cl/BID/Modules/RFB/DetailsAcquisition.aspx?idlicitacion=${CODIGO}`,
    ];

    for (const url of candidatosUrl) {
      log(`[SPIKE2] Probando GET directo: ${url}`);
      try {
        const resp = await fetch(url, { headers: { Cookie: cookieHeader, 'User-Agent': 'Mozilla/5.0' }, redirect: 'follow' });
        const html = await resp.text();
        const tieneImgAdjuntos = html.includes('imgAdjuntos');
        const tieneCodigo = html.includes(CODIGO.replace(/-/g, ''));
        log(`[SPIKE2]   status=${resp.status} finalUrl=${resp.url} tieneImgAdjuntos=${tieneImgAdjuntos} tieneCodigo=${tieneCodigo} len=${html.length}`);
        resultado.intentos.push({ url, status: resp.status, finalUrl: resp.url, tieneImgAdjuntos, tieneCodigo, len: html.length });

        if (tieneImgAdjuntos) {
          const match = html.match(/id=["']imgAdjuntos["'][^>]*onclick=["']([^"']+)["']/i)
            || html.match(/onclick=["']([^"']*OpenGlobalPopup[^"']*)["'][^>]*id=["']imgAdjuntos["']/i);
          resultado.onclickImgAdjuntos = match ? match[1] : '(no matcheado por regex, revisar manualmente)';
          log(`[SPIKE2]   onclick de imgAdjuntos: ${resultado.onclickImgAdjuntos}`);
        }
      } catch (e) {
        log(`[SPIKE2]   ERROR: ${e.message}`);
        resultado.intentos.push({ url, error: e.message });
      }
    }
  } catch (e) {
    resultado.error = e.message;
    log(`[SPIKE2] ERROR general: ${e.message}`);
  } finally {
    await close(browser, context, page);
  }

  process.stdout.write(JSON.stringify(resultado, null, 2));
}

main();
