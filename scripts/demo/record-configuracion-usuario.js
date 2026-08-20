/**
 * Script de Grabación — Configuración de Usuario (Perfil + Canal de Correo)
 * MPM (Mercado Público Management)
 *
 * Recorre el segmento de "Mi Perfil y Configuración" que el guion narrado promete
 * pero el demo principal no muestra: personalizar preferencias y vincular el canal
 * de correo para alertas (Telegram ya no existe en la plataforma, solo correo).
 *
 * Flujo:
 * 1. Login
 * 2. Navegar a /catalogos?tab=portal (Parámetros Mercado Público)
 * 3. Abrir el dropdown de usuario (avatar) → Mi Perfil
 * 4. Mostrar la pestaña "Configuración Alertas" con el Canal de Correo
 *
 * Uso:
 *   cd "scripts/demo"
 *   node record-configuracion-usuario.js            (grabar video)
 *   node record-configuracion-usuario.js --no-video (solo probar)
 */

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

// Configuración
const BASE_URL = process.env.BASE_URL || process.argv.find(a => a.startsWith('--url='))?.split('=')[1] || 'https://mpm-web-6nnd6y6owa-uc.a.run.app';
const EMAIL = process.env.MPM_DEMO_EMAIL || 'admin@tivit.cl';
const PASSWORD = process.env.MPM_DEMO_PASSWORD || 'test1234';
const IS_HEADLESS = process.argv.includes('--headless');
const RECORD_VIDEO = !process.argv.includes('--no-video');
const RECORDINGS_DIR = path.resolve(__dirname, 'recordings');

// Pausas para la narración (cortas: este segmento es complementario al demo principal)
const PAUSES = {
  intro: 10000,       // Portada / contexto
  login: 5000,        // Login
  catalogos: 12000,   // Catálogos > Parámetros Mercado Público
  perfil: 9000,       // Mi Perfil
  configuracion: 14000, // Configuración Alertas (Canal de Correo)
  cierre: 45000,      // Conclusión final narrada (guion-configuracion-tts.md, bloque 06)
};

const beat = (ms = 1200) => new Promise(resolve => setTimeout(resolve, ms));

/**
 * Inyecta un cursor visual personalizado con animación de clic y movimiento suave.
 */
async function injectVirtualCursor(page) {
  await page.addInitScript(() => {
    const CURSOR_ID = '__mpm_virtual_cursor__';
    const RIPPLE_ID = '__mpm_cursor_ripple__';

    function init() {
      if (document.getElementById(CURSOR_ID)) return;

      const cursor = document.createElement('div');
      cursor.id = CURSOR_ID;
      cursor.style.cssText = `
        position: fixed;
        top: 0; left: 0;
        width: 22px; height: 22px;
        border-radius: 50%;
        background: rgba(22, 119, 255, 0.4);
        border: 2.5px solid #1677ff;
        box-shadow: 0 0 10px rgba(22, 119, 255, 0.5);
        pointer-events: none;
        z-index: 2147483647;
        transform: translate(-50%, -50%);
        transition: transform 0.15s ease, background 0.15s ease, border-color 0.15s ease;
      `;

      const ripple = document.createElement('div');
      ripple.id = RIPPLE_ID;
      ripple.style.cssText = `
        position: fixed;
        top: 0; left: 0;
        width: 44px; height: 44px;
        border-radius: 50%;
        border: 2px solid #ff4d4f;
        background: rgba(255, 77, 79, 0.25);
        pointer-events: none;
        z-index: 2147483646;
        transform: translate(-50%, -50%) scale(0);
        opacity: 0;
        transition: transform 0.4s ease-out, opacity 0.4s ease-out;
      `;

      document.documentElement.appendChild(cursor);
      document.documentElement.appendChild(ripple);

      window.addEventListener('mousemove', (e) => {
        cursor.style.left = e.clientX + 'px';
        cursor.style.top = e.clientY + 'px';
      }, true);

      window.addEventListener('mousedown', (e) => {
        cursor.style.transform = 'translate(-50%, -50%) scale(0.75)';
        cursor.style.background = 'rgba(255, 77, 79, 0.7)';
        cursor.style.borderColor = '#ff4d4f';

        ripple.style.left = e.clientX + 'px';
        ripple.style.top = e.clientY + 'px';
        ripple.style.transform = 'translate(-50%, -50%) scale(1)';
        ripple.style.opacity = '1';
      }, true);

      window.addEventListener('mouseup', () => {
        cursor.style.transform = 'translate(-50%, -50%) scale(1)';
        cursor.style.background = 'rgba(22, 119, 255, 0.4)';
        cursor.style.borderColor = '#1677ff';

        ripple.style.transform = 'translate(-50%, -50%) scale(1.6)';
        ripple.style.opacity = '0';
      }, true);
    }

    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', init);
    } else {
      init();
    }
  });
}

/**
 * Click suave con movimiento animado previo
 */
async function smoothClick(page, selectorOrLocator, options = {}) {
  try {
    const locator = typeof selectorOrLocator === 'string' ? page.locator(selectorOrLocator).first() : selectorOrLocator;
    await locator.waitFor({ state: 'visible', timeout: options.timeout || 10000 });
    const box = await locator.boundingBox();
    if (box) {
      const targetX = box.x + box.width / 2;
      const targetY = box.y + box.height / 2;
      await page.mouse.move(targetX, targetY, { steps: 14 });
      await beat(200);
      await locator.click(options);
      await beat(350);
      return true;
    }
  } catch (err) {
    console.warn(`[smoothClick] Warning: ${selectorOrLocator} (${err.message})`);
  }
  return false;
}

/**
 * Búsqueda de elemento por texto y clic inteligente en contenedor clickeable
 */
async function clickByText(page, selector, text) {
  try {
    const handle = await page.evaluateHandle(([sel, txt]) => {
      const candidates = Array.from(document.querySelectorAll(sel)).filter(
        el => el.textContent.trim().includes(txt) && el.offsetParent !== null
      );
      const match = candidates.find(
        el => !candidates.some(other => other !== el && el.contains(other))
      );
      if (!match) return null;
      return match.closest('button, a, li, label, [role="tab"], [role="menuitem"], [role="button"], .ant-segmented-item, .ant-tabs-tab, .ant-menu-item, [role="menuitem"]') || match;
    }, [selector, text]);

    const el = handle.asElement();
    if (el) {
      const box = await el.boundingBox();
      if (box) {
        await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2, { steps: 12 });
        await beat(200);
        await el.click();
        await beat(400);
        return true;
      }
    }
  } catch (err) {
    console.warn(`[clickByText] Warning clickeando "${text}": ${err.message}`);
  }
  return false;
}

(async () => {
  if (RECORD_VIDEO && !fs.existsSync(RECORDINGS_DIR)) {
    fs.mkdirSync(RECORDINGS_DIR, { recursive: true });
  }

  console.log('================================================================');
  console.log('  MPM — GRABACIÓN DE CONFIGURACIÓN DE USUARIO (PROD)');
  console.log(`  URL Destino: ${BASE_URL}`);
  console.log(`  Grabación de Video: ${RECORD_VIDEO ? 'ACTIVADA (' + RECORDINGS_DIR + ')' : 'DESACTIVADA'}`);
  console.log('================================================================\n');

  const browser = await chromium.launch({
    headless: IS_HEADLESS,
    args: ['--start-maximized', '--disable-infobars'],
  });

  const context = await browser.newContext({
    viewport: null, // + --start-maximized => abre maximizada
    recordVideo: RECORD_VIDEO ? { dir: RECORDINGS_DIR } : undefined,
  });

  const page = await context.newPage();
  await injectVirtualCursor(page);

  page.on('console', msg => {
    if (msg.type() === 'error') console.log(`[Browser Console Error] ${msg.text()}`);
  });
  page.on('response', resp => {
    if (resp.status() >= 400) console.log(`[HTTP ${resp.status()}] ${resp.url()}`);
  });

  try {
    // ----------------------------------------------------
    // [01] PORTADA / LOGIN
    // ----------------------------------------------------
    console.log('[01] Portada e inicio de sesión institucional');
    await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle' });
    await beat(PAUSES.intro);

    // ----------------------------------------------------
    // [02] LOGIN
    // ----------------------------------------------------
    console.log('[02] Autenticación');
    const emailInput = page.getByTestId('login-email').or(page.locator('input[placeholder*="correo"]')).first();
    const passInput = page.getByTestId('login-password').or(page.locator('input[type="password"]')).first();
    const submitBtn = page.getByTestId('login-submit').or(page.locator('button:has-text("Ingresar")')).first();

    await emailInput.waitFor({ state: 'visible', timeout: 15000 });
    await smoothClick(page, emailInput);
    await emailInput.fill(EMAIL);
    await beat(400);

    await smoothClick(page, passInput);
    await passInput.fill(PASSWORD);
    await beat(500);

    await smoothClick(page, submitBtn);
    await page.waitForURL(/\/licitaciones/, { timeout: 30000 });
    await beat(PAUSES.login);

    // ----------------------------------------------------
    // [03] CATÁLOGOS > PARÁMETROS MERCADO PÚBLICO
    // ----------------------------------------------------
    console.log('[03] Navegando a Catálogos > Parámetros Mercado Público');
    await clickByText(page, '.ant-menu-item, a, span', 'Catálogos');
    await page.waitForURL('**/catalogos**', { timeout: 15000 });
    await beat(1500);

    // Abrir la pestaña "Parámetros Mercado Público" (tab=portal)
    await clickByText(page, '.ant-tabs-tab', 'Parámetros Mercado Público');
    await page.waitForURL('**/catalogos?tab=portal**', { timeout: 10000 }).catch(() => {});
    await beat(1500);
    // Esperar el título real de la pestaña portal (visible) — no una tabla genérica,
    // porque otras pestañas ocultas también tienen tablas y matchean primero.
    await page.getByText('Estados de Licitación en Mercado Público', { exact: false }).first().waitFor({ state: 'visible', timeout: 15000 });
    await beat(PAUSES.catalogos);

    // ----------------------------------------------------
    // [04] ABRIR MENÚ DE USUARIO > MI PERFIL
    // ----------------------------------------------------
    console.log('[04] Abriendo menú de usuario > Mi Perfil');
    // El avatar del usuario es el que está dentro de un .ant-dropdown-trigger (arriba a la
    // derecha del header). El otro .ant-avatar de la página es decorativo (abajo) y no abre menú.
    let avatar = page.locator('.ant-dropdown-trigger .ant-avatar').first();
    if (await avatar.count().catch(() => 0) === 0) {
      avatar = page.locator('.ant-avatar').first();
    }
    if (await avatar.isVisible()) {
      await smoothClick(page, avatar);
      await beat(800);
      await clickByText(page, '.ant-dropdown-menu-item, [role="menuitem"], div, span', 'Mi perfil');
      await beat(1500);
      await page.locator('.ant-modal:has-text("Mi Perfil y Configuración")').first().waitFor({ state: 'visible', timeout: 10000 });
      await beat(PAUSES.perfil);
    }

    // ----------------------------------------------------
    // [05] PESTAÑA "CONFIGURACIÓN ALERTAS" — CANAL DE CORREO
    // ----------------------------------------------------
    console.log('[05] Mostrando Configuración Alertas > Canal de Correo');
    await clickByText(page, '.ant-tabs-tab', 'Configuración Alertas');
    await beat(1500);
    // Esperar el input del Canal de Correo visible (placeholder "ej. alertas-tivit@tivit.cl")
    await page.locator('input[placeholder*="alertas-tivit"]').first().waitFor({ state: 'visible', timeout: 10000 });
    await beat(PAUSES.configuracion);

    // ----------------------------------------------------
    // [06] CONCLUSIÓN FINAL — dejar el modal abierto mientras suena la narración de cierre
    // ----------------------------------------------------
    await beat(PAUSES.cierre);

    console.log('\n================================================================');
    console.log('  GRABACIÓN DE CONFIGURACIÓN COMPLETADA EXITOSAMENTE');
    console.log('================================================================\n');
  } catch (error) {
    console.error('Error durante la ejecución:', error);
    await page.screenshot({ path: path.resolve(__dirname, 'error_screenshot_config.png') }).catch(() => {});
    const currentUrl = page.url();
    console.log(`URL actual al fallar: ${currentUrl}`);
  } finally {
    await context.close();
    await browser.close();

    if (RECORD_VIDEO) {
      console.log(`Video guardado en: ${RECORDINGS_DIR}`);
    }
  }
})();
