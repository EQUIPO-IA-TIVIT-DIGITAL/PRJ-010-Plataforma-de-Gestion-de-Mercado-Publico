/**
 * Demo automatizada de MPM (Mercado Público Management) con Puppeteer.
 * Reproduce en Chrome "limpio" (sin overlay de control) el recorrido mapeado
 * manualmente contra el entorno de prueba en producción.
 *
 * Uso:
 *   cd "scripts/demo"
 *   npm install puppeteer
 *   node demo-mpm.js
 *
 * Mientras corre, grabar la pantalla (ventana de Chrome que abre el script).
 * El guion narrado sincronizado está en guion-demo-mpm.md — cada paso lleva
 * el mismo número [n] en ambos archivos para facilitar el doblaje con la voz TTS.
 */

const puppeteer = require('puppeteer');

const BASE_URL = 'https://mpm-web-6nnd6y6owa-uc.a.run.app';
const EMAIL = 'admin@tivit.cl';
const PASSWORD = 'test123';

// Pausa "humana" entre acciones para que la grabación se vea natural.
const beat = (ms = 1500) => new Promise((r) => setTimeout(r, ms));

// Puppeteer mueve un mouse "real" a nivel de eventos del navegador (por eso
// Chrome dispara mousemove/mousedown/mouseup normales), pero no existe un
// cursor visual en pantalla — es invisible en la grabación. Este helper
// inyecta un círculo que sigue esos eventos reales y "pulsa" al hacer clic,
// para que se vea dónde se está interactuando. Se registra con
// evaluateOnNewDocument para sobrevivir a la única navegación completa del
// script (login); el resto de la demo es ruteo SPA del lado del cliente, que
// no recarga el documento y por lo tanto no borra la inyección.
async function injectFakeCursor(page) {
  await page.evaluateOnNewDocument(() => {
    const CURSOR_ID = '__mpm_demo_cursor__';
    function init() {
      if (document.getElementById(CURSOR_ID)) return;
      const cursor = document.createElement('div');
      cursor.id = CURSOR_ID;
      cursor.style.cssText = `
        position: fixed; top: 0; left: 0;
        width: 22px; height: 22px;
        border-radius: 50%;
        background: rgba(255, 59, 48, 0.35);
        border: 2px solid rgba(255, 59, 48, 0.95);
        box-shadow: 0 0 6px rgba(0,0,0,0.35);
        pointer-events: none;
        z-index: 2147483647;
        transform: translate(-50%, -50%);
        transition: left 0.25s ease, top 0.25s ease;
      `;
      document.documentElement.appendChild(cursor);

      window.addEventListener(
        'mousemove',
        (e) => {
          cursor.style.left = e.clientX + 'px';
          cursor.style.top = e.clientY + 'px';
        },
        true
      );
      window.addEventListener(
        'mousedown',
        () => {
          cursor.style.transitionDuration = '0.25s, 0.25s, 0.1s, 0.1s';
          cursor.style.transform = 'translate(-50%, -50%) scale(0.7)';
          cursor.style.background = 'rgba(255, 59, 48, 0.65)';
        },
        true
      );
      window.addEventListener(
        'mouseup',
        () => {
          cursor.style.transform = 'translate(-50%, -50%) scale(1)';
          cursor.style.background = 'rgba(255, 59, 48, 0.35)';
        },
        true
      );
    }
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', init);
    } else {
      init();
    }
  });
}

// Duración estimada (en ms) de cada bloque de narración de guion-demo-mpm.md,
// calculada a ~150 palabras/minuto (ritmo típico de una voz TTS en español).
// Se usan como pausa post-acción para que la voz alcance a terminar de hablar
// antes de pasar al siguiente paso. Si cambia el guion, recalcular acá.
const NARRATION_MS = {
  intro: 15000, // "Esta es la Plataforma de Gestión de Mercado Público de TIVIT..." (~37 palabras)
  1: 4000, // "Empezamos iniciando sesión..." (~8 palabras)
  2: 15000, // "Esta es la pantalla principal..." (~36 palabras)
  3: 9000, // "Al hacer clic en cualquier licitación..." (~22 palabras)
  4: 9000, // "Con un clic en la estrella..." (~21 palabras)
  5: 16000, // "Además del filtro tradicional... 'adquisición de software y equipamiento tecnológico'..." (~39 palabras)
  6: 12000, // "Este es el dashboard ejecutivo..." (~30 palabras)
  '6b': 14000, // "Más abajo está el ranking de competidores..." (~35 palabras)
  '6c': 8000, // "Y en esta otra pestaña..." (~19 palabras)
  7: 9000, // "Acá está el módulo de análisis..." (~23 palabras)
  8: 7000, // "Dentro de cada workspace..." (~17 palabras)
  9: 12000, // "Y este es el resultado del análisis..." (~29 palabras)
  10: 15000, // "También se puede conversar..." (~35 palabras) + espera real de Gemini
  11: 8000, // "La plataforma incluye mensajería interna..." (~19 palabras)
  12: 10000, // "El centro de notificaciones agrupa..." (~24 palabras)
  13: 20000, // "Y esto es lo más particular del sistema..." (~55 palabras)
  14: 8000, // "Por último, el módulo de catálogos..." (~19 palabras)
  cierre: 13000, // "Eso es la Plataforma de Gestión de Mercado Público..." (~32 palabras)
};

// Helper: click en el primer elemento visible cuyo texto contenga `text`.
// AntD suele poner el texto en un <span> hijo mientras el manejador de click
// vive en un ancestro (<li>, <label>, [role="tab"], etc.) — subimos hasta el
// ancestro clickeable más cercano antes de disparar el click.
async function clickByText(page, selector, text) {
  const handle = await page.evaluateHandle(
    (sel, txt) => {
      const candidates = Array.from(document.querySelectorAll(sel)).filter(
        (el) => el.textContent.trim().includes(txt) && el.offsetParent !== null
      );
      // querySelectorAll devuelve en orden de documento (padres antes que hijos).
      // Buscamos el match más específico: el que no contiene a ningún otro match
      // (evita quedarnos con un <div> gigante que envuelve toda la página).
      const match = candidates.find(
        (el) => !candidates.some((other) => other !== el && el.contains(other))
      );
      if (!match) return null;
      const clickable = match.closest(
        'button, a, li, label, [role="tab"], [role="menuitem"], [role="button"], .ant-segmented-item, .ant-tabs-tab, .ant-menu-item'
      );
      return clickable || match;
    },
    selector,
    text
  );
  const el = handle.asElement();
  if (!el) throw new Error(`No se encontró "${text}" con selector "${selector}"`);
  await el.click();
  return el;
}

async function typeInPlaceholder(page, placeholder, text) {
  const selector = `input[placeholder="${placeholder}"], textarea[placeholder="${placeholder}"]`;
  await page.waitForSelector(selector, { visible: true, timeout: 15000 });
  await page.click(selector);
  await page.type(selector, text, { delay: 40 });
}

(async () => {
  const browser = await puppeteer.launch({
    headless: false,
    defaultViewport: null,
    args: ['--start-maximized'],
  });
  const [page] = await browser.pages();
  await injectFakeCursor(page);

  // [Intro] Pantalla de login — deja tiempo para la frase de apertura del guion.
  await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle2' });
  await beat(NARRATION_MS.intro);

  // [1] Login
  await typeInPlaceholder(page, 'correo@empresa.com', EMAIL);
  await typeInPlaceholder(page, '••••••••', PASSWORD);
  await clickByText(page, 'button', 'Ingresar');
  await page.waitForNavigation({ waitUntil: 'networkidle2' });
  await beat(NARRATION_MS[1]);

  // [2] Licitaciones — listado general
  await page.waitForFunction(
    () => document.body.innerText.includes('licitaciones'),
    { timeout: 20000 }
  );
  await beat(NARRATION_MS[2]);

  // [3] Abrir el detalle de la primera licitación de la tabla
  await page.waitForSelector('tbody tr, .ant-table-row', { timeout: 15000 });
  await page.click('tbody tr:first-child, .ant-table-row:first-child');
  await beat(NARRATION_MS[3]);

  // Cerrar el detalle (botón "close"/X del drawer, esquina superior del panel)
  await page.keyboard.press('Escape');
  await beat(800);

  // [4] Seguir una licitación (icono estrella en la primera fila)
  const starSelector = '.ant-table-row:first-child [aria-label*="star"], .ant-table-row:first-child .anticon-star';
  try {
    await page.click(starSelector);
    await beat(NARRATION_MS[4]);
  } catch {
    /* si el selector de estrella cambia, se omite este paso sin romper la demo */
  }

  // [5] Búsqueda inteligente en lenguaje natural
  await clickByText(page, 'div, span, button', 'Búsqueda inteligente');
  await beat(800);
  await typeInPlaceholder(
    page,
    'Ej: ciberseguridad para el sector salud, mayores a 10 millones... (Enter para buscar)',
    'adquisición de software y equipamiento tecnológico'
  );
  await page.keyboard.press('Enter');
  await beat(NARRATION_MS[5]);

  // [6] Dashboard Ejecutivo — KPIs
  await clickByText(page, 'a, span, li', 'Ejecutivo');
  await page.waitForFunction(() => document.body.innerText.includes('Dashboard Ejecutivo'), { timeout: 15000 });
  await beat(NARRATION_MS[6]);

  // [6b] Bajar hasta la tabla de ranking y expandir el primer competidor
  // para mostrar el detalle de licitaciones ganadas/perdidas contra él.
  try {
    await page.waitForSelector('.ant-collapse-header', { visible: true, timeout: 10000 });
    await page.evaluate(() => {
      document.querySelector('.ant-collapse-header')?.scrollIntoView({ block: 'center', behavior: 'smooth' });
    });
    await beat(1200);
    await page.click('.ant-collapse-header');
    await beat(NARRATION_MS['6b']); // mostrar la lista de licitaciones ganadas/perdidas contra ese competidor
  } catch {
    /* si el ranking cambia de componente, se omite este paso sin romper la demo */
  }

  // [6c] Pestaña "Todas las Licitaciones Analizadas"
  try {
    await clickByText(page, 'div, span', 'Todas las Licitaciones Analizadas');
    await beat(NARRATION_MS['6c']);
  } catch {}

  // [7] Análisis — listado de workspaces
  await clickByText(page, 'a, span, li', 'Análisis');
  await page.waitForFunction(() => document.body.innerText.includes('Análisis de Licitaciones'), { timeout: 15000 });
  await beat(NARRATION_MS[7]);

  // [8] Abrir un workspace completado
  await clickByText(page, 'button', 'Ver análisis');
  await beat(NARRATION_MS[8]);

  // [9] Ver dashboard de resultados IA
  await clickByText(page, 'button', 'Ver dashboard de resultados');
  await page.waitForFunction(() => document.body.innerText.includes('Resumen comparativo'), { timeout: 15000 });
  await beat(NARRATION_MS[9]);

  // [10] Abrir el chat contextual con IA y hacer una pregunta
  // El botón flotante es un <button class="ant-float-btn mpm-chat-fab">, sin
  // aria-label ni title — hay que apuntarle por su clase propia del proyecto.
  // No usamos los chips de "pregunta sugerida": solo aparecen si el chat del
  // workspace está vacío, y como esta demo se corre varias veces sobre el
  // mismo workspace, el historial ya tiene mensajes previos y los chips no
  // se muestran. Escribimos directo en el textarea, que siempre existe.
  try {
    await page.waitForSelector('.mpm-chat-fab', { visible: true, timeout: 10000 });
    await page.click('.mpm-chat-fab');
    await beat(1200);
    await typeInPlaceholder(page, 'Pregunta sobre el análisis...', '¿Cuál fue el factor más importante de la pérdida?');
    await page.keyboard.press('Enter');
    await beat(NARRATION_MS[10]); // narración + espera real de la respuesta de Gemini
  } catch {
    /* si el fab de chat cambia de clase en un futuro rediseño, se omite sin romper la demo */
  }

  // Cerrar el drawer del chat explícitamente: su mask (fondo oscuro) queda
  // por encima del menú lateral y, si sigue abierto, intercepta el próximo
  // clic de navegación (el clic solo cierra el drawer en vez de navegar).
  try {
    await page.click('.ant-drawer-close');
    await beat(800);
  } catch {
    /* si no hay drawer abierto (el paso [10] falló antes), no hay nada que cerrar */
  }

  // [11] Mensajería — abrir una conversación y enviar un mensaje
  await clickByText(page, 'a, span, li', 'Mensajes');
  await page.waitForSelector('.ant-typography-ellipsis-single-line', { visible: true, timeout: 15000 });
  await beat(1500);
  await page.click('.ant-typography-ellipsis-single-line');
  await beat(1500);
  await typeInPlaceholder(page, 'Escribe un mensaje… (Enter para enviar)', 'Demo: mensaje de prueba');
  await page.keyboard.press('Enter');
  await beat(NARRATION_MS[11]);

  // [12] Notificaciones
  await clickByText(page, 'a, span, li', 'Notificaciones');
  await page.waitForFunction(() => document.body.innerText.includes('Notificaciones'), { timeout: 15000 });
  await beat(NARRATION_MS[12]);

  // [13] Alertas — crear una alerta nueva y mostrar la expansión IA de sinónimos
  await clickByText(page, 'a, span, li', 'Alertas');
  await page.waitForFunction(() => document.body.innerText.includes('Alertas Inteligentes'), { timeout: 15000 });
  await beat(1500);
  await clickByText(page, 'button', 'Nueva alerta');
  await beat(1000);
  await typeInPlaceholder(page, 'ej. cloud, SOC, data center', 'ciberseguridad');
  await clickByText(page, 'button', 'Crear');
  await beat(NARRATION_MS[13]); // narración + espera real de la expansión de sinónimos por IA

  // [14] Catálogos — datos de referencia
  await clickByText(page, 'a, span, li', 'Catálogos');
  await page.waitForFunction(() => document.body.innerText.includes('Catálogos'), { timeout: 15000 });
  await beat(NARRATION_MS[14]);

  // [Cierre] — dejar la ventana abierta para el cierre de la grabación
  await beat(NARRATION_MS.cierre);
  console.log('Demo completa. Cierra la ventana de Chrome cuando termines de grabar.');
})();
