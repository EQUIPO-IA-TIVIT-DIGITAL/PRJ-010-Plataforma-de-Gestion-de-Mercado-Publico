// 029-fix-hallazgos-code-review-competidores-alertas (FR-005/US5): test real (Playwright
// headless, no mockeado) de buscarTablaEnDocumento contra fixtures HTML con distintos ordenes
// de columnas -- confirma que un layout reordenado ya no corrompe monto/estado.
//
// Uso: node test-cuadroOfertas.mjs

import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { buscarTablaEnDocumento } from './modulos/cuadroOfertas.js';

function htmlConTabla(filasHtml, encabezadoHtml) {
  return `<!DOCTYPE html><html><body>
    <table>
      <tr>${encabezadoHtml}</tr>
      ${filasHtml}
    </table>
  </body></html>`;
}

const ENCABEZADO_ORDEN_REAL = '<th>Rut Proveedor</th><th>Proveedor</th><th>Nombre Oferta</th><th>Total Oferta</th><th>Estado</th>';
const ENCABEZADO_ORDEN_REORDENADO = '<th>Estado</th><th>Total Oferta</th><th>Proveedor</th><th>Rut Proveedor</th>';

async function run() {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  let fallos = 0;

  async function test(nombre, fn) {
    try {
      await fn();
      console.log(`  [OK] ${nombre}`);
    } catch (err) {
      fallos++;
      console.error(`  [FAIL] ${nombre}: ${err.message}`);
    }
  }

  await test('orden real (Rut | Proveedor | Nombre Oferta | Total Oferta | Estado) extrae correctamente', async () => {
    const filas = `<tr><td>76.123.456-7</td><td>ENTEL CHILE SA</td><td>Oferta técnica</td><td>$1.234.567</td><td>Adjudicado</td></tr>`;
    await page.setContent(htmlConTabla(filas, ENCABEZADO_ORDEN_REAL));
    const resultado = await page.evaluate(buscarTablaEnDocumento);

    assert.equal(resultado.encontrada, true);
    assert.equal(resultado.filas.length, 1);
    assert.equal(resultado.filas[0].nombreProveedor, 'ENTEL CHILE SA');
    assert.equal(resultado.filas[0].montoOferta, 1234567);
    assert.equal(resultado.filas[0].estadoOferta, 'Adjudicado');
    assert.equal(resultado.filas[0].rutProveedor, '76.123.456-7');
  });

  await test('columnas reordenadas (Estado | Total Oferta | Proveedor | Rut) también extraen correctamente', async () => {
    // Antes del fix (destructuring por posición fija), esto habría asignado "Adjudicado" a
    // montoOferta y "$1.234.567" a estado -- corrompiendo los datos silenciosamente.
    const filas = `<tr><td>Adjudicado</td><td>$1.234.567</td><td>ENTEL CHILE SA</td><td>76.123.456-7</td></tr>`;
    await page.setContent(htmlConTabla(filas, ENCABEZADO_ORDEN_REORDENADO));
    const resultado = await page.evaluate(buscarTablaEnDocumento);

    assert.equal(resultado.encontrada, true);
    assert.equal(resultado.filas.length, 1, 'debe reconocer la fila aunque el orden de columnas sea distinto');
    assert.equal(resultado.filas[0].nombreProveedor, 'ENTEL CHILE SA');
    assert.equal(resultado.filas[0].montoOferta, 1234567, 'el monto debe venir de la columna "Total Oferta" real, no de una posición fija');
    assert.equal(resultado.filas[0].estadoOferta, 'Adjudicado', 'el estado debe venir de la columna "Estado" real, no de una posición fija');
  });

  await test('encabezado sin columnas reconocibles se reporta como no reconocido, no se adivina', async () => {
    const encabezadoIrreconocible = '<th>Columna X</th><th>Columna Y</th><th>Columna Z</th>';
    const filas = `<tr><td>a</td><td>b</td><td>c</td></tr>`;
    await page.setContent(htmlConTabla(filas, encabezadoIrreconocible));
    const resultado = await page.evaluate(buscarTablaEnDocumento);

    // Esta tabla no matchea el criterio de localización (requiere "proveedor" en el texto), así
    // que ni siquiera se encuentra -- comportamiento correcto (no confunde una tabla ajena).
    assert.equal(resultado.encontrada, false);
  });

  await test('tabla localizada pero con encabezado de columnas irreconocible: filas vacías, no corrompidas', async () => {
    // Cumple el criterio de localización (contiene "proveedor" y "estado" en el texto general),
    // pero el header real no tiene columnas mapeables una a una -- FR-005 exige no adivinar.
    const encabezado = '<th>Info Proveedor / Estado combinados</th>';
    const filas = `<tr><td>ENTEL CHILE SA - $1.234.567 - Adjudicado</td></tr>`;
    await page.setContent(htmlConTabla(filas, encabezado));
    const resultado = await page.evaluate(buscarTablaEnDocumento);

    assert.equal(resultado.encontrada, true);
    assert.equal(resultado.filas.length, 0, 'sin columnas mapeables, no debe inventar una fila con datos posiblemente mal asignados');
    assert.equal(resultado.encabezadoNoReconocido, true);
  });

  await browser.close();

  if (fallos > 0) {
    console.error(`\n${fallos} test(s) fallaron.`);
    process.exit(1);
  }
  console.log('\nTodos los tests de buscarTablaEnDocumento pasaron.');
}

run();
