import { screenshotOnError, esperarConDelay, reintentar } from './browser.js';

const MP_URL = 'https://www.mercadopublico.cl/Home';

export async function login(page, context) {
  const rut = process.env.MP_RUT;
  const password = process.env.MP_PASSWORD;

  if (!rut || !password) {
    throw new Error('Credenciales no configuradas. Definir MP_RUT y MP_PASSWORD en .env');
  }

  console.log('\n[LOGIN] Iniciando sesion en Mercado Publico...');
  console.log(`[LOGIN] RUT: ${rut}`);

  try {
    // ── Paso 1: Navegar a Mercado Publico ─────────────────────────────
    await reintentar(async () => {
      console.log('[LOGIN] Navegando a Mercado Publico...');
      await page.goto(MP_URL, { waitUntil: 'networkidle', timeout: 45000 });
    });

    await esperarConDelay(2000);

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
    console.log('[LOGIN] Esperando formulario de login (Heimdall)...');
    await page.waitForURL(/heimdall|login|auth/, { timeout: 15000 }).catch(() => {});
    await esperarConDelay(2000);

    const loginUrl = page.url();
    console.log(`[LOGIN] URL de login: ${loginUrl.substring(0, 80)}`);

    // ── Paso 3: Seleccionar tab "Extranjero" ──────────────────────────
    console.log('[LOGIN] Seleccionando tab "Extranjero"...');
    await reintentar(async () => {
      const tabExtranjero = page.locator('#liExtranjero')
        .or(page.locator('li:has-text("Extranjero")'))
        .first();

      await tabExtranjero.waitFor({ state: 'visible', timeout: 10000 });
      await tabExtranjero.click();
      await esperarConDelay(1500);
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
      await esperarConDelay(500);

      const inputPassword = page.locator('#password-re')
        .or(page.locator('input[name="password"][type="password"]').first())
        .first();

      await inputPassword.waitFor({ state: 'visible', timeout: 10000 });
      await inputPassword.click();
      await inputPassword.clear();
      await inputPassword.fill(password);
      await esperarConDelay(500);

      console.log('[LOGIN] Click en "Ingresar Ahora"...');
      const btnSubmit = page.locator('#kc-login-re')
        .or(page.locator('input[name="login"][type="submit"]').first())
        .first();

      await btnSubmit.click();
    });

    console.log('[LOGIN] Esperando respuesta de login...');
    await esperarConDelay(5000);

    // ── Verificar error de login ───────────────────────────────────────
    const errorLogin = await page.evaluate(() => {
      const errEl = document.querySelector('#input-error, .kc-error-message, .error, .alert-danger, [class*="error"]');
      return errEl ? errEl.textContent.trim() : null;
    });

    if (errorLogin && (errorLogin.includes('invalid') || errorLogin.includes('inv') || errorLogin.includes('Incorrect'))) {
      throw new Error(`Error de login: ${errorLogin}`);
    }

    const urlDespuesLogin = page.url();
    const sigueEnLogin = urlDespuesLogin.includes('heimdall') && urlDespuesLogin.includes('auth');

    if (sigueEnLogin && !errorLogin) {
      const bodyText = await page.evaluate(() => document.body.innerText.substring(0, 500));
      if (bodyText.includes('inv') || bodyText.includes('incorrect') || bodyText.includes('Invalid')) {
        throw new Error('Credenciales invalidas - verifique MP_RUT y MP_PASSWORD en .env');
      }
    }

    // ── Paso 5: Seleccionar organizacion (si aparece) ─────────────────
    console.log('[LOGIN] Verificando selector de organizacion...');
    const seleccionoOrg = await seleccionarOrganizacion(page);
    if (!seleccionoOrg) {
      console.log('[LOGIN] No se detecto selector de organizacion. Verificando si ya estamos dentro...');
      const currentUrl = page.url();
      if (currentUrl.includes('mercadopublico.cl') && !currentUrl.includes('heimdall') && !currentUrl.includes('auth')) {
        console.log('[LOGIN] Estamos en Mercado Publico - login exitoso sin selector de org');
      } else {
        console.log('[LOGIN] ADVERTENCIA: Podria ser necesario seleccionar organizacion manualmente');
      }
    }

    console.log('[LOGIN] Login completado!');
    return true;
  } catch (e) {
    console.log(`[LOGIN] ERROR: ${e.message}`);
    const carpeta = process.env.MP_CARPETA_SALIDA || './descargas';
    await screenshotOnError(page, carpeta, 'login-error');
    throw e;
  }
}

async function seleccionarOrganizacion(page) {
  console.log('[LOGIN] Buscando selector de organizacion...');

  await esperarConDelay(3000);

  const pageText = await page.evaluate(() => document.body.innerText || '').catch(() => '');

  if (pageText.includes('Hemos encontrado') || pageText.includes('entidades asociadas') ||
      (pageText.includes('elegir') && pageText.includes('Documento'))) {
    console.log('[LOGIN] Pagina de seleccion de organizacion detectada');
  } else if (!pageText.includes('TIVIT') && !pageText.includes('organizacion') &&
             !pageText.includes('empresa') && !pageText.includes('selecciona')) {
    const currentUrl = page.url();
    const estamosEnHome = (currentUrl.includes('/Home') || currentUrl.includes('/Menu') ||
      currentUrl.includes('/Portal') || currentUrl.includes('Procurement')) &&
      !currentUrl.includes('heimdall') && !currentUrl.includes('auth');

    if (estamosEnHome) {
      console.log('[LOGIN] Ya estamos en Mercado Publico - sin selector de organizacion');
      return true;
    }

    console.log('[LOGIN] No se detecto pagina de seleccion de organizacion');
    return false;
  }

  console.log('[LOGIN] Seleccionando organizacion TIVIT...');

  await esperarConDelay(1000);

  const cerrarPopup = page.locator('#cerrarPopupDatosContacto, #btnDjsCerrar').first();
  if (await cerrarPopup.isVisible({ timeout: 2000 }).catch(() => false)) {
    console.log('[LOGIN] Cerrando popup de datos de contacto...');
    await cerrarPopup.click();
    await esperarConDelay(1000);
  }

  const radioOrg = page.locator('input[id*="rdbOrg"]').first()
    .or(page.locator('input[type="radio"]').filter({ hasText: /TIVIT/i }).first())
    .or(page.locator('tr:has-text("TIVIT") input[type="radio"]').first())
    .or(page.locator('td:has-text("TIVIT")').first());

  const radioCount = await page.locator('input[type="radio"]').count();
  console.log(`[LOGIN] Radios encontrados: ${radioCount}`);

  if (await radioOrg.count() > 0) {
    console.log('[LOGIN] Click en radio de TIVIT...');
    await radioOrg.first().click();
    await esperarConDelay(1500);
  } else {
    const todosElementos = page.locator('tr, td, div, li, label, span');
    const cantidad = await todosElementos.count();
    for (let i = 0; i < Math.min(cantidad, 100); i++) {
      const texto = await todosElementos.nth(i).textContent().catch(() => '');
      if (texto && texto.includes('TIVIT')) {
        const radio = todosElementos.nth(i).locator('input[type="radio"]').first();
        if (await radio.count() > 0) {
          await radio.click();
          console.log('[LOGIN] Organizacion TIVIT seleccionada');
          break;
        }
      }
    }
  }

  await esperarConDelay(1000);

  console.log('[LOGIN] Click en boton/link para ingresar...');
  const btnIngresar = page.locator('a:has-text("Ingresar")')
    .or(page.locator('input[value*="Ingresar"], input[value*="ingresar"], button:has-text("Ingresar"), input[type="submit"]'))
    .or(page.locator('#kc-login-re, .btn-primary, .btn.btn-primary'))
    .first();

  if (await btnIngresar.count() > 0 && await btnIngresar.isVisible({ timeout: 3000 }).catch(() => false)) {
    await btnIngresar.click();
    await esperarConDelay(5000);
    console.log('[LOGIN] Redirigiendo despues de seleccionar organizacion...');
  } else {
    console.log('[LOGIN] No se encontro boton Ingresar, verificando navegacion...');
  }

  await page.waitForURL(/mercadopublico\.cl/, { timeout: 15000 }).catch(() => {
    console.log('[LOGIN] No se detecto redireccion...');
  });

  await esperarConDelay(2000);

  const urlFinal = page.url();
  console.log(`[LOGIN] URL final: ${urlFinal.substring(0, 80)}`);

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