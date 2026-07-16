import { chromium } from 'playwright';

const DEFAULT_TIMEOUT = 30000;
const NAVIGATION_TIMEOUT = 45000;

export async function launch(headless = false, sessionState = null) {
  console.log(`\n${'='.repeat(60)}`);
  console.log('AGENTE MERCADO PUBLICO - INICIANDO NAVEGADOR');
  console.log(`Modo: ${headless ? 'HEADLESS (background)' : 'VISIBLE (debug)'}`);
  if (sessionState) {
    console.log('Cargando sesion existente desde BD...');
  }
  console.log(`${'='.repeat(60)}\n`);

  const browser = await chromium.launch({
    headless,
    // "channel: chromium" activa el NUEVO modo headless (binario completo de Chromium),
    // cuya huella es casi identica al modo visible. El headless por defecto de Playwright
    // usa el "chromium headless shell" (headless clasico), que es justamente el que los
    // anti-bot de Mercado Publico detectan y castigan con reCAPTCHA/403 -- era el motivo
    // por el que en Cloud Run habia que envolver todo con xvfb-run.
    channel: 'chromium',
    slowMo: headless ? 0 : 100,
    args: [
      '--disable-blink-features=AutomationControlled',
      '--no-sandbox',
      '--disable-setuid-sandbox',
    ],
  });

  // UA con la version REAL del motor: un UA hardcodeado (antes Chrome/125) que no coincide
  // con la version verdadera de Chromium es en si mismo una senal de bot facil de detectar.
  // Ademas el UA por defecto en headless dice "HeadlessChrome", que hay que limpiar.
  const versionReal = browser.version(); // p.ej. "139.0.7258.5"
  const userAgent = `Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/${versionReal} Safari/537.36`;

  const contextOptions = {
    viewport: { width: 1920, height: 1080 },
    acceptDownloads: true,
    userAgent,
    locale: 'es-CL',
    timezoneId: 'America/Santiago',
  };

  if (sessionState) {
    contextOptions.storageState = sessionState;
  }

  const context = await browser.newContext(contextOptions);

  context.setDefaultTimeout(DEFAULT_TIMEOUT);
  context.setDefaultNavigationTimeout(NAVIGATION_TIMEOUT);

  const page = await context.newPage();

  console.log('Navegador listo');

  return { browser, context, page };
}

export async function close(browser, context, page) {
  try {
    if (page && !page.isClosed()) {
      await page.close().catch(() => {});
    }
    if (context) {
      await context.close().catch(() => {});
    }
    if (browser) {
      await browser.close().catch(() => {});
    }
    console.log('\nNavegador cerrado correctamente');
  } catch (e) {
    console.log('\nError cerrando navegador:', e.message);
  }
}

export async function screenshotOnError(page, carpeta, nombre) {
  try {
    const fs = await import('fs');
    const path = await import('path');
    if (!fs.existsSync(carpeta)) fs.mkdirSync(carpeta, { recursive: true });
    const archivo = path.join(carpeta, `${nombre}-${Date.now()}.png`);
    await page.screenshot({ path: archivo, fullPage: true });
    console.log(`  Screenshot de error guardado: ${archivo}`);
    return archivo;
  } catch (e) {
    console.log('  No se pudo capturar screenshot:', e.message);
    return null;
  }
}

export async function esperarConDelay(ms) {
  const delay = parseInt(process.env.MP_DELAY_MS || '2000', 10);
  await new Promise(resolve => setTimeout(resolve, ms || delay));
}

export async function reintentar(fn, maxReintentos = 3, baseDelay = 2000) {
  for (let intento = 1; intento <= maxReintentos; intento++) {
    try {
      return await fn();
    } catch (e) {
      const delay = baseDelay * Math.pow(2, intento - 1);
      console.log(`  Intento ${intento}/${maxReintentos} fallo: ${e.message}`);
      if (intento < maxReintentos) {
        console.log(`  Reintentando en ${delay}ms...`);
        await new Promise(resolve => setTimeout(resolve, delay));
      } else {
        throw e;
      }
    }
  }
}