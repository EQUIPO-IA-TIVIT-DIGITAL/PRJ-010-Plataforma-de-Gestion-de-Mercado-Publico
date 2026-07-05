import { screenshotOnError, esperarConDelay, reintentar } from './browser.js';

const SEARCH_URL = 'https://www.mercadopublico.cl/BID/Modules/RFB/NEwSearchProcurement.aspx';

export async function buscarLicitaciones(page, context) {
  console.log('\n[BUSQUEDA] Navegando a busqueda de licitaciones...');

  try {
    await cerrarPopups(page);

    await reintentar(async () => {
      console.log('[BUSQUEDA] Navegando a pagina de busqueda...');
      await page.goto(SEARCH_URL, { waitUntil: 'networkidle', timeout: 45000 });
    });

    await esperarConDelay(3000);

    await cerrarPopups(page);

    await esperarConDelay(1000);

    console.log('[BUSQUEDA] Verificando que la pagina de busqueda cargo correctamente...');
    const cboRegionVisible = await page.locator('#cboRegion').isVisible({ timeout: 5000 }).catch(() => false);
    const cboStateVisible = await page.locator('#cboState').isVisible({ timeout: 3000 }).catch(() => false);

    if (!cboRegionVisible || !cboStateVisible) {
      console.log('[BUSQUEDA] Filtros no visibles, recargando pagina...');
      await page.reload({ waitUntil: 'networkidle', timeout: 45000 }).catch(() => {});
      await esperarConDelay(3000);
      await cerrarPopups(page);
      await esperarConDelay(1000);
    }

    console.log('[BUSQUEDA] Configurando filtros...');
    await configurarFiltros(page);

    console.log('[BUSQUEDA] Ejecutando busqueda...');
    await ejecutarBusqueda(page);

    console.log('[BUSQUEDA] Extrayendo resultados...');
    const licitaciones = await extraerResultados(page, context);

    console.log(`[BUSQUEDA] ${licitaciones.length} licitaciones encontradas`);
    return licitaciones;

  } catch (e) {
    console.log(`[BUSQUEDA] ERROR: ${e.message}`);
    const carpeta = process.env.MP_CARPETA_SALIDA || './descargas';
    await screenshotOnError(page, carpeta, 'busqueda-error');
    throw e;
  }
}

async function cerrarPopups(page) {
  try {
    const cerrarBtn = page.locator('#cerrarPopupDatosContacto, #btnDjsCerrar, button:has-text("Cerrar")').first();
    if (await cerrarBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      console.log('[BUSQUEDA] Cerrando popup...');
      await cerrarBtn.click();
      await esperarConDelay(1000);
    }

    const mantenerBtn = page.locator('#btnCancelarCierre');
    if (await mantenerBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await mantenerBtn.click();
      await esperarConDelay(1000);
    }
  } catch (e) {
    // No hacer nada si no hay popup
  }
}

async function configurarFiltros(page) {
  const fechaDesde = process.env.MP_FECHA_DESDE || '01-01-2026';
  const hoy = new Date();
  const dia = String(hoy.getDate()).padStart(2, '0');
  const mes = String(hoy.getMonth() + 1).padStart(2, '0');
  const anio = hoy.getFullYear();
  const fechaHasta = `${dia}-${mes}-${anio}`;

  console.log(`[BUSQUEDA] Filtros: Region=Todas, Estado=Adjudicada, Desde=${fechaDesde}, Hasta=${fechaHasta}`);

  await reintentar(async () => {
    console.log('[BUSQUEDA] Seleccionando "Todas las Regiones"...');
    const selectRegion = page.locator('#cboRegion');
    await selectRegion.waitFor({ state: 'visible', timeout: 15000 });
    await selectRegion.selectOption(' ');
    console.log('[BUSQUEDA] Region "Todas" seleccionada');
  });

  await esperarConDelay(500);

  await reintentar(async () => {
    console.log('[BUSQUEDA] Seleccionando "Adjudicada"...');
    const selectEstado = page.locator('#cboState');
    await selectEstado.waitFor({ state: 'visible', timeout: 15000 });
    await selectEstado.selectOption('8');
    console.log('[BUSQUEDA] Estado "Adjudicada" seleccionado');
  });

  await esperarConDelay(500);

  await reintentar(async () => {
    console.log(`[BUSQUEDA] Estableciendo fecha desde: ${fechaDesde}...`);
    const inputDesde = page.locator('#calFrom');
    await inputDesde.waitFor({ state: 'visible', timeout: 15000 });
    await inputDesde.click();
    await inputDesde.fill('');
    await inputDesde.fill(fechaDesde);
    await page.keyboard.press('Tab');
    console.log('[BUSQUEDA] Fecha desde establecida');
  });

  await esperarConDelay(500);

  await reintentar(async () => {
    console.log(`[BUSQUEDA] Estableciendo fecha hasta: ${fechaHasta}...`);
    const inputHasta = page.locator('#calTo');
    await inputHasta.waitFor({ state: 'visible', timeout: 15000 });
    await inputHasta.click();
    await inputHasta.fill('');
    await inputHasta.fill(fechaHasta);
    await page.keyboard.press('Tab');
    console.log('[BUSQUEDA] Fecha hasta establecida');
  });

  await esperarConDelay(500);

  console.log('[BUSQUEDA] Filtros configurados correctamente');

  await seleccionarFiltroOfertado(page);

  await esperarConDelay(500);
}

async function seleccionarFiltroOfertado(page) {
  console.log('[BUSQUEDA] Seleccionando "Licitaciones en las que he ofertado"...');
  try {
    const radioOfertado = page.locator('#radLicitacionOfertado');
    const isChecked = await radioOfertado.isChecked().catch(() => false);
    if (!isChecked) {
      await radioOfertado.waitFor({ state: 'visible', timeout: 10000 });
      await radioOfertado.click();
      await esperarConDelay(1000);
      console.log('[BUSQUEDA] Radio "Licitaciones en las que he ofertado" seleccionado');
    } else {
      console.log('[BUSQUEDA] Radio ya seleccionado');
    }
  } catch (e) {
    console.log(`[BUSQUEDA] ADVERTENCIA: No se pudo seleccionar radio ofertado: ${e.message}`);
  }
}

async function ejecutarBusqueda(page) {
  console.log('[BUSQUEDA] Click en boton Buscar...');

  await reintentar(async () => {
    const btnBuscar = page.locator('#btnSearch');
    await btnBuscar.waitFor({ state: 'visible', timeout: 15000 });
    await btnBuscar.click();
  });

  console.log('[BUSQUEDA] Esperando resultados...');
  await esperarConDelay(5000);

  try {
    await page.waitForSelector('table', { timeout: 15000 }).catch(() => {});
    await esperarConDelay(2000);
  } catch (e) {
    console.log('[BUSQUEDA] Timeout esperando tabla de resultados');
  }
}

async function extraerResultados(page, context) {
  console.log('[BUSQUEDA] Extrayendo resultados de la tabla...');

  const licitaciones = [];

  try {
    const filas = await page.evaluate(() => {
      const anchors = document.querySelectorAll('a[onclick*="OpenGlobalPopup"]');
      const resultados = [];

      for (const anchor of anchors) {
        const onclick = anchor.getAttribute('onclick') || '';
        const match = onclick.match(/OpenGlobalPopup\('([^']+)'\)/);
        const urlFicha = match ? match[1] : '';
        const codigo = anchor.textContent.trim();

        if (codigo && urlFicha) {
          const row = anchor.closest('tr');
          let nombre = '';
          let descripcion = '';
          let demandante = '';
          let fechaPublicacion = '';
          let fechaCierre = '';

          if (row) {
            const cells = row.querySelectorAll('td');
            const cellTexts = Array.from(cells).map(c => c.textContent.trim());
            for (const text of cellTexts) {
              if (text && text !== codigo && text.length > 5) {
                if (!nombre) { nombre = text; }
                else if (!descripcion && text.length > nombre.length) { descripcion = text; }
              }
            }
          }

          const parentTable = anchor.closest('table');
          if (parentTable) {
            const allText = parentTable.innerText || '';
            const lines = allText.split('\n').map(l => l.trim()).filter(Boolean);

            let codigoIdx = -1;
            for (let i = 0; i < lines.length; i++) {
              if (lines[i] === codigo) {
                codigoIdx = i;
                break;
              }
            }

            if (codigoIdx >= 0) {
              nombre = lines[codigoIdx + 1] || nombre;
              descripcion = lines[codigoIdx + 2] || descripcion;
              demandante = lines[codigoIdx + 3] || '';
              fechaPublicacion = lines[codigoIdx + 4] || '';
              fechaCierre = lines[codigoIdx + 5] || '';
            }
          }

          const baseUrl = 'https://www.mercadopublico.cl';
          const fullUrl = urlFicha.startsWith('/') ? baseUrl + urlFicha : urlFicha;

          resultados.push({
            codigo,
            nombre: nombre.substring(0, 200),
            descripcion: descripcion.substring(0, 300),
            demandante,
            fechaPublicacion,
            fechaCierre,
            urlFicha: fullUrl,
            onclick,
          });
        }
      }

      return resultados;
    });

    licitaciones.push(...filas);
    console.log(`[BUSQUEDA] ${licitaciones.length} licitaciones extraidas de la pagina actual`);

    const hayPaginacion = await page.locator('#wucPager__TblPages, a:has-text(">")').count();
    if (hayPaginacion > 0) {
      console.log('[BUSQUEDA] Se detecto paginacion, iterando paginas...');
      let pagina = 2;

      while (pagina <= 50) {
        try {
          const btnSiguiente = page.locator('#wucPager__TblPages a:last-child')
            .or(page.locator('a[href*="Page$Next"], a:has-text(">")'))
            .first();

          if (await btnSiguiente.count() === 0) break;
          if (await btnSiguiente.isVisible({ timeout: 2000 }).catch(() => false)) {
            const isDisabled = await btnSiguiente.evaluate(el => el.classList.contains('disabled') || el.getAttribute('disabled') !== null).catch(() => true);
            if (isDisabled) break;

            await btnSiguiente.click();
            await esperarConDelay(3000);

            const filasPagina = await page.evaluate(() => {
              const anchors = document.querySelectorAll('a[onclick*="OpenGlobalPopup"]');
              const res = [];
              for (const anchor of anchors) {
                const onclick = anchor.getAttribute('onclick') || '';
                const match = onclick.match(/OpenGlobalPopup\('([^']+)'\)/);
                const urlFicha = match ? match[1] : '';
                const codigo = anchor.textContent.trim();
                if (codigo && urlFicha) {
                  res.push({
                    codigo,
                    urlFicha: urlFicha.startsWith('/') ? 'https://www.mercadopublico.cl' + urlFicha : urlFicha,
                    onclick,
                  });
                }
              }
              return res;
            });

            if (filasPagina.length === 0) break;
            licitaciones.push(...filasPagina);
            console.log(`[BUSQUEDA] Pagina ${pagina}: ${filasPagina.length} licitaciones`);
            pagina++;
          } else {
            break;
          }
        } catch (e) {
          console.log(`[BUSQUEDA] Fin de paginacion: ${e.message}`);
          break;
        }
      }
    }

  } catch (e) {
    console.log(`[BUSQUEDA] Error extrayendo resultados: ${e.message}`);
    const carpeta = process.env.MP_CARPETA_SALIDA || './descargas';
    await screenshotOnError(page, carpeta, 'resultados-error');
  }

  return licitaciones;
}