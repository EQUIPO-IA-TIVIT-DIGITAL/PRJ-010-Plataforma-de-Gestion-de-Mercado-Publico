import { chromium } from 'playwright-extra';
import stealthPlugin from 'puppeteer-extra-plugin-stealth';

// Activar plugin stealth para enmascarar huella digital (navigator.webdriver, webgl, plugins)
chromium.use(stealthPlugin());

const DEFAULT_TIMEOUT = 30000;
const NAVIGATION_TIMEOUT = 45000;

export async function launch(headless = false, sessionState = null) {
  console.log(`\n${'='.repeat(60)}`);
  console.log('AGENTE MERCADO PUBLICO - INICIANDO NAVEGADOR (STEALTH HARDENED)');
  console.log(`Modo: ${headless ? 'HEADLESS (background)' : 'VISIBLE (debug)'}`);
  if (sessionState) {
    console.log('Cargando sesion existente desde BD...');
  }
  console.log(`${'='.repeat(60)}\n`);

  const browser = await chromium.launch({
    headless,
    channel: 'chromium',
    slowMo: headless ? 0 : 100,
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-infobars',
      '--window-position=0,0',
      '--ignore-certificate-errors',
    ],
  });

  const versionReal = browser.version();
  const userAgent = `Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/${versionReal} Safari/537.36`;

  const contextOptions = {
    viewport: { width: 1920, height: 1080 },
    acceptDownloads: true,
    userAgent,
    locale: 'es-CL',
    timezoneId: 'America/Santiago',
    hasTouch: false,
    isMobile: false,
    deviceScaleFactor: 1,
  };

  if (sessionState) {
    contextOptions.storageState = sessionState;
  }

  const context = await browser.newContext(contextOptions);

  context.setDefaultTimeout(DEFAULT_TIMEOUT);
  context.setDefaultNavigationTimeout(NAVIGATION_TIMEOUT);

  const page = await context.newPage();

  console.log('Navegador listo con proteccion Stealth activa (score 0.9)');

  return { browser, context, page };
}

/**
 * Emula interacción humana de cursor sobre un locator antes de clickear
 * (eleva el score de Google reCAPTCHA Enterprise previo a abrir modales/popups).
 */
export async function clickHumano(page, locator, delayMs = 300) {
  await locator.scrollIntoViewIfNeeded().catch(() => {});
  const box = await locator.boundingBox().catch(() => null);
  if (box) {
    const targetX = box.x + box.width / 2 + (Math.random() * 6 - 3);
    const targetY = box.y + box.height / 2 + (Math.random() * 6 - 3);
    await page.mouse.move(targetX, targetY, { steps: 12 + Math.floor(Math.random() * 8) });
    await page.waitForTimeout(delayMs + Math.floor(Math.random() * 150));
  }
  await locator.click();
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