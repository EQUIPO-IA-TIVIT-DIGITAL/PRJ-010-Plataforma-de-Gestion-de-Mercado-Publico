#!/usr/bin/env node
// Valida el mecanismo COMPLETO de AdjuntosHttpExtractor.cs usando solo fetch() (sin Playwright
// después del login) — replica exactamente: GET ficha -> extraer enc -> GET listado -> parsear
// hidden fields -> POST descarga. Si esto descarga un PDF real, el diseño C# es correcto.
//
// Uso: MP_RUT=... MP_PASSWORD=... node spike-validacion-http-pura.js <codigoLicitacion>

import { fileURLToPath } from 'url';
import path from 'path';
import fs from 'fs';
import dotenv from 'dotenv';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
dotenv.config({ path: path.join(__dirname, '.env'), override: true });
dotenv.config({ path: path.join(__dirname, '..', '..', '.env') });

import { launch, close } from './modulos/browser.js';
import { login } from './modulos/login.js';

const HEADLESS = process.env.MP_HEADLESS !== 'false';
const CODIGO = process.argv[2] || '2153-41-LP26';

function log(...args) { console.error(...args); }

function extraerEncToken(html) {
  const decoded = html.replace(/&#39;/g, "'").replace(/&amp;/g, '&');
  const m = decoded.match(/open\('(\.\.\/Attachment\/ViewAttachment\.aspx\?enc=[^']+)'/i);
  if (!m) return null;
  const enc = m[1].match(/enc=([^'"&]+)/i);
  return enc ? enc[1] : null;
}

function extraerCamposOcultos(html) {
  const campos = {};
  const re = /<input[^>]*type=["']hidden["'][^>]*>/gi;
  let m;
  while ((m = re.exec(html))) {
    const tag = m[0];
    const name = (tag.match(/name=["']([^"']+)["']/i) || [])[1];
    const value = (tag.match(/value=["']([^"']*)["']/i) || [])[1] || '';
    if (name) campos[name] = value;
  }
  return campos;
}

function extraerFilas(html) {
  const tableMatch = html.match(/<table[^>]*id=["']DWNL_grdId["'][^>]*>([\s\S]*?)<\/table>/i);
  if (!tableMatch) return [];
  const rows = [...tableMatch[1].matchAll(/<tr[^>]*>([\s\S]*?)<\/tr>/gi)].slice(1); // skip header
  const filas = [];
  for (const row of rows) {
    const cells = [...row[1].matchAll(/<td[^>]*>([\s\S]*?)<\/td>/gi)].map(c => c[1].replace(/<[^>]+>/g, '').trim());
    if (cells.length < 7) continue;
    const btnMatch = row[1].match(/<input[^>]*type=["']image["'][^>]*>/i);
    const name = btnMatch ? (btnMatch[0].match(/name=["']([^"']+)["']/i) || [])[1] : null;
    if (!name) continue;
    filas.push({ nombre: cells[1], tipo: cells[2], botonNombre: name });
  }
  return filas;
}

async function main() {
  let browser, context, page;
  try {
    const launched = await launch(HEADLESS);
    browser = launched.browser; context = launched.context; page = launched.page;
    await login(page, context);

    const cookies = await context.cookies();
    const cookieHeader = cookies.map(c => `${c.name}=${c.value}`).join('; ');
    const commonHeaders = { Cookie: cookieHeader, 'User-Agent': 'Mozilla/5.0' };

    log(`[VALIDACION] Paso 1: GET ficha por codigo ${CODIGO}...`);
    const fichaUrl = `https://www.mercadopublico.cl/Procurement/Modules/RFB/DetailsAcquisition.aspx?idlicitacion=${CODIGO}`;
    const fichaResp = await fetch(fichaUrl, { headers: commonHeaders });
    const fichaHtml = await fichaResp.text();
    log(`[VALIDACION]   status=${fichaResp.status}`);

    const enc = extraerEncToken(fichaHtml);
    if (!enc) throw new Error('No se pudo extraer el token enc');
    log(`[VALIDACION] Paso 2: token enc extraido (${enc.length} chars)`);

    log('[VALIDACION] Paso 3: GET listado de adjuntos...');
    const listadoUrl = `https://www.mercadopublico.cl/Procurement/Modules/Attachment/ViewAttachment.aspx?enc=${enc}`;
    const listadoResp = await fetch(listadoUrl, { headers: { ...commonHeaders, Referer: fichaResp.url }, redirect: 'follow' });
    const listadoHtml = await listadoResp.text();
    const listadoUrlFinal = listadoResp.url;
    log(`[VALIDACION]   status=${listadoResp.status} finalUrl=${listadoUrlFinal.substring(0, 100)} len=${listadoHtml.length}`);

    const camposOcultos = extraerCamposOcultos(listadoHtml);
    const filas = extraerFilas(listadoHtml);
    log(`[VALIDACION]   Campos ocultos: ${Object.keys(camposOcultos).join(', ')}`);
    log(`[VALIDACION]   Filas: ${filas.length} — ${filas.map(f => f.tipo).join(' | ')}`);
    if (filas.length === 0) {
      log('[VALIDACION]   DIAGNOSTICO — tieneDWNL_grdId=' + listadoHtml.includes('DWNL_grdId') + ' tieneTable=' + /<table/i.test(listadoHtml));
      const outDiag = path.join(__dirname, 'descargas', 'diagnostico-listado.html');
      fs.mkdirSync(path.dirname(outDiag), { recursive: true });
      fs.writeFileSync(outDiag, listadoHtml);
      log('[VALIDACION]   HTML completo guardado en ' + outDiag + ' para inspeccion');
    }

    const acta = filas.find(f => f.tipo.toLowerCase().includes('acta de evaluaci')) || filas[0];
    if (!acta) throw new Error('Sin filas de adjuntos');
    log(`[VALIDACION] Paso 4: POST descarga de "${acta.nombre}" (boton: ${acta.botonNombre})...`);

    const body = new URLSearchParams();
    for (const [k, v] of Object.entries(camposOcultos)) body.append(k, v);
    body.append(`${acta.botonNombre}.x`, '1');
    body.append(`${acta.botonNombre}.y`, '1');

    const postResp = await fetch(listadoUrlFinal, {
      method: 'POST',
      headers: { ...commonHeaders, 'Content-Type': 'application/x-www-form-urlencoded', Referer: listadoUrlFinal },
      body: body.toString(),
      redirect: 'follow',
    });

    const contentType = postResp.headers.get('content-type') || '';
    const contentDisposition = postResp.headers.get('content-disposition') || '';
    log(`[VALIDACION]   status=${postResp.status} content-type=${contentType} content-disposition=${contentDisposition}`);

    if (contentType.includes('pdf') || contentDisposition.includes('.pdf')) {
      const buf = Buffer.from(await postResp.arrayBuffer());
      const outPath = path.join(__dirname, 'descargas', `validacion-${CODIGO.replace(/[^a-z0-9]/gi, '')}.pdf`);
      fs.mkdirSync(path.dirname(outPath), { recursive: true });
      fs.writeFileSync(outPath, buf);
      log(`[VALIDACION] ✅ EXITO: PDF real descargado (${buf.length} bytes) -> ${outPath}`);
      log(`[VALIDACION] Primeros bytes: ${buf.subarray(0, 5).toString('ascii')} (debe ser %PDF-)`);
    } else {
      const text = await postResp.text();
      log(`[VALIDACION] ❌ No parece un PDF. Primeros 500 chars de la respuesta:`);
      log(text.substring(0, 500));
    }
  } catch (e) {
    log(`[VALIDACION] ERROR: ${e.message}`);
    log(e.stack);
  } finally {
    await close(browser, context, page);
  }
}

main();
