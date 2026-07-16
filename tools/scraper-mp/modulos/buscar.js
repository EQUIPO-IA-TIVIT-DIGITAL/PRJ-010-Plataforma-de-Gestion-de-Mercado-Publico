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

    console.log('[BUSQUEDA] Configurando filtros base...');
    await configurarFiltrosBase(page);

    // Bucle interactivo por estados para extraer la participación real completa
    const estados = ['8', '6', '5', '7', '15']; // Adjudicada, Cerrada, Publicada, Desierta, Revocada
    const licitacionesMap = new Map();

    for (const estado of estados) {
      try {
        console.log(`\n[BUSQUEDA] Cambiando a filtro de estado: '${estado}'...`);
        
        await reintentar(async () => {
          const selectEstado = page.locator('#cboState');
          await selectEstado.waitFor({ state: 'visible', timeout: 15000 });
          await selectEstado.selectOption(estado);
        });
        await esperarConDelay(1000);

        console.log(`[BUSQUEDA] Ejecutando busqueda para estado '${estado}'...`);
        await ejecutarBusqueda(page);

        console.log(`[BUSQUEDA] Extrayendo resultados para estado '${estado}'...`);
        const resultadosEstado = await extraerResultados(page, context);
        
        for (const lic of resultadosEstado) {
          licitacionesMap.set(lic.codigo, lic);
        }
        
        console.log(`[BUSQUEDA] Estado '${estado}': ${resultadosEstado.length} encontradas. Total acumulado único: ${licitacionesMap.size}`);
      } catch (errEstado) {
        console.log(`[BUSQUEDA] ADVERTENCIA: Error en ciclo de estado '${estado}': ${errEstado.message}`);
      }
    }

    const licitaciones = Array.from(licitacionesMap.values());
    console.log(`\n[BUSQUEDA] Búsqueda finalizada. Total único de licitaciones encontradas: ${licitaciones.length}`);
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

async function configurarFiltrosBase(page) {
  const fechaDesde = process.env.MP_FECHA_DESDE || '01-01-2026';
  const hoy = new Date();
  const dia = String(hoy.getDate()).padStart(2, '0');
  const mes = String(hoy.getMonth() + 1).padStart(2, '0');
  const anio = hoy.getFullYear();
  const fechaHasta = `${dia}-${mes}-${anio}`;

  console.log(`[BUSQUEDA] Filtros: Region=Todas, Desde=${fechaDesde}, Hasta=${fechaHasta}`);

  await reintentar(async () => {
    console.log('[BUSQUEDA] Seleccionando "Todas las Regiones"...');
    const selectRegion = page.locator('#cboRegion');
    await selectRegion.waitFor({ state: 'visible', timeout: 15000 });
    await selectRegion.selectOption(' ');
    console.log('[BUSQUEDA] Region "Todas" seleccionada');
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

  console.log('[BUSQUEDA] Filtros base configurados correctamente');

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
  const modalCargando = page.locator('#ModalCargando_backgroundElement, .CssBackCargando').first();
  try {
    if (await modalCargando.isVisible().catch(() => false)) {
      console.log('[BUSQUEDA] Esperando que desaparezca el modal de carga...');
      await modalCargando.waitFor({ state: 'hidden', timeout: 20000 }).catch(() => {});
      await esperarConDelay(1000);
    }
  } catch (errModal) {
    // ignorar
  }

  console.log('[BUSQUEDA] Click en boton Buscar...');

  await reintentar(async () => {
    try {
      if (await modalCargando.isVisible().catch(() => false)) {
        await modalCargando.waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});
      }
    } catch (err) {
      // ignorar
    }

    const btnBuscar = page.locator('#btnSearch');
    await btnBuscar.waitFor({ state: 'visible', timeout: 15000 });
    await btnBuscar.click({ force: true });
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

    // El paginador de esta pagina (#wucPager__TblPages) NO usa <a href>, usa <div onclick="
    // javascript:fnMovePage(N,'wucPager')"> -- un postback de ASP.NET WebForms. El codigo
    // anterior buscaba <a> (nunca los encontraba) y por eso SIEMPRE se quedaba solo en la
    // pagina 1, sin reportar error ni aviso. Se detecto el 2026-07-07 al inspeccionar el HTML
    // real del paginador (mostraba paginas 1,2,3,4 para la busqueda "licitaciones ofertadas
    // por TIVIT", nunca visitadas).
    const numerosPagina = await page.evaluate(() => {
      const el = document.getElementById('wucPager__TblPages');
      if (!el) return [];
      const divs = el.querySelectorAll('div[onclick*="fnMovePage"]');
      const nums = [];
      for (const d of divs) {
        const m = (d.getAttribute('onclick') || '').match(/fnMovePage\((\d+)/);
        if (m) nums.push(parseInt(m[1], 10));
      }
      return nums;
    });

    if (numerosPagina.length > 0) {
      const maxPagina = Math.max(...numerosPagina);
      console.log(`[BUSQUEDA] Paginacion detectada: ${maxPagina} paginas en total`);

      for (let pagina = 2; pagina <= maxPagina; pagina++) {
        try {
          const primerCodigoAntes = await page.evaluate(() => {
            const a = document.querySelector('a[onclick*="OpenGlobalPopup"]');
            return a ? a.textContent.trim() : null;
          });

          const clickeado = await page.evaluate((n) => {
            const el = document.getElementById('wucPager__TblPages');
            if (!el) return false;
            const divs = el.querySelectorAll('div[onclick*="fnMovePage"]');
            for (const d of divs) {
              if ((d.getAttribute('onclick') || '').includes(`fnMovePage(${n},`)) {
                d.click();
                return true;
              }
            }
            return false;
          }, pagina);

          if (!clickeado) {
            console.log(`[BUSQUEDA] No se encontro el control de pagina ${pagina}, deteniendo paginacion`);
            break;
          }

          // Postback de WebForms: esperar a que cambie la primera fila (no solo un delay fijo)
          await page.waitForFunction(
            (codigoAnterior) => {
              const a = document.querySelector('a[onclick*="OpenGlobalPopup"]');
              return a && a.textContent.trim() !== codigoAnterior;
            },
            primerCodigoAntes,
            { timeout: 15000 }
          ).catch(() => {});
          await esperarConDelay(2000);

          const filasPagina = await page.evaluate(() => {
            const anchors = document.querySelectorAll('a[onclick*="OpenGlobalPopup"]');
            const resultados = [];
            for (const anchor of anchors) {
              const onclick = anchor.getAttribute('onclick') || '';
              const match = onclick.match(/OpenGlobalPopup\('([^']+)'\)/);
              const urlFicha = match ? match[1] : '';
              const codigo = anchor.textContent.trim();
              if (!codigo || !urlFicha) continue;

              const parentTable = anchor.closest('table');
              let nombre = '', descripcion = '', demandante = '', fechaPublicacion = '', fechaCierre = '';
              if (parentTable) {
                const allText = parentTable.innerText || '';
                const lines = allText.split('\n').map(l => l.trim()).filter(Boolean);
                const codigoIdx = lines.indexOf(codigo);
                if (codigoIdx >= 0) {
                  nombre = lines[codigoIdx + 1] || '';
                  descripcion = lines[codigoIdx + 2] || '';
                  demandante = lines[codigoIdx + 3] || '';
                  fechaPublicacion = lines[codigoIdx + 4] || '';
                  fechaCierre = lines[codigoIdx + 5] || '';
                }
              }

              const baseUrl = 'https://www.mercadopublico.cl';
              resultados.push({
                codigo,
                nombre: nombre.substring(0, 200),
                descripcion: descripcion.substring(0, 300),
                demandante,
                fechaPublicacion,
                fechaCierre,
                urlFicha: urlFicha.startsWith('/') ? baseUrl + urlFicha : urlFicha,
                onclick,
              });
            }
            return resultados;
          });

          if (filasPagina.length === 0) {
            console.log(`[BUSQUEDA] Pagina ${pagina}: sin filas, deteniendo paginacion`);
            break;
          }
          licitaciones.push(...filasPagina);
          console.log(`[BUSQUEDA] Pagina ${pagina}: ${filasPagina.length} licitaciones`);
        } catch (e) {
          console.log(`[BUSQUEDA] Error en pagina ${pagina}: ${e.message}`);
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