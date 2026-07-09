import { esperarConDelay, screenshotOnError } from './browser.js';

const MAX_REINTENTOS = 2;

/**
 * 024-inteligencia-competencia-alertas / US1: extrae el listado de oferentes (no solo el
 * adjudicatario) desde el "Cuadro de Ofertas" de la ficha publica de una licitacion adjudicada
 * -- confirmado en vivo el 2026-07-09 que esta seccion es publica, sin necesitar login.
 *
 * A diferencia de "Ver Adjuntos" (adjuntos.js), el Cuadro de Ofertas se abre como un modal
 * dentro de la misma pagina (no una ventana nueva), asi que no hace falta manejar
 * context.pages() ni popups -- solo esperar a que el modal renderice su tabla.
 *
 * @param {import('playwright').Page} fichaPage - pagina ya posicionada en la ficha de la licitacion
 * @param {{codigo?: string, nombre?: string}} datosLicitacion
 * @param {string} carpetaDestino - para guardar screenshots de error
 * @returns {{ofertas: Array<{rutProveedor: string, nombreProveedor: string, montoOferta: number|null, estadoOferta: string}>, error?: string, estructuraCambio?: boolean}}
 */
export async function extraerCuadroOfertas(fichaPage, datosLicitacion, carpetaDestino) {
  console.log(`\n[CUADRO-OFERTAS] Buscando Cuadro de Ofertas para: ${datosLicitacion.codigo || datosLicitacion.nombre || 'sin codigo'}...`);

  for (let intento = 1; intento <= MAX_REINTENTOS; intento++) {
    try {
      // El icono "Cuadro de ofertas" no siempre tiene el mismo id entre licitaciones -- se
      // localiza por el texto visible debajo del icono, mas robusto que un id fijo (mismo
      // principio que el canary de adjuntos.js: preferir texto estable a estructura fragil).
      const iconoCuadroOfertas = fichaPage.locator('text=/Cuadro\\s+de\\s+ofertas/i').first();
      const visible = await iconoCuadroOfertas.isVisible({ timeout: 8000 }).catch(() => false);

      if (!visible) {
        console.log('[CUADRO-OFERTAS] No se encontro el icono "Cuadro de ofertas" en la ficha -- probablemente este tipo/estado de licitacion no lo expone (ej. Compra Agil, Trato Directo).');
        return { ofertas: [], error: 'Icono no disponible para este tipo de licitacion' };
      }

      await iconoCuadroOfertas.click();
      await esperarConDelay(2000);

      // Dentro del modal, "Resumen de ofertas" es la pestaña que trae la tabla completa con
      // RUT/Proveedor/Monto/Estado por oferente (confirmado en vivo).
      const tabResumen = fichaPage.locator('text=/Resumen de ofertas/i').first();
      if (await tabResumen.isVisible({ timeout: 5000 }).catch(() => false)) {
        await tabResumen.click();
        await esperarConDelay(1500);
      }

      const resultado = await fichaPage.evaluate(() => {
        // Busca la tabla que tenga un encabezado reconocible (Rut Proveedor / Proveedor / Total
        // Oferta / Estado) en vez de depender de un id fijo -- la pagina de Mercado Publico
        // reutiliza ids genericos entre distintos cuadros/modales.
        const tablas = Array.from(document.querySelectorAll('table'));
        const tabla = tablas.find(t => {
          const texto = t.innerText.toLowerCase();
          return texto.includes('proveedor') && (texto.includes('total oferta') || texto.includes('estado'));
        });

        if (!tabla) return { encontrada: false, filas: [] };

        const filas = Array.from(tabla.querySelectorAll('tr')).slice(1); // salta encabezado
        const resultados = [];

        for (const fila of filas) {
          const celdas = Array.from(fila.querySelectorAll('td')).map(td => td.textContent?.trim() || '');
          if (celdas.length < 4) continue;

          // Orden observado en vivo: Rut Proveedor | Proveedor | Nombre Oferta | Total Oferta | Estado
          const [rut, proveedor, , montoTexto, estado] = celdas;
          if (!proveedor) continue;

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
      });

      if (!resultado.encontrada) {
        console.log('[CUADRO-OFERTAS] Modal abierto pero no se encontro la tabla de ofertas esperada -- posible cambio de estructura del sitio.');
        await screenshotOnError(fichaPage, carpetaDestino, 'cuadro-ofertas-tabla-no-encontrada');
        return { ofertas: [], error: 'Tabla de ofertas no encontrada', estructuraCambio: true };
      }

      console.log(`[CUADRO-OFERTAS] ${resultado.filas.length} ofertas encontradas.`);
      resultado.filas.forEach(o => console.log(`  ${o.nombreProveedor} | ${o.montoOferta ?? '-'} | ${o.estadoOferta}`));

      return { ofertas: resultado.filas };

    } catch (e) {
      console.log(`[CUADRO-OFERTAS] ERROR (intento ${intento}): ${e.message}`);
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
