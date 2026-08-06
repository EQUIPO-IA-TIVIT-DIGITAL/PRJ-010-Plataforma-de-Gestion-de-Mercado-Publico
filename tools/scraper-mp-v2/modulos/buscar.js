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
    let estadosExitosos = 0;

    for (const estado of estados) {
      // Reproducido en vivo el 2026-07-15: al encadenar busquedas consecutivas sobre la misma
      // pagina, el postback async de ASP.NET (UpdatePanel) puede quedar COLGADO para siempre
      // (Sys.WebForms.PageRequestManager queda "busy"; ni abortPostBack() lo destraba). En ese
      // estado todos los clics en Buscar se ignoran en silencio y la tabla vieja queda en el
      // DOM: el scraper extraia resultados obsoletos o casi nada. La unica recuperacion fiable
      // es recargar la pagina y reconfigurar los filtros, que es lo que hace este retry.
      let exitoEstado = false;
      const esPrimerEstado = estado === estados[0];
      for (let intento = 1; intento <= 2 && !exitoEstado; intento++) {
        try {
          // Recarga proactiva entre estados (no solo en reintentos): el cuelgue del postback
          // ocurre de forma casi deterministica en la SEGUNDA busqueda sobre la misma pagina
          // (verificado en corridas del 2026-07-16: estados 5/7/15 colgaban siempre en el
          // intento 1 y se recuperaban tras recargar). Recargar cuesta ~5s; esperar el
          // timeout del cuelgue costaba 45s por estado.
          if (intento > 1 || !esPrimerEstado) {
            console.log(`[BUSQUEDA] Recargando pagina de busqueda antes del estado '${estado}'...`);
            await page.goto(SEARCH_URL, { waitUntil: 'networkidle', timeout: 45000 });
            await cerrarPopups(page);
            await configurarFiltrosBase(page);
          }

          console.log(`\n[BUSQUEDA] Cambiando a filtro de estado: '${estado}'...`);
          await esperarPostbackLibre(page, 15000);

          await reintentar(async () => {
            const selectEstado = page.locator('#cboState');
            await selectEstado.waitFor({ state: 'visible', timeout: 15000 });
            await selectEstado.selectOption(estado);
          });

          // El change del dropdown puede disparar su propio postback: esperar a que termine
          // ANTES de clicar Buscar (la colision de ambos es lo que deja la pagina colgada).
          await esperarPostbackLibre(page, 15000);

          console.log(`[BUSQUEDA] Ejecutando busqueda para estado '${estado}'...`);
          await ejecutarBusqueda(page);

          console.log(`[BUSQUEDA] Extrayendo resultados para estado '${estado}'...`);
          const resultadosEstado = await extraerResultados(page, context);

          for (const lic of resultadosEstado) {
            licitacionesMap.set(lic.codigo, lic);
          }

          console.log(`[BUSQUEDA] Estado '${estado}': ${resultadosEstado.length} encontradas. Total acumulado único: ${licitacionesMap.size}`);
          exitoEstado = true;
          estadosExitosos++;
        } catch (errEstado) {
          console.log(`[BUSQUEDA] ADVERTENCIA: Error en ciclo de estado '${estado}' (intento ${intento}/2): ${errEstado.message}`);
          if (intento === 2) {
            const carpeta = process.env.MP_CARPETA_SALIDA || './descargas';
            await screenshotOnError(page, carpeta, `busqueda-estado-${estado}-error`);
          }
        }
      }
    }

    // 030-qol-frontend-y-fix-scraper US3: si NINGUNO de los 5 estados pudo leerse (2 intentos
    // cada uno agotados por postback colgado, sesion caida, etc.), el ciclo NO tuvo una lectura
    // real del sitio -- antes esto retornaba [] silenciosamente y el llamador (agente-mp.js)
    // lo trataba igual que "0 licitaciones nuevas legitimas", terminando el ciclo con exit code 0
    // y una notificacion ambigua ("El scraper termino con codigo 0. Licitaciones: 0, Actas: 0").
    // Lanzar aqui hace que el ciclo se reporte como fallo real (ver agente-mp.js catch de
    // executeCycle) en vez de un "0 resultados" que el usuario no puede distinguir de un dia
    // sin licitaciones nuevas.
    if (estadosExitosos === 0) {
      throw new Error(
        `No se pudo leer ningun estado de busqueda (0 de ${estados.length} estados exitosos tras 2 intentos cada uno) -- posible sesion invalida, cambio de estructura del sitio, o postback colgado no recuperado`
      );
    }

    const licitaciones = Array.from(licitacionesMap.values());
    console.log(`\n[BUSQUEDA] Búsqueda finalizada. Total único de licitaciones encontradas: ${licitaciones.length} (${estadosExitosos}/${estados.length} estados leidos correctamente)`);
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

/**
 * Espera a que el PageRequestManager de ASP.NET no tenga un postback async en vuelo.
 * Si sigue ocupado tras el timeout, la pagina quedo colgada (verificado en vivo 2026-07-15:
 * de ese estado no se sale sin recargar) -- se lanza error para que el llamador recargue.
 */
async function esperarPostbackLibre(page, timeout = 15000) {
  const libre = await page.waitForFunction(
    () => {
      try {
        if (typeof Sys === 'undefined' || !Sys.WebForms) return true; // pagina sin MS AJAX
        return !Sys.WebForms.PageRequestManager.getInstance().get_isInAsyncPostBack();
      } catch (e) {
        return true;
      }
    },
    null,
    { timeout }
  ).then(() => true).catch(() => false);

  if (!libre) {
    throw new Error(`Postback de ASP.NET colgado (sigue "busy" tras ${timeout}ms) - requiere recargar la pagina`);
  }
}

async function ejecutarBusqueda(page) {
  console.log('[BUSQUEDA] Click en boton Buscar...');

  // Senal de "resultados listos" que NO depende de eventos de MS AJAX: el evento endRequest
  // del PageRequestManager NO llega a dispararse en esta pagina (verificado en vivo
  // 2026-07-15/16: beginRequest si, endRequest nunca), y waitForSelector('table') tampoco
  // sirve porque la tabla vieja ya esta en el DOM. Se combinan dos senales:
  //  a) transicion del flag get_isInAsyncPostBack(): true (postback en vuelo) -> false
  //  b) cambio de la "firma" del area de resultados (texto "Se encontraron N" + 1er codigo)
  const firmaResultados = () => page.evaluate(() => {
    const cuenta = [...document.querySelectorAll('span, div, p')]
      .find(e => /Se encontraron/.test(e.textContent) && e.textContent.length < 80);
    const primerCodigo = document.querySelector('a[onclick*="OpenGlobalPopup"]');
    return `${cuenta ? cuenta.textContent.trim() : 'sin-cuenta'}|${primerCodigo ? primerCodigo.textContent.trim() : 'sin-filas'}`;
  }).catch(() => null);

  const firmaAntes = await firmaResultados();

  const btnBuscar = page.locator('#btnSearch');
  await btnBuscar.waitFor({ state: 'visible', timeout: 15000 });
  await btnBuscar.click({ force: true });

  console.log('[BUSQUEDA] Esperando fin del postback de resultados...');
  const TIMEOUT_MS = 45000;
  const inicio = Date.now();
  let listo = false;
  let seVioOcupado = false;

  while (Date.now() - inicio < TIMEOUT_MS) {
    const busy = await page.evaluate(() => {
      try {
        if (typeof Sys !== 'undefined' && Sys.WebForms) {
          return Sys.WebForms.PageRequestManager.getInstance().get_isInAsyncPostBack();
        }
      } catch (e) { /* pagina sin MS AJAX */ }
      return false;
    }).catch(() => null); // null = contexto destruido (postback de pagina completa)

    if (busy === null) {
      await page.waitForLoadState('domcontentloaded', { timeout: 30000 }).catch(() => {});
      listo = true;
      break;
    }

    if (busy) {
      seVioOcupado = true;
    } else if (seVioOcupado) {
      // Transicion ocupado -> libre: el postback async completo y el panel ya se re-renderizo
      listo = true;
      break;
    } else {
      // Aun no se observa el postback en vuelo: puede que ya haya terminado entre el click
      // y el primer poll (respuesta muy rapida) -- la firma del area de resultados lo dice.
      const firmaAhora = await firmaResultados();
      if (firmaAhora !== null && firmaAntes !== null && firmaAhora !== firmaAntes) {
        listo = true;
        break;
      }
    }

    await new Promise(r => setTimeout(r, 300));
  }

  if (!listo) {
    throw new Error(`La busqueda no completo su postback en ${TIMEOUT_MS}ms (pagina colgada) - requiere recargar la pagina`);
  }

  // Pequeno margen para que el UpdatePanel termine de pintar tras el re-render
  await esperarConDelay(1000);
  await page.waitForSelector('a[onclick*="OpenGlobalPopup"], #wucPager__TblPages', { timeout: 10000 }).catch(() => {
    console.log('[BUSQUEDA] Busqueda completada sin filas visibles (posible 0 resultados para este estado)');
  });
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

          // Columnas reales de la tabla de resultados (verificado en vivo 2026-08-03):
          // Seguimiento | Numero | Nombre | Descripcion | Demandante | Fecha de publicacion |
          // Fecha de cierre | Estado | Mis ofertas | Acciones -- se toman las celdas <td> por
          // indice relativo a la celda del codigo, NO por conteo de "lineas" de innerText de
          // toda la tabla: la Descripcion es texto largo que envuelve en varias lineas visuales,
          // lo que desalineaba todo lo que venia despues (demandante y ambas fechas salian
          // siempre vacias). cells[] es por fila, no por tabla completa, así que no le afecta
          // el wrap de texto de otras filas.
          if (row) {
            const cells = Array.from(row.querySelectorAll('td'));
            const codigoCellIdx = cells.findIndex(c => c.textContent.trim() === codigo);

            if (codigoCellIdx >= 0) {
              nombre = cells[codigoCellIdx + 1]?.textContent.trim() || '';
              descripcion = cells[codigoCellIdx + 2]?.textContent.trim() || '';
              demandante = cells[codigoCellIdx + 3]?.textContent.trim() || '';
              fechaPublicacion = cells[codigoCellIdx + 4]?.textContent.trim() || '';
              fechaCierre = cells[codigoCellIdx + 5]?.textContent.trim() || '';
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

          // Postback de WebForms: esperar a que cambie la primera fila (no solo un delay fijo).
          // Si la primera fila NO cambia, el postback quedo colgado o el click no surtio
          // efecto: seguir extrayendo repetiria la pagina anterior (duplicados silenciosos).
          const cambio = await page.waitForFunction(
            (codigoAnterior) => {
              const a = document.querySelector('a[onclick*="OpenGlobalPopup"]');
              return a && a.textContent.trim() !== codigoAnterior;
            },
            primerCodigoAntes,
            { timeout: 20000 }
          ).then(() => true).catch(() => false);

          if (!cambio) {
            console.log(`[BUSQUEDA] Pagina ${pagina}: la tabla no cambio tras el postback (pagina colgada?), deteniendo paginacion para no duplicar filas`);
            break;
          }
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

              // Mismo fix que en la extraccion de la pagina 1: celdas <td> por indice relativo
              // al codigo, no lineas de innerText de toda la tabla (ver comentario arriba).
              const row = anchor.closest('tr');
              let nombre = '', descripcion = '', demandante = '', fechaPublicacion = '', fechaCierre = '';
              if (row) {
                const cells = Array.from(row.querySelectorAll('td'));
                const codigoCellIdx = cells.findIndex(c => c.textContent.trim() === codigo);
                if (codigoCellIdx >= 0) {
                  nombre = cells[codigoCellIdx + 1]?.textContent.trim() || '';
                  descripcion = cells[codigoCellIdx + 2]?.textContent.trim() || '';
                  demandante = cells[codigoCellIdx + 3]?.textContent.trim() || '';
                  fechaPublicacion = cells[codigoCellIdx + 4]?.textContent.trim() || '';
                  fechaCierre = cells[codigoCellIdx + 5]?.textContent.trim() || '';
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