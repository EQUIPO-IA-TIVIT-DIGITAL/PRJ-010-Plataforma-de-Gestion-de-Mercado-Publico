import { esperarConDelay, screenshotOnError } from './browser.js';

const MAX_REINTENTOS = 2;

/**
 * Busca la tabla de "Cuadro de Ofertas" en el documento actual y extrae sus filas. Exportada a
 * nivel de modulo (en vez de una closure dentro de extraerCuadroOfertas) para poder testearla
 * directamente via Playwright contra un fixture HTML real (ver
 * tools/scraper-mp-v2/test-cuadroOfertas.mjs, 029-fix-hallazgos-code-review-competidores-alertas
 * T049) -- no captura nada del closure de extraerCuadroOfertas, solo usa `document`.
 *
 * 029-fix-hallazgos-code-review-competidores-alertas (FR-005): antes se asumia siempre el orden
 * fijo "Rut Proveedor | Proveedor | Nombre Oferta | Total Oferta | Estado" (posiciones 0,1,3,4),
 * validando solo que hubiera al menos 4 celdas -- una variante de layout con columnas
 * reordenadas corrompia monto/estado silenciosamente. Se resuelve el indice real de cada columna
 * contra el texto del encabezado, igual que ya se hace para localizar la tabla misma, en vez de
 * asumir una posicion fija.
 */
export const buscarTablaEnDocumento = () => {
  // Busca la tabla que tenga un encabezado reconocible (Rut Proveedor / Proveedor / Total
  // Oferta / Estado) en vez de depender de un id fijo -- la pagina de Mercado Publico
  // reutiliza ids genericos entre distintos cuadros/modales.
  const tablas = Array.from(document.querySelectorAll('table'));
  const tabla = tablas.find(t => {
    const texto = t.innerText.toLowerCase();
    return texto.includes('proveedor') && (texto.includes('total oferta') || texto.includes('estado'));
  });

  if (!tabla) return { encontrada: false, filas: [] };

  const todasLasFilas = Array.from(tabla.querySelectorAll('tr'));
  const filaEncabezado = todasLasFilas[0];
  const filas = todasLasFilas.slice(1);
  const resultados = [];

  const encabezados = filaEncabezado
    ? Array.from(filaEncabezado.querySelectorAll('th,td')).map(c => (c.textContent || '').trim().toLowerCase())
    : [];

  const idxRut = encabezados.findIndex(h => h.includes('rut'));
  const idxProveedor = encabezados.findIndex(h => h.includes('proveedor') && !h.includes('rut'));
  const idxMonto = encabezados.findIndex(h => h.includes('total oferta') || h.includes('monto'));
  const idxEstado = encabezados.findIndex(h => h.includes('estado'));

  const columnasReconocidas = idxProveedor >= 0 && idxMonto >= 0 && idxEstado >= 0;
  if (!columnasReconocidas) {
    // No se pudo mapear el encabezado real -- no se adivina la posicion, se reporta la
    // tabla como no reconocida en vez de arriesgar datos en la columna equivocada.
    return { encontrada: true, filas: [], encabezadoNoReconocido: true };
  }

  for (const fila of filas) {
    const celdas = Array.from(fila.querySelectorAll('td')).map(td => td.textContent?.trim() || '');
    if (celdas.length <= Math.max(idxProveedor, idxMonto, idxEstado, idxRut)) continue;

    const rut = idxRut >= 0 ? celdas[idxRut] : '';
    const proveedor = celdas[idxProveedor];
    const montoTexto = celdas[idxMonto];
    const estado = celdas[idxEstado];
    if (!proveedor) continue;

    // La grilla de Mercado Publico duplica cada fila con una version oculta (responsive
    // mobile) cuyas columnas quedan desalineadas -- se detecto en vivo una fila espuria
    // donde "proveedor" terminaba siendo en realidad el RUT (ej. "92.580.000-7"). Se
    // descarta cualquier fila donde el campo "proveedor" tenga forma de RUT chileno.
    if (/^\d{1,3}(\.\d{3})*-[\dkK]$/.test(proveedor)) continue;

    const montoLimpio = (montoTexto || '').replace(/[^0-9]/g, '');
    const monto = montoLimpio ? parseInt(montoLimpio, 10) : null;

    resultados.push({
      rutProveedor: rut || null,
      nombreProveedor: proveedor,
      montoOferta: monto,
      estadoOferta: estado || null,
    });
  }

  return { encontrada: true, filas: resultados };
};

/**
 * 024-inteligencia-competencia-alertas / US1: extrae el listado de oferentes (no solo el
 * adjudicatario) desde el "Cuadro de Ofertas" de la ficha publica de una licitacion adjudicada
 * -- confirmado en vivo el 2026-07-09 que esta seccion es publica, sin necesitar login.
 *
 * Corregido 2026-07-10: el icono "Cuadro de ofertas" SI abre una ventana nueva (igual que "Ver
 * Adjuntos" en adjuntos.js) -- el comentario original de que era "un modal en la misma pagina"
 * era una suposicion incorrecta del spike inicial, nunca verificada programaticamente. Se
 * confirmo en vivo contra 622-12-LP26 (la misma licitacion validada en research.md R3): tras el
 * click, ninguna busqueda de texto/tabla en fichaPage (ni en sus iframes) encontraba los datos
 * visibles en pantalla -- estaban en una pagina nueva de Playwright (context.pages()), exactamente
 * el mismo patron que adjuntos.js. Antes de este fix, el icono tampoco se encontraba (ver el otro
 * fix debajo), asi que este bug de popup nunca se habia manifestado en una corrida real.
 *
 * Tambien corregido: el rotulo "Cuadro de ofertas" que se ve bajo el icono NO es texto real del
 * DOM (confirmado con un TreeWalker sobre todo document.body -- cero coincidencias de "cuadro" en
 * ningun nodo de texto), es parte del grafico del icono. El locator por texto nunca pudo
 * funcionar. El icono real es un <input type="image" id="imgCuadroOferta" src=".../ic-32.png">,
 * el mismo patron por id que ya usa adjuntos.js (#imgAdjuntos) -- se prioriza el id.
 *
 * @param {import('playwright').Page} fichaPage - pagina ya posicionada en la ficha de la licitacion
 * @param {import('playwright').BrowserContext} context - para detectar la ventana nueva del cuadro de ofertas
 * @param {{codigo?: string, nombre?: string}} datosLicitacion
 * @param {string} carpetaDestino - para guardar screenshots de error
 * @returns {{ofertas: Array<{rutProveedor: string, nombreProveedor: string, montoOferta: number|null, estadoOferta: string}>, error?: string, estructuraCambio?: boolean}}
 */
export async function extraerCuadroOfertas(fichaPage, context, datosLicitacion, carpetaDestino) {
  console.log(`\n[CUADRO-OFERTAS] Buscando Cuadro de Ofertas para: ${datosLicitacion.codigo || datosLicitacion.nombre || 'sin codigo'}...`);

  for (let intento = 1; intento <= MAX_REINTENTOS; intento++) {
    let paginaNuevaAbierta = null;

    try {
      // Corregido 2026-07-10: el icono real y estable es <input type="image" id="imgCuadroOferta">
      // (mismo patron por id que #imgAdjuntos en adjuntos.js), verificado en vivo (count=1,
      // boundingBox real) contra 622-12-LP26. Dos bugs encontrados y ya corregidos antes de
      // llegar a este selector simple: (1) el locator combinado por comas mezclando CSS y
      // "text=" no es sintaxis valida de Playwright -- lanzaba un error de parseo que el
      // .catch(() => false) tragaba en silencio, disfrazado de "no encontrado"; (2)
      // isVisible({timeout}) no espera/reintenta (a diferencia de waitFor), asi que aunque el
      // selector fuera valido la primera revision corria antes de que la ficha terminara de
      // renderizar. waitFor({state:'visible'}) si reintenta hasta el timeout.
      const iconoCuadroOfertas = fichaPage.locator('#imgCuadroOferta').first();
      const visible = await iconoCuadroOfertas.waitFor({ state: 'visible', timeout: 8000 }).then(() => true).catch(() => false);

      if (!visible) {
        console.log(`[CUADRO-OFERTAS] No se encontro el icono "Cuadro de ofertas" en la ficha (url: ${fichaPage.url()}) -- probablemente este tipo/estado de licitacion no lo expone (ej. Compra Agil, Trato Directo).`);
        await screenshotOnError(fichaPage, carpetaDestino, 'debug-icono-no-encontrado');
        return { ofertas: [], error: 'Icono no disponible para este tipo de licitacion' };
      }

      const isDisabled = await iconoCuadroOfertas.getAttribute('disabled').then(val => val !== null).catch(() => false);
      if (isDisabled) {
        console.log(`[CUADRO-OFERTAS] El icono "Cuadro de ofertas" esta deshabilitado (disabled) en la ficha — omitiendo click.`);
        return { ofertas: [], error: 'Icono deshabilitado en la ficha' };
      }

      const pagesBefore = context.pages().length;
      await iconoCuadroOfertas.click();
      await esperarConDelay(3000);

      // No se pudo determinar con certeza si el Cuadro de Ofertas abre una ventana nueva (como
      // "Ver Adjuntos") o un modal/dialog dentro de la misma pagina que Playwright no reporta
      // igual que un screenshot visual -- una prueba en vivo mostro los datos en pantalla sin que
      // ninguna busqueda por DOM (ni por frames) los encontrara. Para no depender de resolver esa
      // duda, se prueban ambas rutas: primero si aparecio una pagina nueva; si no, se busca la
      // tabla directamente dentro de fichaPage (que cubre el caso de modal en la misma pagina).
      const pagesAfter = context.pages();
      const huboVentanaNueva = pagesAfter.length > pagesBefore;
      const paginaDeBusqueda = huboVentanaNueva ? pagesAfter[pagesAfter.length - 1] : fichaPage;
      if (huboVentanaNueva) paginaNuevaAbierta = paginaDeBusqueda;

      if (huboVentanaNueva) {
        await paginaDeBusqueda.waitForLoadState('domcontentloaded', { timeout: 20000 }).catch(() => {});
      }
      await esperarConDelay(1500);

      // "Resumen de ofertas" ya viene seleccionado por defecto al abrir (confirmado en vivo), pero
      // se intenta el click igual por si algun otro tipo de licitacion no lo trae preseleccionado.
      const tabResumen = paginaDeBusqueda.locator('text=/Resumen de ofertas/i').first();
      if (await tabResumen.isVisible({ timeout: 3000 }).catch(() => false)) {
        await tabResumen.click();
        await esperarConDelay(1500);
      }

      // Se prueba en la pagina principal (o la ventana nueva) y, si no aparece ahi, en cada uno
      // de sus frames -- cubre el caso de que el cuadro se renderice dentro de un iframe interno.
      let resultado = await paginaDeBusqueda.evaluate(buscarTablaEnDocumento);
      if (!resultado.encontrada) {
        for (const frame of paginaDeBusqueda.frames ? paginaDeBusqueda.frames() : []) {
          const resultadoFrame = await frame.evaluate(buscarTablaEnDocumento).catch(() => ({ encontrada: false, filas: [] }));
          if (resultadoFrame.encontrada) { resultado = resultadoFrame; break; }
        }
      }

      if (!resultado.encontrada) {
        console.log('[CUADRO-OFERTAS] Se hizo click pero no se encontro la tabla de ofertas esperada (ni en la pagina ni en sus frames) -- posible cambio de estructura del sitio.');
        await screenshotOnError(paginaDeBusqueda, carpetaDestino, 'cuadro-ofertas-tabla-no-encontrada');
        await cerrarCuadroOfertas(paginaDeBusqueda, huboVentanaNueva);
        return { ofertas: [], error: 'Tabla de ofertas no encontrada', estructuraCambio: true };
      }

      console.log(`[CUADRO-OFERTAS] ${resultado.filas.length} ofertas encontradas.`);
      resultado.filas.forEach(o => console.log(`  ${o.nombreProveedor} | ${o.montoOferta ?? '-'} | ${o.estadoOferta}`));

      // Critico: si el cuadro es un modal en la misma pagina (no ventana nueva), hay que cerrarlo
      // explicitamente antes de seguir -- de lo contrario queda abierto encima de la ficha y
      // bloquea el siguiente clic ("Ver Adjuntos"), causando un timeout de 30s ahi (se detecto en
      // vivo: 2 licitaciones seguidas fallaron "Ver Adjuntos" con timeout justo despues de que
      // esta funcion encontrara datos correctamente, disparando un falso "cupo agotado").
      await cerrarCuadroOfertas(paginaDeBusqueda, huboVentanaNueva);
      return { ofertas: resultado.filas };

    } catch (e) {
      console.log(`[CUADRO-OFERTAS] ERROR (intento ${intento}): ${e.message}`);
      if (paginaNuevaAbierta) {
        await paginaNuevaAbierta.close().catch(() => {});
      } else {
        // Puede haber quedado el modal en pantalla incluso tras un error -- se intenta cerrar
        // igual (best-effort) para no bloquear el siguiente paso del ciclo (Ver Adjuntos).
        await fichaPage.keyboard.press('Escape').catch(() => {});
      }
      if (intento < MAX_REINTENTOS) {
        await esperarConDelay(2000);
        continue;
      }
      await screenshotOnError(fichaPage, carpetaDestino, `cuadro-ofertas-error-${Date.now()}`);
      return { ofertas: [], error: e.message };
    }
  }

  return { ofertas: [], error: 'Maximos reintentos excedidos' };
}

/**
 * Cierra el Cuadro de Ofertas: si abrio como ventana nueva, la cierra directamente. Si fue un
 * modal en la misma pagina, se cierra con el boton real del dialog -- confirmado en vivo con
 * claude-in-chrome que el modal es un Telerik RadWindow (clase "rwCloseButton", sin id propio;
 * #btnClose era una suposicion incorrecta que nunca cerraba nada, dejando el modal abierto y
 * bloqueando el siguiente clic sobre la ficha -- eso causaba el timeout de 30s en "Ver Adjuntos"
 * que se veia despues). Escape queda como respaldo por si algun otro tipo de licitacion usa un
 * widget de modal distinto.
 */
async function cerrarCuadroOfertas(paginaDeBusqueda, huboVentanaNueva) {
  if (huboVentanaNueva) {
    await paginaDeBusqueda.close().catch(() => {});
    return;
  }

  const btnClose = paginaDeBusqueda.locator('.rwCloseButton').first();
  const cerrado = await btnClose.click({ timeout: 2000 }).then(() => true).catch(() => false);
  if (!cerrado) {
    await paginaDeBusqueda.keyboard.press('Escape').catch(() => {});
  }
  await esperarConDelay(500);
}
