#!/usr/bin/env node
// Loguea una vez contra Mercado Público y exporta las cookies de sesión como JSON a stdout.
// Usado por MpSessionProvider (spec 016-extraccion-documentos-api) para obtener cookies
// reutilizables por HttpClient, sin abrir un navegador por cada licitación.
//
// Uso: node exportar-sesion.js
// Salida (stdout, única línea JSON): [{ "name": "...", "value": "...", "domain": "...", "path": "..." }, ...]
// Cualquier otro log va a stderr para no contaminar la salida parseable.

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

function logErr(...args) {
  console.error(...args);
}

async function main() {
  let browser, context, page;
  try {
    const launched = await launch(HEADLESS);
    browser = launched.browser;
    context = launched.context;
    page = launched.page;

    await login(page, context);

    const cookies = await context.cookies();
    // Solo cookies del dominio de Mercado Público son relevantes para las descargas HTTP directas
    const cookiesRelevantes = cookies.filter(c => c.domain.includes('mercadopublico.cl'));

    process.stdout.write(JSON.stringify(cookiesRelevantes));
    logErr(`[EXPORTAR-SESION] ${cookiesRelevantes.length} cookies exportadas`);
    process.exitCode = 0;
  } catch (e) {
    logErr('[EXPORTAR-SESION] ERROR:', e.message);
    process.exitCode = 1;
  } finally {
    await close(browser, context, page);
  }
}

main();
