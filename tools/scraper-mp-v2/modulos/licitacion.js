import { screenshotOnError, esperarConDelay, reintentar } from './browser.js';

const BASE_URL = 'https://www.mercadopublico.cl';

export async function extraerDatosLicitacion(page, context, licitacion) {
  console.log(`\n[LICITACION] Procesando: ${licitacion.codigo || licitacion.nombre || 'sin codigo'}...`);

  try {
    let fichaPage = null;
    let isPopup = false;

    if (licitacion.urlFicha) {
      const result = await abrirFichaPopup(page, context, licitacion);
      fichaPage = result.fichaPage;
      isPopup = result.isPopup;
    } else if (licitacion.onclick) {
      const result = await abrirFichaPopup(page, context, licitacion);
      fichaPage = result.fichaPage;
      isPopup = result.isPopup;
    } else {
      console.log('[LICITACION] No hay link ni onclick, intentando extraer desde fila...');
      return {
        datos: {
          ...licitacion,
          estado: 'parcial',
          datosCompletos: false,
        },
        fichaPage: null,
        isPopup: false,
      };
    }

    await esperarConDelay(2000);

    const datos = await extraerDatosDePagina(fichaPage, licitacion);
    datos.urlFicha = fichaPage.url ? fichaPage.url() : licitacion.urlFicha || '';
    datos.fechaExtraccion = new Date().toISOString();

    const resultado = {
      ...licitacion,
      ...datos,
      estado: 'completo',
      datosCompletos: true,
    };

    console.log(`[LICITACION] Datos extraidos: ${resultado.nombre || resultado.codigo || 'sin nombre'}`);

    return { datos: resultado, fichaPage, isPopup };

  } catch (e) {
    console.log(`[LICITACION] Error: ${e.message}`);
    const carpeta = process.env.MP_CARPETA_SALIDA || './descargas';
    await screenshotOnError(page, carpeta, `licitacion-error-${Date.now()}`);
    return {
      datos: {
        ...licitacion,
        estado: 'error',
        error: e.message,
      },
      fichaPage: null,
      isPopup: false,
    };
  }
}

export async function cerrarFicha(page, fichaPage, isPopup) {
  try {
    if (fichaPage && !fichaPage.isClosed()) {
      if (isPopup) {
        console.log('[LICITACION] Cerrando ventana popup...');
        await fichaPage.close().catch(() => {});
      }
    }

    if (!isPopup) {
      try {
        await cerrarFancyboxes(page);
        await page.goBack({ waitUntil: 'domcontentloaded', timeout: 20000 });
        await esperarConDelay(2000);
        console.log('[LICITACION] Volvio a pagina de resultados');
      } catch (e) {
        console.log('[LICITACION] No se pudo volver atras, re-navegando a busqueda...');
        try {
          await page.goto(SEARCH_URL, { waitUntil: 'domcontentloaded', timeout: 30000 });
          await esperarConDelay(2000);
        } catch (e2) {
          console.log(`[LICITACION] Error re-navegando: ${e2.message}`);
        }
      }
    }
  } catch (e) {
    console.log(`[LICITACION] Error cerrando ficha: ${e.message}`);
  }
}

async function cerrarFancyboxes(page) {
  try {
    await page.evaluate(() => {
      if (typeof $.fancybox !== 'undefined') {
        $.fancybox.close();
      }
      const overlay = document.querySelector('.fancybox-overlay, .fancybox-wrap');
      if (overlay) overlay.remove();
    }).catch(() => {});
    await esperarConDelay(500);
  } catch (e) {
    // no hacer nada si no hay fancybox
  }
}

const SEARCH_URL = 'https://www.mercadopublico.cl/BID/Modules/RFB/NEwSearchProcurement.aspx';

async function abrirFichaDirecta(page, context, licitacion) {
  console.log('[LICITACION] Abriendo ficha via URL directa...');

  const urlFicha = licitacion.urlFicha;
  if (!urlFicha) {
    throw new Error('No hay URL de ficha disponible');
  }

  console.log(`[LICITACION] Navegando a: ${urlFicha.substring(0, 80)}...`);

  await page.goto(urlFicha, { waitUntil: 'domcontentloaded', timeout: 45000 });
  await esperarConDelay(3000);
  try {
    await page.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {});
  } catch (e) {
    console.log('[LICITACION] networkidle timeout, continuando con domcontentloaded...');
  }
  await esperarConDelay(2000);

  return { fichaPage: page, isPopup: false };
}

export async function abrirFichaPopup(page, context, licitacion) {
  let urlFicha = licitacion.urlFicha;

  if (urlFicha && licitacion.onclick) {
    const match = licitacion.onclick.match(/OpenGlobalPopup\('([^']+)'\)/);
    if (match) {
      urlFicha = match[1];
      if (urlFicha.startsWith('/')) {
        urlFicha = BASE_URL + urlFicha;
      }
    }
  } else if (!urlFicha && licitacion.onclick) {
    const match = licitacion.onclick.match(/OpenGlobalPopup\('([^']+)'\)/);
    if (!match) {
      throw new Error('No se pudo extraer URL del onclick: ' + licitacion.onclick.substring(0, 80));
    }
    urlFicha = match[1];
    if (urlFicha.startsWith('/')) {
      urlFicha = BASE_URL + urlFicha;
    }
  }

  if (!urlFicha) {
    throw new Error('No hay URL de ficha disponible');
  }

  console.log(`[LICITACION] Abriendo ficha en nueva ventana: ${urlFicha.substring(0, 80)}...`);
  const newPage = await context.newPage();
  try {
    await newPage.goto(urlFicha, { waitUntil: 'domcontentloaded', timeout: 45000 });
  await esperarConDelay(3000);
  try {
    await newPage.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {});
  } catch (e) {
    console.log('[LICITACION] networkidle timeout en popup, continuando...');
  }
    await esperarConDelay(2000);
    return { fichaPage: newPage, isPopup: true };
  } catch (e) {
    await newPage.close().catch(() => {});
    throw e;
  }
}

export async function extraerDatosDePagina(fichaPage, licitacionBase) {
  console.log('[LICITACION] Extrayendo datos estructurados de la ficha...');

  const datos = await fichaPage.evaluate(() => {
    const texto = document.body.innerText || '';
    const lineas = texto.split('\n').map(l => l.trim()).filter(Boolean);

    const getValueInline = (etiqueta) => {
      const idx = lineas.findIndex(l => l.includes(etiqueta));
      if (idx < 0) return null;
      const linea = lineas[idx];
      const separatorIdx = linea.indexOf(':');
      if (separatorIdx >= 0) return linea.substring(separatorIdx + 1).trim();
      return lineas[idx + 1] ? lineas[idx + 1].trim() : null;
    };

    let nombreValue = getValueInline('Nombre de la licitaci') || getValueInline('Nombre');
    let estadoValue = getValueInline('Estado') || null;
    let descripcionValue = getValueInline('Descripci') || null;
    let tipoValue = getValueInline('Tipo de licitaci') || getValueInline('Tipo');
    let monedaValue = getValueInline('Moneda') || getValueInline('Moneda');

    return {
      tituloPagina: document.title,
      nombre: nombreValue,
      estado: estadoValue,
      descripcion: descripcionValue,
      tipo: tipoValue,
      moneda: monedaValue,
      organismo: {
        razonSocial: getValueInline('Razón social') || getValueInline('Organismo'),
        rut: getValueInline('R.U.T.') || getValueInline('RUT'),
        unidad: getValueInline('Unidad de compra') || getValueInline('Unidad'),
      },
      fechas: {
        publicacion: getValueInline('Fecha de Publicaci') || null,
        cierre: getValueInline('Fecha de cierre') || null,
        adjudicacion: getValueInline('Fecha de Adjudicaci') || null,
      },
      monto: {
        totalEstimado: getValueInline('Monto Total Estimado') || getValueInline('Monto'),
      },
      rawText: texto.substring(0, 80000),
    };
  });

  datos.fechaExtraccion = new Date().toISOString();

  const resultado = {
    ...licitacionBase,
    ...datos,
    estado: 'completo',
    datosCompletos: true,
  };

  console.log(`[LICITACION] Datos extraidos: ${resultado.nombre || resultado.codigo || 'sin nombre'}`);

  return resultado;
}