import { screenshotOnError, esperarConDelay } from './browser.js';
import fs from 'fs';
import path from 'path';

const MAX_REINTENTOS = 3;

export async function descargarActaEvaluacion(fichaPage, context, datosLicitacion, carpetaDestino) {
  console.log(`\n[ADJUNTOS] Buscando Acta de Evaluacion para: ${datosLicitacion.codigo || datosLicitacion.nombre || 'sin codigo'}...`);

  if (!fs.existsSync(carpetaDestino)) {
    fs.mkdirSync(carpetaDestino, { recursive: true });
  }

  for (let intento = 1; intento <= MAX_REINTENTOS; intento++) {
    let adjuntosPage = null;

    try {
      const pagesBefore = context.pages().length;

      console.log(`[ADJUNTOS] Intento ${intento}/${MAX_REINTENTOS}: Click en "Ver Adjuntos"...`);
      const imgAdjuntos = fichaPage.locator('#imgAdjuntos');
      await imgAdjuntos.waitFor({ state: 'visible', timeout: 10000 }).catch(() => {});
      await imgAdjuntos.click();
      await esperarConDelay(3000);

      const pagesAfter = context.pages();
      if (pagesAfter.length <= pagesBefore) {
        console.log('[ADJUNTOS] No se abrio nueva ventana de adjuntos');
        if (intento < MAX_REINTENTOS) { await esperarConDelay(2000); continue; }
        return { actaEvaluacion: null, actaDescargada: false, error: 'Sin ventana adjuntos', todosAdjuntos: [] };
      }

      adjuntosPage = pagesAfter[pagesAfter.length - 1];

      // El popup puede pasar por una redireccion intermedia a 403.html antes de llegar a la
      // URL real de ViewAttachment.aspx -- leer adjuntosPage.url() justo despues del delay fijo
      // de 3s a veces captura ese estado intermedio. Se espera a que la navegacion se asiente
      // (networkidle) y se vuelve a chequear la URL una vez mas antes de declarar error.
      await adjuntosPage.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {});
      let adjUrl = adjuntosPage.url();

      if (adjUrl.includes('403')) {
        console.log(`[ADJUNTOS] URL intermedia 403, esperando posible redireccion final...`);
        await esperarConDelay(4000);
        await adjuntosPage.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {});
        adjUrl = adjuntosPage.url();
      }

      console.log(`[ADJUNTOS] Ventana: ${adjUrl.substring(0, 120)}`);

      if (adjUrl.includes('403') || adjUrl.includes('.html') || adjUrl.includes('error')) {
        console.log(`[ADJUNTOS] Error en ventana (${adjUrl.includes('403') ? '403' : 'redireccion'}). Cerrando y reintentando...`);
        await adjuntosPage.close().catch(() => {});

        if (intento < MAX_REINTENTOS) {
          await esperarConDelay(5000);
          continue;
        }
        return { actaEvaluacion: null, actaDescargada: false, error: 'Maximos reintentos excedido', todosAdjuntos: [] };
      }

      await adjuntosPage.waitForLoadState('domcontentloaded', { timeout: 20000 }).catch(() => {});
      await esperarConDelay(2000);

      const evalResult = await adjuntosPage.evaluate(() => {
        const table = document.getElementById('DWNL_grdId');

        // Canary: la palabra "adjunto" aparece en el chrome de la pagina (titulo, breadcrumb,
        // encabezado) incluso cuando la licitacion no tiene archivos que mostrar. Si el canary
        // esta presente pero el grid #DWNL_grdId no, es senal de que Mercado Publico cambio el
        // id/estructura de la tabla -- no de que se agoto el cupo (QA BUG-003). Si el canary
        // tampoco esta, no podemos distinguir con certeza: se mantiene el comportamiento previo
        // (tratarlo como "tabla vacia") para no generar falsos positivos de "cambio de sitio".
        const canary = document.body.innerText.toLowerCase().includes('adjunto');

        if (!table) return { table: false, canary, rows: null };

        const rows = table.querySelectorAll('tr');
        const resultados = [];

        for (let i = 1; i < rows.length; i++) {
          const cells = rows[i].querySelectorAll('td');
          if (cells.length < 7) continue;

          const nombre = cells[1]?.textContent?.trim() || '';
          const tipo = cells[2]?.textContent?.trim() || '';
          const descripcion = cells[3]?.textContent?.trim() || '';
          const tamanio = cells[4]?.textContent?.trim() || '';
          const fecha = cells[5]?.textContent?.trim() || '';
          const verBtn = cells[6]?.querySelector('input[type="image"]');
          const btnId = verBtn?.id || '';

          resultados.push({ nombre, tipo, descripcion, tamanio, fecha, btnId, esActa: tipo === 'Acta de Evaluaci\u00f3n' });
        }

        return { table: true, canary, rows: resultados };
      });

      if (!evalResult.table && evalResult.canary) {
        console.log('[ADJUNTOS] Canary presente pero #DWNL_grdId ausente: posible cambio de estructura del sitio, no cupo agotado');
        await screenshotOnError(adjuntosPage, carpetaDestino, 'adjuntos-posible-cambio-estructura');
        await adjuntosPage.close().catch(() => {});
        const err = new Error('Estructura de la pagina de adjuntos cambio (grid #DWNL_grdId ausente con canary presente)');
        err.isStructureChange = true;
        throw err;
      }

      const actaInfo = evalResult.rows;

      if (!actaInfo || actaInfo.length === 0) {
        console.log('[ADJUNTOS] No se encontraron adjuntos en la tabla');
        const bodyPreview = await adjuntosPage.evaluate(() => document.body.innerText.substring(0, 500)).catch(() => '');
        console.log(`[ADJUNTOS] Body: ${bodyPreview.substring(0, 300)}`);
        await screenshotOnError(adjuntosPage, carpetaDestino, 'adjuntos-tabla-vacia');
        await adjuntosPage.close().catch(() => {});
        return { actaEvaluacion: null, actaDescargada: false, error: 'Tabla vacia', todosAdjuntos: [] };
      }

      console.log(`[ADJUNTOS] ${actaInfo.length} adjuntos encontrados:`);
      actaInfo.forEach(a => console.log(`  ${a.esActa ? '\u2605' : ' '} [${a.tipo}] ${a.nombre}`));

      let acta = actaInfo.find(a => a.esActa);

      if (!acta) {
        acta = actaInfo.find(a =>
          a.nombre.toLowerCase().includes('acta') && a.nombre.toLowerCase().includes('evaluaci')
        );
        if (acta) {
          console.log(`[ADJUNTOS] Acta encontrada por nombre (fallback): "${acta.nombre}"`);
        }
      }

      if (!acta) {
        console.log('[ADJUNTOS] No se encontro Acta de Evaluacion');
        await adjuntosPage.close().catch(() => {});
        return {
          actaEvaluacion: null, actaDescargada: false, error: 'Sin Acta de Evaluacion',
          todosAdjuntos: actaInfo.map(a => ({ nombre: a.nombre, tipo: a.tipo, descripcion: a.descripcion, tamanio: a.tamanio })),
        };
      }

      console.log(`[ADJUNTOS] Acta encontrada: "${acta.nombre}" (${acta.tamanio})`);
      console.log(`[ADJUNTOS] Click en "Ver" para descargar...`);

      const downloadPromise = adjuntosPage.waitForEvent('download', { timeout: 30000 }).catch(e => null);
      const verBtn = adjuntosPage.locator(`#${acta.btnId}`);
      await verBtn.click();

      const download = await downloadPromise;

      if (!download) {
        console.log('[ADJUNTOS] No se recibio evento de descarga');
        await adjuntosPage.close().catch(() => {});
        return {
          actaEvaluacion: null, actaDescargada: false, error: 'Sin descarga',
          todosAdjuntos: actaInfo.map(a => ({ nombre: a.nombre, tipo: a.tipo })),
        };
      }

      const fileName = download.suggestedFilename() || `acta-evaluacion-${datosLicitacion.codigo || 'sin-codigo'}.pdf`;
      const filePath = path.join(carpetaDestino, fileName);
      await download.saveAs(filePath);
      console.log(`[ADJUNTOS] Acta descargada: ${fileName}`);

      await adjuntosPage.close().catch(() => {});

      return {
        actaEvaluacion: filePath, actaDescargada: true, nombreActa: acta.nombre, tamanio: acta.tamanio,
        todosAdjuntos: actaInfo.map(a => ({ nombre: a.nombre, tipo: a.tipo, descripcion: a.descripcion })),
      };

    } catch (e) {
      console.log(`[ADJUNTOS] ERROR (intento ${intento}): ${e.message}`);
      if (adjuntosPage && !adjuntosPage.isClosed()) {
        await screenshotOnError(adjuntosPage, carpetaDestino, `adjuntos-error-${Date.now()}`);
        await adjuntosPage.close().catch(() => {});
      }
      // Cambio de estructura del sitio: no tiene sentido reintentar (no se va a arreglar solo
      // en unos segundos) — se corta de inmediato y se marca de forma distinguible para que
      // agente-mp.js NO lo cuente como "cupo agotado" (QA BUG-003).
      if (e.isStructureChange) {
        return { actaEvaluacion: null, actaDescargada: false, error: e.message, todosAdjuntos: [], estructuraCambio: true };
      }
      if (intento < MAX_REINTENTOS) {
        await esperarConDelay(3000);
        continue;
      }
      return { actaEvaluacion: null, actaDescargada: false, error: e.message, todosAdjuntos: [] };
    }
  }

  return { actaEvaluacion: null, actaDescargada: false, error: 'Maximos reintentos excedido', todosAdjuntos: [] };
}
