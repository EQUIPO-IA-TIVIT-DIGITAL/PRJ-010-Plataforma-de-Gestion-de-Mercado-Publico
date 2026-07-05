#!/usr/bin/env node

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import dotenv from 'dotenv';
import { extraerTexto, listarDocumentos } from './services/extractor.js';
import { analizarDocumento, analizarConContexto } from './services/openai-service.js';
import {
  PROMPT_ANALISIS_LICITACION,
  PROMPT_ANALISIS_OFERTA,
  PROMPT_COMPARATIVA
} from './prompts/analisis-tender.js';

dotenv.config();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const DOCUMENTS_PATH = process.env.DOCUMENTS_PATH ||
  '/home/maliaga/Documentos/Licitaciones/SERVICIO DE HOUSING - CÓDIGO: E1O2RT9';
const OUTPUT_PATH = process.env.OUTPUT_PATH || path.join(__dirname, 'resultados');

async function main() {
  console.log('='.repeat(60));
  console.log('PROCESADOR DE DOCUMENTOS DE LICITACIONES');
  console.log('='.repeat(60));
  console.log(`\nCarpeta de documentos: ${DOCUMENTS_PATH}`);
  console.log(`Carpeta de salida: ${OUTPUT_PATH}\n`);

  if (!fs.existsSync(DOCUMENTS_PATH)) {
    console.error(`❌ Carpeta no encontrada: ${DOCUMENTS_PATH}`);
    process.exit(1);
  }

  if (!fs.existsSync(OUTPUT_PATH)) {
    fs.mkdirSync(OUTPUT_PATH, { recursive: true });
  }

  // Listar documentos
  const documentos = listarDocumentos(DOCUMENTS_PATH);
  console.log(`Documentos encontrados: ${documentos.length}\n`);

  documentos.forEach((doc, i) => {
    console.log(`  ${i + 1}. [${doc.tipo}] ${doc.nombre}`);
  });

  console.log('\n' + '='.repeat(60));
  console.log('EXTRAYENDO TEXTO DE DOCUMENTOS');
  console.log('='.repeat(60) + '\n');

  // Extraer texto de todos los documentos
  const documentosConTexto = [];
  for (const doc of documentos) {
    const texto = await extraerTexto(doc.ruta);
    if (texto) {
      documentosConTexto.push({
        ...doc,
        texto: texto.substring(0, 50000) // Limitar a 50K caracteres
      });
      console.log(`  ✅ ${doc.nombre} (${texto.length} caracteres)`);
    }
  }

  console.log(`\nDocumentos procesados: ${documentosConTexto.length}/${documentos.length}\n`);

  // Guardar textos extraídos
  const textosPath = path.join(OUTPUT_PATH, 'textos-extraidos.json');
  fs.writeFileSync(textosPath, JSON.stringify(documentosConTexto, null, 2));
  console.log(`Textos guardados en: ${textosPath}\n`);

  console.log('='.repeat(60));
  console.log('ANALIZANDO CON IA');
  console.log('='.repeat(60) + '\n');

  // Identificar documentos críticos
  const bases = documentosConTexto.filter(d => d.tipo === 'BASES');
  const ofertas = documentosConTexto.filter(d => d.tipo.includes('OFERTA'));
  const otros = documentosConTexto.filter(d =>
    !d.tipo.includes('BASES') && !d.tipo.includes('OFERTA')
  );

  const resultados = {};

  // Analizar bases
  if (bases.length > 0) {
    console.log('Analizando BASES de la licitación...');
    try {
      resultados.bases = await analizarConContexto(bases, PROMPT_ANALISIS_LICITACION);
      console.log('  ✅ Bases analizadas\n');
    } catch (error) {
      console.error('  ❌ Error analizando bases:', error.message);
    }
  }

  // Analizar ofertas
  if (ofertas.length > 0) {
    console.log('Analizando OFERTAS...');
    try {
      resultados.ofertas = await analizarConContexto(ofertas, PROMPT_ANALISIS_OFERTA);
      console.log('  ✅ Ofertas analizadas\n');
    } catch (error) {
      console.error('  ❌ Error analizando ofertas:', error.message);
    }
  }

  // Análisis comparativo
  if (resultados.bases && resultados.ofertas) {
    console.log('Generando análisis COMPARATIVO...');
    try {
      const contexto = {
        bases: resultados.bases,
        ofertas: resultados.ofertas,
        documentos_adicionales: otros.map(d => ({ nombre: d.nombre, tipo: d.tipo }))
      };
      resultados.comparativa = await analizarDocumento(
        JSON.stringify(contexto, null, 2),
        PROMPT_COMPARATIVA
      );
      console.log('  ✅ Análisis comparativo generado\n');
    } catch (error) {
      console.error('  ❌ Error en análisis comparativo:', error.message);
    }
  }

  // Guardar resultados
  const resultadosPath = path.join(OUTPUT_PATH, 'analisis-completo.json');
  fs.writeFileSync(resultadosPath, JSON.stringify(resultados, null, 2));
  console.log(`Resultados guardados en: ${resultadosPath}\n`);

  // Generar resumen para dashboard
  const resumen = generarResumen(resultados);
  const resumenPath = path.join(OUTPUT_PATH, 'resumen-dashboard.json');
  fs.writeFileSync(resumenPath, JSON.stringify(resumen, null, 2));
  console.log(`Resumen para dashboard: ${resumenPath}\n`);

  console.log('='.repeat(60));
  console.log('PROCESAMIENTO COMPLETADO');
  console.log('='.repeat(60));
}

function generarResumen(resultados) {
  const resumen = {
    licitacion: {},
    criterios: [],
    requisitos: [],
    analisis: {}
  };

  if (resultados.bases) {
    resumen.licitacion = {
      nombre: resultados.bases.nombre_licitacion,
      codigo: resultados.bases.codigo_licitacion,
      tipo: resultados.bases.tipo_licitacion,
      organismo: resultados.bases.organismo_demandante,
      monto: resultados.bases.monto_estimado,
      moneda: resultados.bases.moneda,
      duracion: resultados.bases.duracion_meses,
      renovacion: resultados.bases.renovacion
    };

    resumen.criterios = resultados.bases.criterios || [];
    resumen.requisitos = [
      ...(resultados.bases.certificaciones_requeridas || []),
      ...(resultados.bases.documentos_legales || [])
    ];
  }

  if (resultados.ofertas) {
    resumen.analisis.oferta = {
      empresa: resultados.ofertas.nombre_empresa,
      solucion: resultados.ofertas.solucion_propuesta,
      certificaciones: resultados.ofertas.certificaciones_presentadas || [],
      requisitos_cumplidos: resultados.ofertas.requisitos_cumplidos || [],
      requisitos_no_cumplidos: resultados.ofertas.requisitos_no_cumplidos || []
    };
  }

  if (resultados.comparativa) {
    resumen.analisis.comparativa = {
      fortalezas: resultados.comparativa.fortalezas_oferta,
      debilidades: resultados.comparativa.debilidades_oferta,
      riesgos: resultados.comparativa.riesgos_identificados,
      recomendaciones: resultados.comparativa.recomendaciones
    };
  }

  return resumen;
}

main().catch(error => {
  console.error('Error fatal:', error);
  process.exit(1);
});
