// Script de investigacion/validacion, NO parte del flujo normal del scraper.
// Valida el fix de extraccion de fechaPublicacion/fechaCierre/demandante en buscar.js.
// No escribe en la base, no descarga adjuntos, no llama a Gemini.
import path from 'path';
import { fileURLToPath } from 'url';
import dotenv from 'dotenv';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
dotenv.config({ path: path.join(__dirname, '.env'), override: true });
dotenv.config({ path: path.join(__dirname, '..', '..', '.env') });

import { launch, close } from './modulos/browser.js';
import { login } from './modulos/login.js';
import { buscarLicitaciones } from './modulos/buscar.js';

async function main() {
  const { browser, context, page } = await launch(process.env.MP_HEADLESS === 'true');

  try {
    await login(page, context);
    console.log('[INVESTIGACION] Login OK, buscando con MP_FECHA_DESDE=' + (process.env.MP_FECHA_DESDE || '(default)'));

    const licitaciones = await buscarLicitaciones(page, context);

    console.log(`\n[INVESTIGACION] ${licitaciones.length} licitaciones encontradas. Detalle:\n`);
    console.log(JSON.stringify(licitaciones, null, 2));
  } catch (e) {
    console.error('[INVESTIGACION] Error:', e.message);
  } finally {
    await close(browser, context, page);
  }
}

main();
