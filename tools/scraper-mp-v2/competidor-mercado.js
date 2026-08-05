// spec 031 (US4): entrypoint standalone, invocado on-demand por CompetidorMercadoService
// (background, no bloquea el request HTTP) -- calcula la actividad total de mercado de un
// competidor dentro de un área de negocio + rango de fechas, incluyendo licitaciones donde
// TIVIT nunca participó. No es parte del ciclo diario de agente-mp.js.
//
// Uso: node competidor-mercado.js --competidor="Telefónica Empresas" --area=1
//        --fechaDesde=2026-01-01 --fechaHasta=2026-07-31 --palabrasClave="cloud,nube,aws"
//
// Flujo (ver research.md §4, actualización 2026-08-04):
//   1. buscarPublico.js: HTTP plano (sin Playwright) contra el buscador público de Mercado
//      Público, acotado por las palabras clave del área + rango de fechas -- devuelve códigos
//      de licitación, sin importar si TIVIT participó.
//   2. Para cada código: Playwright visita la ficha (extraerDatosLicitacion, sin modificar) y
//      extrae el Cuadro de Ofertas (extraerCuadroOfertas, sin modificar) -- igual que ya hace
//      agente-mp.js para las licitaciones de TIVIT.
//   3. Si el competidor aparece entre los oferentes, se acumula al resultado.
//   4. Se persiste en competidores_actividad_mercado (estado 'listo' o 'error').

import 'dotenv/config';
import { launch, close, esperarConDelay } from './modulos/browser.js';
import { buscarLicitacionesPublico } from './modulos/buscarPublico.js';
import { extraerDatosLicitacion, cerrarFicha } from './modulos/licitacion.js';
import { extraerCuadroOfertas } from './modulos/cuadroOfertas.js';
import { initDB, closeDB } from './modulos/db.js';

function parseArgs() {
  const args = {};
  for (const arg of process.argv.slice(2)) {
    const m = arg.match(/^--([^=]+)=(.*)$/);
    if (m) args[m[1]] = m[2];
  }
  return args;
}

function coincideCompetidor(nombreProveedor, nombreCompetidor) {
  if (!nombreProveedor) return false;
  return nombreProveedor.toLowerCase().includes(nombreCompetidor.toLowerCase());
}

async function main() {
  const args = parseArgs();
  const { competidor, area, fechaDesde, fechaHasta, palabrasClave } = args;

  if (!competidor || !fechaDesde || !fechaHasta) {
    console.error('[COMPETIDOR-MERCADO] Uso: --competidor= --fechaDesde= --fechaHasta= [--area=] [--palabrasClave=término1,término2]');
    process.exit(1);
  }

  const pool = initDB();
  const areaCodigo = area ? parseInt(area, 10) : null;
  const desde = new Date(fechaDesde);
  const hasta = new Date(fechaHasta);
  const terminos = (palabrasClave || competidor).split(',').map(t => t.trim()).filter(Boolean);

  console.log(`[COMPETIDOR-MERCADO] Buscando actividad de "${competidor}" en área ${areaCodigo ?? 'todas'}, ${fechaDesde} a ${fechaHasta}`);

  try {
    // Paso 1: universo de licitaciones candidatas, un término a la vez (deduplicado por código)
    // -- el buscador público solo acepta un texto por llamada, no un OR de keywords.
    const candidatas = new Map();
    for (const termino of terminos) {
      const resultados = await buscarLicitacionesPublico({
        textoBusqueda: termino,
        fechaDesde: desde,
        fechaHasta: hasta,
        registrosPorPagina: 100,
      });
      for (const r of resultados) candidatas.set(r.codigo, r);
    }
    console.log(`[COMPETIDOR-MERCADO] ${candidatas.size} licitaciones candidatas (universo del área+período)`);

    // Paso 2: visitar cada ficha y revisar el Cuadro de Ofertas
    const { browser, context, page } = await launch(true);
    const licitacionesConCompetidor = [];
    let cantidadLicitaciones = 0;
    let montoTotalAdjudicado = 0;

    // Cada candidata siempre trae urlFicha (buscarPublico.js la completa con una plantilla si
    // hace falta), asi que abrirFichaPopup abre su propia pestaña via context.newPage() sin
    // tocar `page` -- son independientes entre si y se pueden procesar en paralelo. Se limita la
    // concurrencia (en vez de lanzar todas a la vez) para no saturar la sesion ni Mercado Publico.
    const CONCURRENCIA = parseInt(process.env.MP_COMPETIDOR_CONCURRENCIA || '3', 10);

    async function procesarLicitacion(lic) {
      try {
        const { datos, fichaPage, isPopup } = await extraerDatosLicitacion(page, context, lic);
        if (!fichaPage) return;

        try {
          const resultCuadro = await extraerCuadroOfertas(fichaPage, context, datos, '/tmp');
          const ofertaCompetidor = (resultCuadro.ofertas || []).find(o => coincideCompetidor(o.nombreProveedor, competidor));

          if (ofertaCompetidor) {
            cantidadLicitaciones++;
            const esAdjudicado = /adjudicad/i.test(ofertaCompetidor.estadoOferta || '');
            if (esAdjudicado && ofertaCompetidor.montoOferta) montoTotalAdjudicado += ofertaCompetidor.montoOferta;

            licitacionesConCompetidor.push({
              licitacionCodigo: lic.codigo,
              nombre: datos.nombre || lic.nombre,
              montoOferta: ofertaCompetidor.montoOferta,
              estadoOferta: ofertaCompetidor.estadoOferta,
              tivitParticipo: (resultCuadro.ofertas || []).some(o => coincideCompetidor(o.nombreProveedor, 'tivit')),
            });
          }
        } finally {
          // Corregido: antes se pasaba `false` fijo aunque la ficha siempre abre como popup
          // (isPopup real de abrirFichaPopup es true) -- nunca se cerraba fichaPage, dejando
          // pestañas abiertas acumulandose durante toda la corrida.
          await cerrarFicha(page, fichaPage, isPopup);
        }
      } catch (e) {
        console.error(`[COMPETIDOR-MERCADO] Error procesando ${lic.codigo}: ${e.message}`);
      }
    }

    const cola = Array.from(candidatas.values());
    let siguiente = 0;
    async function worker() {
      while (siguiente < cola.length) {
        const lic = cola[siguiente++];
        await procesarLicitacion(lic);
        await esperarConDelay(500);
      }
    }
    await Promise.all(Array.from({ length: Math.min(CONCURRENCIA, cola.length) }, () => worker()));

    await close(browser, context, page);

    const contenidoJson = JSON.stringify({ licitaciones: licitacionesConCompetidor });

    await pool.query(
      `SELECT usp_CompetidoresActividadMercado_Guardar($1, $2, $3, $4, $5, $6, $7)`,
      [competidor, areaCodigo, fechaDesde, fechaHasta, cantidadLicitaciones, montoTotalAdjudicado, contenidoJson]
    );

    console.log(`[COMPETIDOR-MERCADO] Listo: ${cantidadLicitaciones} licitaciones, $${montoTotalAdjudicado} adjudicado`);
  } catch (e) {
    console.error(`[COMPETIDOR-MERCADO] Fallo: ${e.message}`);
    await pool.query(
      `SELECT usp_CompetidoresActividadMercado_MarcarError($1, $2, $3, $4)`,
      [competidor, areaCodigo, fechaDesde, fechaHasta]
    );
    process.exitCode = 1;
  } finally {
    await closeDB();
  }
}

main();
