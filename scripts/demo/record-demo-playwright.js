/**
 * Script de Grabación Automatizada con Playwright — MPM (Mercado Público Management)
 *
 * Recorre las funcionalidades de la plataforma en PRODUCCIÓN:
 * 1. Acceso y Roles
 * 2. Licitaciones (Áreas de Negocio, Estados, Seguidas y De Interés)
 * 3. Búsqueda Semántica con IA Gemini
 * 4. Análisis con IA: workspace post-adjudicación YA cargado (dashboard comparativo + chat contextual)
 * 5. Dashboard Ejecutivo Global y Ranking de Competidores
 * 6. Inteligencia de Competidores y Actividad Total de Mercado
 * 7. Alertas Inteligentes con Expansión Semántica por IA
 * 8. Colaboración en Tiempo Real (SignalR) y Notificaciones
 * 9. Catálogos Corporativos TIVIT (Casos de Éxito, Certificaciones PDF y Plantilla DOCX)
 * 10. Perfil de Usuario (Configuración de Alertas por correo) y Cierre Institucional
 *
 * NOTA: el Módulo de Análisis con IA se muestra sobre un workspace YA cargado
 * (no se sube ni descarga una licitación nueva). La "Sala de Oferta" y el
 * "Centro de Administración" quedan fuera del demo.
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

// Pausas sincronizadas con el guion narrado de voz (en milisegundos)
const PAUSES = {
  intro: 14000,                  // [01] Portada / Contexto
  login: 5000,                   // [02] Login & Seguridad
  licitaciones_listado: 15000,   // [03] Licitaciones, áreas de negocio y vistas
  busqueda_ia: 14000,            // [04] Búsqueda semántica en lenguaje natural
  analisis_workspace: 12000,     // [05] Análisis IA: workspace ya cargado (validación documental)
  analisis_dashboard: 12000,     // [05] Análisis IA: dashboard comparativo vs ganador
  chat_ia: 15000,                // [05] Análisis IA: chat contextual con IA
  dashboard_ejecutivo: 14000,    // [06] Dashboard Ejecutivo y ranking competidores
  competidores: 14000,           // [07] Inteligencia de competidores y mercado total
  alertas_expansion: 15000,      // [08] Alertas inteligentes y expansión semántica
  mensajeria_notifs: 12000,      // [09] Mensajería SignalR y centro de eventos
  catalogos_corporativos: 15000, // [10] Catálogos TIVIT: Casos de éxito y certificaciones
  perfil_cierre: 14000           // [11] Perfil, alertas multicanal y cierre
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
 * Escritura suave con efecto humano
 */
async function smoothType(page, selectorOrLocator, text, delay = 45) {
  try {
    const locator = typeof selectorOrLocator === 'string' ? page.locator(selectorOrLocator).first() : selectorOrLocator;
    await locator.waitFor({ state: 'visible', timeout: 10000 });
    await smoothClick(page, locator);
    await page.keyboard.press('Control+A');
    await page.keyboard.press('Backspace');
    await locator.pressSequentially(text, { delay });
    await beat(400);
  } catch (err) {
    console.warn(`[smoothType] Warning: ${err.message}`);
  }
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
      return match.closest('button, a, li, label, [role="tab"], [role="menuitem"], [role="button"], .ant-segmented-item, .ant-tabs-tab, .ant-menu-item') || match;
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

/**
 * Scroll suave para visualización fluida
 */
async function smoothScroll(page, distance = 400, steps = 10) {
  for (let i = 0; i < steps; i++) {
    await page.mouse.wheel(0, distance / steps);
    await beat(60);
  }
}

(async () => {
  if (RECORD_VIDEO && !fs.existsSync(RECORDINGS_DIR)) {
    fs.mkdirSync(RECORDINGS_DIR, { recursive: true });
  }

  console.log('================================================================');
  console.log('  MPM — GRABACIÓN DE DEMO DE FUNCIONALIDADES (PROD)');
  console.log(`  URL Destino: ${BASE_URL}`);
  console.log(`  Grabación de Video: ${RECORD_VIDEO ? 'ACTIVADA (' + RECORDINGS_DIR + ')' : 'DESACTIVADA'}`);
  console.log('================================================================\n');

  const browser = await chromium.launch({
    headless: IS_HEADLESS,
    args: ['--start-maximized', '--disable-infobars'],
  });

  // viewport: null + --start-maximized => la ventana abre MAXIMIZADA y el viewport
  // toma el tamaño real de la pantalla (no uno fijo 1920x1080). El video se graba
  // al tamaño real del viewport.
  const context = await browser.newContext({
    viewport: null,
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
    // [01] PORTADA / LOGIN INSTITUCIONAL
    // ----------------------------------------------------
    console.log('[01] Portada e inicio de sesión institucional');
    await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle' });
    await beat(PAUSES.intro);

    // ----------------------------------------------------
    // [02] LOGIN
    // ----------------------------------------------------
    console.log('[02] Autenticación y roles de usuario');
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
    // [03] LICITACIONES — FILTROS POR ÁREA, ESTADOS Y VISTAS
    // ----------------------------------------------------
    console.log('[03] Consola de Licitaciones: Áreas de negocio, estados y vistas rápidas');
    await page.waitForSelector('.ant-table-row', { timeout: 20000 });
    await beat(1500);

    // Mostrar vistas rápidas: Seguidas y De Interés
    console.log('  -> Mostrando vistas: Seguidas y De Interés');
    await clickByText(page, '.ant-segmented-item, .ant-radio-button-wrapper, span', 'Seguidas');
    await beat(2000);
    await clickByText(page, '.ant-segmented-item, .ant-radio-button-wrapper, span', 'De Interés');
    await beat(2000);
    await clickByText(page, '.ant-segmented-item, .ant-radio-button-wrapper, span', 'Todas');
    await beat(1500);

    // Filtrar por Área de Negocio => "Cloud"
    console.log('  -> Filtrando por Área de negocio: Cloud');
    const areaSelect = page.getByTestId('filter-area-negocio').or(page.locator('.ant-select:has-text("Área de negocio")')).first();
    if (await areaSelect.isVisible()) {
      await smoothClick(page, areaSelect);
      await beat(800);
      const optCloud = page.locator('.ant-select-item-option-content:has-text("Cloud"), .ant-select-item-option:has-text("Cloud")').first();
      if (await optCloud.isVisible()) {
        await smoothClick(page, optCloud);
      } else {
        await clickByText(page, '.ant-select-dropdown div, span', 'Cloud');
      }
      await beat(2000);
      await page.waitForSelector('.ant-table-row', { timeout: 15000 });
    }

    await smoothScroll(page, 250);
    await beat(1500);
    await smoothScroll(page, -250);
    await beat(PAUSES.licitaciones_listado);

    // ----------------------------------------------------
    // [04] BÚSQUEDA SEMÁNTICA CON IA GEMINI
    // ----------------------------------------------------
    console.log('[04] Búsqueda semántica inteligente en lenguaje natural');
    await clickByText(page, '.ant-segmented-item, div, span, button', 'Búsqueda inteligente');
    await beat(1200);
    await smoothType(
      page,
      'input[placeholder*="ciberseguridad"], input[placeholder*="Enter para buscar"]',
      'adquisición de software y equipamiento tecnológico'
    );
    await page.keyboard.press('Enter');
    await beat(PAUSES.busqueda_ia);

    // ----------------------------------------------------
    // [05] ANÁLISIS CON IA — WORKSPACE POST-ADJUDICACIÓN YA CARGADO
    // ----------------------------------------------------
    console.log('[05] Análisis con IA: Workspace de Actas PDF y Validación Documental (ya cargado)');
    await clickByText(page, '.ant-menu-item, a, span', 'Análisis');
    await page.waitForURL('**/analisis**', { timeout: 15000 });
    await beat(2000);
    await smoothScroll(page, 300);
    await beat(PAUSES.analisis_workspace);
    await smoothScroll(page, -300);

    // Abrir un workspace ya analizado (no se sube ni descarga licitación nueva).
    // Se prefiere la licitación de AWS (GANADA por TIVIT, 7ma tarjeta del listado) — las demás
    // son licitaciones perdidas y no conviene mostrarlas en un video de muestra.
    await page.waitForSelector('.mpm-workspace-card', { timeout: 15000 });
    const licitacionObjetivo = 'CONTRATACIÓN DE CRÉDITOS DE NUBE PÚBLICA DE AWS PA';
    // Posición fija: la 7ma tarjeta del grid (índice 6). El texto se usa solo para verificar/log.
    const targetCard = page.locator('.mpm-workspace-card').nth(6);
    const cardTexto = await targetCard.innerText().catch(() => '');
    console.log(`[05] Tarjeta objetivo (7ma): ${cardTexto.split('\n').filter(Boolean).slice(0, 3).join(' | ')}`);
    if (cardTexto.includes(licitacionObjetivo)) {
      console.log('[05] Tarjeta confirmada: CONTRATACIÓN DE CRÉDITOS DE NUBE PÚBLICA DE AWS PA');
    } else {
      console.log('[05] ADVERTENCIA: la 7ma tarjeta no coincide con el texto esperado — revisar si el listado cambió');
    }
    if (await targetCard.count().catch(() => 0) > 0) {
      await smoothClick(page, targetCard);
      await beat(2000);

      const btnDashboard = page.locator('button:has-text("Ver dashboard de resultados")').first();
      if (await btnDashboard.isVisible()) {
        await smoothClick(page, btnDashboard);
        await beat(2000);
      }
      await smoothScroll(page, 400);
      await beat(PAUSES.analisis_dashboard);
      await smoothScroll(page, -400);

      // Chat contextual con IA sobre el análisis
      const chatFab = page.locator('.mpm-chat-fab, button:has(.anticon-message), button[aria-label*="chat"]').first();
      if (await chatFab.isVisible()) {
        await smoothClick(page, chatFab);
        await beat(1500);
        await smoothType(
          page,
          'textarea[placeholder*="Pregunta"], input[placeholder*="Pregunta"]',
          '¿Cuáles fueron los factores de éxito que permitieron ganar esta licitación?'
        );
        await page.keyboard.press('Enter');
        await beat(PAUSES.chat_ia);

        const closeBtn = page.locator('.ant-drawer-close').first();
        if (await closeBtn.isVisible()) {
          await smoothClick(page, closeBtn);
          await beat(800);
        }
      }
    }

    // ----------------------------------------------------
    // [06] DASHBOARD EJECUTIVO Y RANKING DE COMPETIDORES
    // ----------------------------------------------------
    console.log('[06] Dashboard Ejecutivo: Win Rate histórico y ranking de competidores');
    await clickByText(page, '.ant-menu-item, a, span', 'Ejecutivo');
    await page.waitForURL('**/analisis/ejecutivo**', { timeout: 15000 });
    await beat(2500);
    await smoothScroll(page, 450);
    await beat(1500);

    const collapseHeader = page.locator('.ant-collapse-header').first();
    if (await collapseHeader.isVisible()) {
      await smoothClick(page, collapseHeader);
      await beat(2500);
    }
    await clickByText(page, '.ant-tabs-tab, div, span', 'Todas las Licitaciones Analizadas');
    await beat(PAUSES.dashboard_ejecutivo);

    // ----------------------------------------------------
    // [07] INTELIGENCIA DE COMPETIDORES
    // ----------------------------------------------------
    console.log('[07] Módulo de Competidores: Historial de ofertas y actividad total de mercado');
    await clickByText(page, '.ant-menu-item, a, span', 'Competidores');
    await page.waitForURL('**/competidores**', { timeout: 15000 });
    await beat(2000);
    const compInput = page.locator('input[placeholder*="competidor"], .ant-select-selection-search-input').first();
    if (await compInput.isVisible()) {
      await smoothType(page, compInput, 'SONDA');
      await beat(1200);
      const opt = page.locator('.ant-select-item-option-content').first();
      if (await opt.isVisible()) {
        await smoothClick(page, opt);
      } else {
        await page.keyboard.press('Enter');
      }
    }
    await beat(PAUSES.competidores);

    // ----------------------------------------------------
    // [08] ALERTAS INTELIGENTES CON EXPANSIÓN IA
    // ----------------------------------------------------
    console.log('[08] Alertas Inteligentes: Expansión semántica automática de conceptos');
    await clickByText(page, '.ant-menu-item, a, span', 'Alertas');
    await page.waitForURL('**/alertas**', { timeout: 15000 });
    await beat(2000);
    const btnNuevaAlerta = page.locator('button:has-text("Nueva alerta")').first();
    if (await btnNuevaAlerta.isVisible()) {
      await smoothClick(page, btnNuevaAlerta);
      await beat(1000);
      await smoothType(page, 'input[placeholder*="cloud"], input[placeholder*="ej."]', 'ciberseguridad');
      await beat(800);
      await clickByText(page, '.ant-modal-footer button, button', 'Crear');
      await beat(PAUSES.alertas_expansion);
    }

    // ----------------------------------------------------
    // [09] MENSAJERÍA EN TIEMPO REAL Y NOTIFICACIONES
    // ----------------------------------------------------
    console.log('[09] Colaboración en Tiempo Real: Mensajería SignalR y Notificaciones');
    await clickByText(page, '.ant-menu-item, a, span', 'Mensajes');
    await page.waitForURL('**/mensajes**', { timeout: 15000 });
    await beat(2000);
    const convoItem = page.locator('.ant-typography-ellipsis-single-line, .ant-list-item').first();
    if (await convoItem.isVisible()) {
      await smoothClick(page, convoItem);
      await beat(1200);
      await smoothType(page, 'input[placeholder*="Escribe un mensaje"], textarea[placeholder*="mensaje"]', 'Revisando requerimientos técnicos para la propuesta.');
      await page.keyboard.press('Enter');
      await beat(2000);
    }
    await clickByText(page, '.ant-menu-item, a, span', 'Notificaciones');
    await page.waitForURL('**/notificaciones**', { timeout: 15000 });
    await beat(PAUSES.mensajeria_notifs);

    // ----------------------------------------------------
    // [10] CATÁLOGOS CORPORATIVOS TIVIT
    // ----------------------------------------------------
    console.log('[10] Catálogos Corporativos TIVIT: Casos de Éxito, Certificaciones PDF y Plantilla DOCX');
    await clickByText(page, '.ant-menu-item, a, span', 'Catálogos');
    await page.waitForURL('**/catalogos**', { timeout: 15000 });
    await beat(2000);

    // Recorrer pestañas de catálogo corporativo
    console.log('  -> Pestaña 1: Casos de Éxito & Experiencias');
    await clickByText(page, '.ant-tabs-tab', 'Casos de Éxito');
    await beat(2500);
    console.log('  -> Pestaña 2: Certificaciones Corporativas con visor de PDFs');
    await clickByText(page, '.ant-tabs-tab', 'Certificaciones');
    await beat(2500);
    console.log('  -> Pestaña 3: Capítulos de Propuesta DOCX');
    await clickByText(page, '.ant-tabs-tab', 'Capítulos');
    await beat(2500);
    console.log('  -> Pestaña 4: Parámetros Mercado Público');
    await clickByText(page, '.ant-tabs-tab', 'Mercado Público');
    await beat(PAUSES.catalogos_corporativos);

    // ----------------------------------------------------
    // [11] PERFIL DE USUARIO, CONFIGURACIÓN DE ALERTAS Y CIERRE
    // ----------------------------------------------------
    console.log('[11] Perfil de Usuario, Configuración de Alertas (canal de correo) y Cierre institucional');
    const avatar = page.locator('.ant-avatar').first();
    if (await avatar.isVisible()) {
      await smoothClick(page, avatar);
      await beat(800);
      await clickByText(page, '.ant-dropdown-menu-item, div, span', 'Mi perfil');
      await beat(2000);
      await clickByText(page, '.ant-tabs-tab', 'Configuración Alertas');
      await beat(2000);
      await page.keyboard.press('Escape');
      await beat(800);
    }
    await beat(PAUSES.perfil_cierre);

    console.log('\n================================================================');
    console.log('  GRABACIÓN COMPLETADA EXITOSAMENTE');
    console.log('================================================================\n');
  } catch (error) {
    console.error('Error durante la ejecución:', error);
    await page.screenshot({ path: path.resolve(__dirname, 'error_screenshot.png') }).catch(() => {});
    const html = await page.content().catch(() => '');
    const currentUrl = page.url();
    console.log(`URL actual al fallar: ${currentUrl}`);
    const errorText = await page.locator('.ant-alert, .ant-message, [style*="color: rgb(239, 68, 68)"]').allInnerTexts().catch(() => []);
    if (errorText.length > 0) {
      console.log('Mensajes visibles en pantalla:', errorText);
    }
  } finally {
    await context.close();
    await browser.close();

    if (RECORD_VIDEO) {
      console.log(`Video guardado en: ${RECORDINGS_DIR}`);
    }
  }
})();
