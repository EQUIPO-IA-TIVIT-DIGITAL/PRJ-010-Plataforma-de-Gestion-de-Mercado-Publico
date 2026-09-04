import { screenshotOnError, esperarConDelay, clickHumano } from './browser.js';
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
      const imgAdjuntos = fichaPage.locator('#imgAdjuntos');
      await imgAdjuntos.waitFor({ state: 'visible', timeout: 15000 }).catch(() => {});

      // 1. Click con emulación humana para elevar score de reCAPTCHA Enterprise
      const popupPromise = context.waitForEvent('page', { timeout: 15000 }).catch(() => null);
      await clickHumano(fichaPage, imgAdjuntos, 400);
      adjuntosPage = await popupPromise;

      if (!adjuntosPage) {
        const pages = context.pages();
        if (pages.length > 1) {
          adjuntosPage = pages[pages.length - 1];
        }
      }

      // 2. Fallback preservando opener/referer en el contexto de la ficha
      if (!adjuntosPage || adjuntosPage === fichaPage) {
        const urlRelativa = await fichaPage.evaluate(() => {
          const el = document.getElementById('imgAdjuntos');
          if (!el) return null;
          const onclick = el.getAttribute('onclick') || '';
          const match = onclick.match(/open\(['"]([^'"]+)['"]/);
          return match && match[1] ? match[1] : null;
        });

        if (urlRelativa) {
          console.log(`[ADJUNTOS] Fallback: disparando window.open en contexto de ficha...`);
          const fallbackPopupPromise = context.waitForEvent('page', { timeout: 15000 }).catch(() => null);
          await fichaPage.evaluate((u) => { window.open(u, 'MercadoPublicoPopup'); }, urlRelativa);
          adjuntosPage = await fallbackPopupPromise;
        }
      }

      if (!adjuntosPage || adjuntosPage === fichaPage) {
        console.log('[ADJUNTOS] No se abrio nueva ventana de adjuntos');
        if (intento < MAX_REINTENTOS) { await esperarConDelay(2000); continue; }
        return { actaEvaluacion: null, actaDescargada: false, error: 'Sin ventana adjuntos', todosAdjuntos: [] };
      }

      await adjuntosPage.waitForURL(url => url.href.includes('Attachment') || url.href.includes('ViewAttachment'), { timeout: 20000 }).catch(() => {});
      await adjuntosPage.waitForLoadState('domcontentloaded', { timeout: 20000 }).catch(() => {});
      await esperarConDelay(1500);

      let adjUrl = adjuntosPage.url();
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

      // Deteccion de bloqueo del robot (robot.png / Acceso denegado)
      const robotBlockDetected = await adjuntosPage.evaluate(() => {
        const hasRobotImg = document.querySelector('img[src*="robot.png"]') !== null;
        const hasAccesoDenegado = document.body.innerText.toLowerCase().includes('acceso denegado');
        const isAccessDeniedTitle = document.title.toLowerCase().includes('acceso denegado');
        return hasRobotImg || hasAccesoDenegado || isAccessDeniedTitle;
      });

      if (robotBlockDetected) {
        console.log('[ADJUNTOS] DETECTADA PANTALLA DE BLOQUEO DE ROBOT (Acceso Denegado).');
        await screenshotOnError(adjuntosPage, carpetaDestino, 'adjuntos-bloqueo-robot');
        await adjuntosPage.close().catch(() => {});
        const err = new Error('Bloqueo anti-bot de Mercado Publico detectado (Acceso Denegado - robot.png)');
        err.isRobotBlock = true;
        throw err;
      }

      // Esperar activamente si hay alguna tabla o indicador de carga
      await adjuntosPage.waitForSelector('table, #DWNL_grdId, body', { timeout: 8000 }).catch(() => {});

      const evalResult = await adjuntosPage.evaluate(() => {
        // 1. Localizar tabla de adjuntos por selectores prioritarios
        let table = document.getElementById('DWNL_grdId')
          || document.querySelector('table[id*="DWNL_grdId"]')
          || document.querySelector('table[id*="grdId"]')
          || document.querySelector('table[id*="DWNL"]')
          || document.querySelector('table[id*="Adjuntos"]');

        // 2. Si no encontró por ID, buscar tablas con inputs tipo imagen o de descarga
        if (!table) {
          const allTables = Array.from(document.querySelectorAll('table'));
          table = allTables.find(t =>
            t.querySelector('input[type="image"]') ||
            t.querySelector('input[name*="DWNL"]') ||
            t.querySelector('input[id*="DWNL"]')
          );
        }

        const bodyText = (document.body?.innerText || '').toLowerCase();
        const noAdjuntosKeywords = [
          'no se encontraron registros',
          'no existen registros',
          'no existen archivos adjuntos',
          'sin archivos',
          'sin adjuntos',
          'no registra adjuntos',
          '0 registros'
        ];
        const tieneMensajeVacio = noAdjuntosKeywords.some(kw => bodyText.includes(kw));

        if (!table) {
          if (tieneMensajeVacio || !document.querySelector('input[type="image"]')) {
            return { table: false, canary: false, rows: [] };
          }
          return { table: false, canary: true, rows: null };
        }

        const rows = table.querySelectorAll('tr');
        if (rows.length <= 1) {
          return { table: true, canary: false, rows: [] };
        }

        const resultados = [];
        for (let i = 1; i < rows.length; i++) {
          const cells = rows[i].querySelectorAll('td');
          if (cells.length < 2) continue;

          const verBtn = rows[i].querySelector('input[type="image"]')
            || rows[i].querySelector('input[type="submit"]')
            || rows[i].querySelector('input[type="button"]')
            || rows[i].querySelector('a');

          const btnId = verBtn?.id || verBtn?.getAttribute('name') || '';

          let nombre = '';
          let tipo = '';
          let descripcion = '';
          let tamanio = '';
          let fecha = '';

          if (cells.length >= 7) {
            nombre = cells[1]?.textContent?.trim() || '';
            tipo = cells[2]?.textContent?.trim() || '';
            descripcion = cells[3]?.textContent?.trim() || '';
            tamanio = cells[4]?.textContent?.trim() || '';
            fecha = cells[5]?.textContent?.trim() || '';
          } else {
            nombre = cells[0]?.textContent?.trim() || cells[1]?.textContent?.trim() || '';
            tipo = cells[1]?.textContent?.trim() || '';
          }

          if (!nombre && !btnId) continue;
          if (!nombre) nombre = `documento_${i}`;

          const esActa = tipo.toLowerCase().includes('acta') && tipo.toLowerCase().includes('evaluaci')
            || nombre.toLowerCase().includes('acta') && nombre.toLowerCase().includes('evaluaci');

          resultados.push({
            nombre,
            tipo,
            descripcion,
            tamanio,
            fecha,
            btnId,
            esActa
          });
        }

        return { table: true, canary: false, rows: resultados };
      });

      if (!evalResult.table && evalResult.canary) {
        console.log('[ADJUNTOS] Canary presente pero tabla ausente: posible cambio de estructura del sitio, no cupo agotado');
        await screenshotOnError(adjuntosPage, carpetaDestino, 'adjuntos-posible-cambio-estructura');
        await adjuntosPage.close().catch(() => {});
        const err = new Error('Estructura de la pagina de adjuntos no reconocida');
        err.isStructureChange = true;
        throw err;
      }

      const actaInfo = evalResult.rows || [];

      if (actaInfo.length === 0) {
        console.log('[ADJUNTOS] No se encontraron adjuntos en la tabla (licitación sin documentos)');
        await adjuntosPage.close().catch(() => {});
        return { actaEvaluacion: null, actaDescargada: false, error: null, todosAdjuntos: [] };
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
      if (e.isRobotBlock) {
        return { actaEvaluacion: null, actaDescargada: false, error: e.message, todosAdjuntos: [], isRobotBlock: true };
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
