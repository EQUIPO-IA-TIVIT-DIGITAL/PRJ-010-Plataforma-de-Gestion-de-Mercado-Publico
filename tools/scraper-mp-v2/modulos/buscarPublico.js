// spec 031 (US4): búsqueda pública de licitaciones, sin sesión ni Playwright -- a diferencia
// de buscar.js (que navega la vista autenticada "en las que he ofertado" vía WebForms +
// postback), este módulo llama directamente al endpoint HTML público que usa
// https://www.mercadopublico.cl/Home/BusquedaLicitacion, confirmado en vivo el 2026-08-04
// (ver research.md §4 de la spec 031, "Actualización 2026-08-04"). No requiere login.
//
// Uso: obtener el universo de licitaciones de un área+período (potencialmente donde TIVIT
// nunca participó), para luego visitar cada ficha con Playwright y revisar su Cuadro de
// Ofertas (extraerCuadroOfertas, sin modificar) en busca de un competidor específico.

import * as cheerio from 'cheerio';

const BUSCAR_URL = 'https://www.mercadopublico.cl/BuscarLicitacion/Home/Buscar';
const BUSCADOR_PAGINA_URL = 'https://www.mercadopublico.cl/BuscarLicitacion?IsFirstTableDesign=True';
const FICHA_URL_TEMPLATE = 'https://www.mercadopublico.cl/Procurement/Modules/RFB/DetailsAcquisition.aspx?idlicitacion={codigo}';

// El endpoint POST no requiere login, pero SÍ exige una cookie de sesión obtenida con un GET
// previo a la página del buscador -- sin ella responde 200 con una lista vacía en vez de un
// error explícito (confirmado en vivo, ver research.md §4). Se cachea a nivel de proceso: un
// solo GET por corrida del script, no uno por cada término buscado.
let cookieCache = null;

async function obtenerCookieSesion() {
  if (cookieCache) return cookieCache;
  const resp = await fetch(BUSCADOR_PAGINA_URL);
  const setCookie = typeof resp.headers.getSetCookie === 'function' ? resp.headers.getSetCookie() : [];
  cookieCache = setCookie.map(c => c.split(';')[0]).join('; ');
  return cookieCache;
}

/**
 * @param {{ textoBusqueda?: string, fechaDesde: Date, fechaHasta: Date, registrosPorPagina?: number, pagina?: number }} opts
 * @returns {Promise<{ codigo: string, nombre: string, urlFicha: string }[]>}
 */
export async function buscarLicitacionesPublico(opts) {
  const {
    textoBusqueda = '',
    fechaDesde,
    fechaHasta,
    registrosPorPagina = 50,
    pagina = 0,
  } = opts;

  const body = {
    textoBusqueda,
    idEstado: '-1',
    codigoRegion: '-1',
    idTipoLicitacion: '-1',
    fechaInicio: fechaDesde.toISOString(),
    fechaFin: fechaHasta.toISOString(),
    registrosPorPagina: String(registrosPorPagina),
    idTipoFecha: [],
    idOrden: '1',
    compradores: [],
    garantias: null,
    rubros: [],
    proveedores: [], // ver research.md -- probado con texto libre, no filtra por competidor; no se usa
    montoEstimadoTipo: [0],
    esPublicoMontoEstimado: null,
    pagina,
  };

  const cookie = await obtenerCookieSesion();

  const resp = await fetch(BUSCAR_URL, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Requested-With': 'XMLHttpRequest',
      ...(cookie ? { Cookie: cookie } : {}),
    },
    body: JSON.stringify(body),
  });

  if (!resp.ok) {
    throw new Error(`[BUSCAR-PUBLICO] HTTP ${resp.status} al buscar licitaciones públicas`);
  }

  const html = await resp.text();
  return parsearResultados(html);
}

function parsearResultados(html) {
  const $ = cheerio.load(html);
  const resultados = [];

  $('.lic-bloq-wrap').each((_, el) => {
    const bloque = $(el);
    // el código vive en un <span class="clearfix"> hermano del <strong>"ID Licitación:"</strong>
    // (confirmado en vivo, ver research.md §4) -- no dentro del propio <strong>.
    const codigo = bloque.find('.id-licitacion span.clearfix').first().text().trim();
    if (!codigo) return;

    const link = bloque.find('a').first();
    const nombre = link.text().trim() || bloque.find('h3, h4').first().text().trim() || codigo;

    // El onclick trae la URL de ficha exacta que usa el propio sitio -- se prioriza sobre el
    // template propio (más confiable que reconstruirla a mano).
    const onclick = link.attr('onclick') || '';
    const match = onclick.match(/verFicha\('([^']+)'\)/);
    const urlFicha = match ? match[1].replace(/^http:/, 'https:') : FICHA_URL_TEMPLATE.replace('{codigo}', encodeURIComponent(codigo));

    resultados.push({ codigo, nombre, urlFicha });
  });

  return resultados;
}
