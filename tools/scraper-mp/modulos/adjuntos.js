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
      const adjUrl = adjuntosPage.url();
      console.log(`[ADJUNTOS] Ventana: ${adjUrl.substring(0, 120)}`);

      if (adjUrl.includes('403') || adjUrl.includes('.html') || adjUrl.includes('error')) {
        console.log(`[ADJUNTOS] Error en ventana (${adjUrl.includes('403') ? '403' : 'redireccion'}). Cerrando y reintentando...`);
        await adjuntosPage.close().catch(() => {});

        if (intento < MAX_REINTENTOS) {
          await esperarConDelay(3000);
          continue;
        }
        return { actaEvaluacion: null, actaDescargada: false, error: 'Maximos reintentos excedido', todosAdjuntos: [] };
      }

      await adjuntosPage.waitForLoadState('domcontentloaded', { timeout: 20000 }).catch(() => {});
      await esperarConDelay(2000);

      const actaInfo = await adjuntosPage.evaluate(() => {
        const table = document.getElementById('DWNL_grdId');
        if (!table) return null;

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

        return resultados;
      });

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
      if (intento < MAX_REINTENTOS) {
        await esperarConDelay(3000);
        continue;
      }
      return { actaEvaluacion: null, actaDescargada: false, error: e.message, todosAdjuntos: [] };
    }
  }

  return { actaEvaluacion: null, actaDescargada: false, error: 'Maximos reintentos excedido', todosAdjuntos: [] };
}
