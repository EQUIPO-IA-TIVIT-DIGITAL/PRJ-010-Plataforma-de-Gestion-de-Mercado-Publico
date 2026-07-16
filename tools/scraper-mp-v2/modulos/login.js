import { screenshotOnError, esperarConDelay, reintentar } from './browser.js';

const MP_URL = 'https://www.mercadopublico.cl/Home';
const MENU_URL = 'https://www.mercadopublico.cl/Portal/Modules/Menu/Menu.aspx';

// Verificado en vivo el 2026-07-15: la unica senal fiable de sesion autenticada es el link
// "Cerrar sesion" (#lnkEndSession) presente en Menu.aspx. Las heuristicas por URL fallan
// porque el selector de organizacion es un MODAL sobre /Home (URL ya sin "heimdall"), y una
// pagina intermedia o bloqueada tambien pasa el filtro de URL.
const SELECTOR_SESION_ACTIVA = '#lnkEndSession';

export async function login(page, context) {
  const rut = process.env.MP_RUT;
  const password = process.env.MP_PASSWORD;

  if (!rut || !password) {
    throw new Error('Credenciales no configuradas. Definir MP_RUT y MP_PASSWORD en .env');
  }

  console.log('\n[LOGIN] Iniciando sesion en Mercado Publico...');
  console.log(`[LOGIN] RUT: ${rut}`);

  try {
    // ── Paso 0: Verificar si la sesion previa (cargada via storageState) sigue activa ──
    console.log('[LOGIN] Verificando si la sesion ya esta activa...');
    if (await sesionAutenticada(page)) {
      console.log('[LOGIN] Sesion valida reutilizada (evitando login de Keycloak)!');
      await cerrarPopupsMP(page);
      return true;
    }
    console.log('[LOGIN] Sesion no activa o expirada, procediendo con login completo...');

    // ── Paso 1: Navegar a Mercado Publico ─────────────────────────────
    await reintentar(async () => {
      console.log('[LOGIN] Navegando a Mercado Publico...');
      await page.goto(MP_URL, { waitUntil: 'networkidle', timeout: 45000 });
    });

    await verificarBloqueoRobot(page, 'home');

    // ── Paso 2: Click "Iniciar Sesion" ────────────────────────────────
    console.log('[LOGIN] Buscando boton "Iniciar Sesion"...');
    await reintentar(async () => {
      const btnLogin = page.locator('button.btn.btn-xl.btn-pri')
        .or(page.getByRole('button', { name: /iniciar sesi/i }))
        .first();

      await btnLogin.waitFor({ state: 'visible', timeout: 10000 });
      await btnLogin.click();
    });

    // ── Esperar redireccion a Heimdall (Keycloak) ─────────────────────
    // Si la redireccion no ocurre, es un fallo real (antes se tragaba el error con
    // .catch(()=>{}) y el flujo seguia sobre /Home, fallando mas adelante sin pista).
    console.log('[LOGIN] Esperando formulario de login (Heimdall)...');
    await page.waitForURL(/heimdall|login|auth/i, { timeout: 20000 });

    const loginUrl = page.url();
    console.log(`[LOGIN] URL de login: ${loginUrl.substring(0, 80)}`);

    await verificarBloqueoRobot(page, 'heimdall');

    // ── Paso 3: Seleccionar tab "Extranjero" ──────────────────────────
    // Los inputs #username-re/#password-re existen en el DOM desde el inicio pero quedan
    // ocultos hasta activar este tab (verificado en vivo 2026-07-15).
    console.log('[LOGIN] Seleccionando tab "Extranjero"...');
    await reintentar(async () => {
      const tabExtranjero = page.locator('#liExtranjero')
        .or(page.locator('li:has-text("Extranjero")'))
        .first();

      await tabExtranjero.waitFor({ state: 'visible', timeout: 10000 });
      await tabExtranjero.click();
    });

    // ── Paso 4: Ingresar credenciales ─────────────────────────────────
    console.log('[LOGIN] Ingresando credenciales...');
    await reintentar(async () => {
      const inputUsername = page.locator('#username-re')
        .or(page.locator('input[name="username"]').first())
        .first();

      await inputUsername.waitFor({ state: 'visible', timeout: 10000 });
      await inputUsername.click();
      await inputUsername.clear();
      await inputUsername.fill(rut);

      const inputPassword = page.locator('#password-re')
        .or(page.locator('input[name="password"][type="password"]').first())
        .first();

      await inputPassword.waitFor({ state: 'visible', timeout: 10000 });
      await inputPassword.click();
      await inputPassword.clear();
      await inputPassword.fill(password);

      console.log('[LOGIN] Click en "Ingresar Ahora"...');
      const btnSubmit = page.locator('#kc-login-re')
        .or(page.locator('input[name="login"][type="submit"]').first())
        .first();

      await btnSubmit.click();
    });

    // Esperar a salir de Heimdall O a que aparezca un mensaje de error de Keycloak,
    // lo que ocurra primero (en vez de un sleep fijo de 5s).
    console.log('[LOGIN] Esperando respuesta de login...');
    await Promise.race([
      page.waitForURL(u => !/heimdall/i.test(u.toString()), { timeout: 25000 }),
      page.waitForSelector('#input-error, .kc-error-message, .alert-danger, .alert-error, span[class*="kc-feedback"]', { timeout: 25000 }),
    ]).catch(() => { /* se resuelve abajo con la verificacion explicita */ });

    // ── Verificar error de login (Keycloak responde en espanol) ────────
    const errorLogin = await detectarErrorKeycloak(page);
    if (errorLogin) {
      const err = new Error(`Credenciales rechazadas por Keycloak: "${errorLogin}" - verifique MP_RUT y MP_PASSWORD en .env`);
      err.isLoginFallido = true;
      throw err;
    }

    if (/heimdall/i.test(page.url())) {
      // Seguimos en Keycloak sin mensaje de error visible: pagina atascada o captcha.
      await verificarBloqueoRobot(page, 'post-submit');
      const err = new Error('El login no salio de Heimdall y no hay mensaje de error visible (pagina atascada o intersticial)');
      err.isLoginFallido = true;
      throw err;
    }

    // ── Paso 5: Seleccionar organizacion (modal sobre /Home) ──────────
    console.log('[LOGIN] Verificando selector de organizacion...');
    await seleccionarOrganizacion(page);

    // ── Paso 6: Asercion positiva de sesion ───────────────────────────
    // No basta con "la URL parece correcta": exigimos el elemento autenticado real.
    console.log('[LOGIN] Verificando sesion autenticada (asercion positiva)...');
    if (!(await sesionAutenticada(page))) {
      await verificarBloqueoRobot(page, 'post-login');
      const err = new Error(`Login sin sesion autenticada: no aparece ${SELECTOR_SESION_ACTIVA} en Menu.aspx (URL actual: ${page.url().substring(0, 80)})`);
      err.isLoginFallido = true;
      throw err;
    }

    await cerrarPopupsMP(page);

    console.log('[LOGIN] Login completado y verificado!');
    return true;
  } catch (e) {
    console.log(`[LOGIN] ERROR: ${e.message}`);
    const carpeta = process.env.MP_CARPETA_SALIDA || './descargas';
    await screenshotOnError(page, carpeta, 'login-error');
    throw e;
  }
}

/**
 * Asercion positiva de sesion: navega a Menu.aspx y exige el link "Cerrar sesion".
 * Devuelve true/false sin lanzar (el llamador decide si es fatal).
 */
async function sesionAutenticada(page) {
  try {
    if (!page.url().includes('/Portal/Modules/Menu/Menu.aspx')) {
      await page.goto(MENU_URL, { waitUntil: 'domcontentloaded', timeout: 20000 });
    }
    const currentUrl = page.url();
    console.log(`[LOGIN] URL de verificacion: ${currentUrl.substring(0, 80)}`);
    if (/heimdall|Login/i.test(currentUrl)) {
      return false;
    }
    const cerrarSesion = page.locator(SELECTOR_SESION_ACTIVA);
    await cerrarSesion.waitFor({ state: 'visible', timeout: 10000 });
    return true;
  } catch (e) {
    console.log(`[LOGIN] Sesion no verificable: ${e.message.split('\n')[0]}`);
    return false;
  }
}

/**
 * Deteccion de bloqueo anti-robot/captcha, mismo patron que adjuntos.js (isRobotBlock):
 * imagen robot.png, texto "acceso denegado", o iframe de reCAPTCHA visible.
 * Lanza con e.isRobotBlock = true para que el orquestador corte sin reintentar en caliente.
 */
async function verificarBloqueoRobot(page, contexto) {
  const senales = await page.evaluate(() => {
    const robotImg = !!document.querySelector('img[src*="robot"]');
    const texto = (document.body?.innerText || '').toLowerCase();
    const accesoDenegado = texto.includes('acceso denegado') || texto.includes('access denied');
    const captchaVisible = [...document.querySelectorAll('iframe[src*="recaptcha"], iframe[src*="hcaptcha"]')]
      .some(f => f.offsetParent !== null);
    return { robotImg, accesoDenegado, captchaVisible };
  }).catch(() => ({ robotImg: false, accesoDenegado: false, captchaVisible: false }));

  if (senales.robotImg || senales.accesoDenegado || senales.captchaVisible) {
    const detalle = Object.entries(senales).filter(([, v]) => v).map(([k]) => k).join(', ');
    const err = new Error(`Bloqueo anti-robot detectado durante login (${contexto}): ${detalle}`);
    err.isRobotBlock = true;
    throw err;
  }
}

/**
 * Lee el mensaje de error de Keycloak si existe. Los mensajes reales de Mercado Publico
 * vienen en espanol ("inv", "incorrect" no matcheaban nunca) -- ahora basta con que el
 * contenedor de error de Keycloak tenga texto visible.
 */
async function detectarErrorKeycloak(page) {
  return await page.evaluate(() => {
    const contenedores = document.querySelectorAll(
      '#input-error, .kc-error-message, .alert-danger, .alert-error, span[class*="kc-feedback"], .kc-feedback-text'
    );
    for (const el of contenedores) {
      const texto = (el.textContent || '').trim();
      if (texto && el.offsetParent !== null) return texto.substring(0, 200);
    }
    return null;
  }).catch(() => null);
}

/**
 * Seleccion de organizacion. DOM real verificado en vivo el 2026-07-15:
 *  - Es un modal sobre /Home con texto "Hemos encontrado N entidades asociadas..."
 *  - Radio: <input type="radio" name="grupoOrg" id="rdbOrg{id}" value="{id}">
 *  - Boton: <a class="btn btn-pri">Ingresar</a> (sin id)
 * Si el modal no aparece (cuenta con una sola entidad auto-seleccionada), no es error:
 * la asercion positiva del Paso 6 decide si la sesion quedo activa.
 */
async function seleccionarOrganizacion(page) {
  console.log('[LOGIN] Buscando modal de seleccion de organizacion...');

  const radioOrg = page.locator('input[type="radio"][name="grupoOrg"]')
    .or(page.locator('input[id*="rdbOrg"]'))
    .first();

  const aparecio = await radioOrg.waitFor({ state: 'visible', timeout: 15000 })
    .then(() => true)
    .catch(() => false);

  if (!aparecio) {
    console.log('[LOGIN] No aparecio el modal de organizacion (puede que la cuenta entre directo)');
    return false;
  }

  const radios = page.locator('input[type="radio"][name="grupoOrg"], input[id*="rdbOrg"]');
  const cantidad = await radios.count();
  console.log(`[LOGIN] Modal de organizacion detectado (${cantidad} entidades)`);

  if (cantidad === 1) {
    await radios.first().click();
  } else {
    // Varias entidades: elegir la fila cuyo texto contenga MP_ORGANIZACION (default TIVIT)
    const nombreOrg = process.env.MP_ORGANIZACION || 'TIVIT';
    const radioDeLaOrg = page.locator(`tr:has-text("${nombreOrg}") input[type="radio"]`).first();
    if (await radioDeLaOrg.count() > 0) {
      await radioDeLaOrg.click();
      console.log(`[LOGIN] Organizacion "${nombreOrg}" seleccionada`);
    } else {
      console.log(`[LOGIN] ADVERTENCIA: ninguna fila contiene "${nombreOrg}", seleccionando la primera entidad`);
      await radios.first().click();
    }
  }

  console.log('[LOGIN] Click en boton "Ingresar" del modal...');
  const btnIngresar = page.locator('a.btn.btn-pri:has-text("Ingresar")')
    .or(page.locator('a:has-text("Ingresar"), button:has-text("Ingresar"), input[value*="Ingresar"]'))
    .first();
  await btnIngresar.waitFor({ state: 'visible', timeout: 10000 });
  await btnIngresar.click();

  // El ingreso redirige a Menu.aspx; esperar la navegacion real, no un sleep.
  await page.waitForURL(/Menu\.aspx|Portal/i, { timeout: 25000 }).catch(() => {
    console.log('[LOGIN] No se detecto redireccion a Menu.aspx tras elegir organizacion (la asercion final decidira)');
  });

  await cerrarPopupsMP(page);
  return true;
}

async function cerrarPopupsMP(page) {
  try {
    const popups = [
      '#cerrarPopupDatosContacto',
      '#btnDjsCerrar',
      'button:has-text("Cerrar")',
      'a:has-text("Cerrar")',
      '.close',
      '[data-dismiss="modal"]',
    ];
    for (const sel of popups) {
      const btn = page.locator(sel).first();
      if (await btn.isVisible({ timeout: 1000 }).catch(() => false)) {
        console.log(`[LOGIN] Cerrando popup: ${sel}`);
        await btn.click().catch(() => {});
        await esperarConDelay(500);
      }
    }
  } catch (e) {
    // no hacer nada si no hay popups
  }
}
