#!/usr/bin/env node
/**
 * Recuperación: corre el pipeline IA sobre actas ya descargadas.
 * Uso: node recuperar-pipeline-ia.mjs
 */

import { fileURLToPath } from 'url';
import path from 'path';
import dotenv from 'dotenv';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

dotenv.config({ path: path.join(__dirname, '.env'), override: true });
dotenv.config({ path: path.join(__dirname, '..', '..', '.env') });

import { pipelineAnalisisCompleto } from './modulos/api-client.js';

const LOTE_DIR = path.join(__dirname, 'descargas', 'lote-2026-06-24');

// Only the 6 that failed (workspaces 6, 7, 9, 11 are already completado)
const licitaciones = [
  { id: 2,  codigo: '869591-6-LR26',   nombre: 'CONTRATACIÓN DE CRÉDITOS DE NUBE PÚBLICA DE AWS PA',       pdf: 'Informe_de_Evaluación_Licitación_869591-6-LR26_(firmado).pdf' },
  { id: 3,  codigo: '1605-23-LP26',    nombre: 'LP-4609 Servicio de Ciberinteligencia por 36 meses',       pdf: 'LP-4609_ITE_Informe-Evaluación-Licitación_ID_1605-23-LP26_v5_Fdo.pdf' },
  { id: 4,  codigo: '548874-36-I226',  nombre: 'SERVICIO DE INFRAESTRUCTURA COMO SERVICIO IAAS',           pdf: 'ACTA.pdf' },
  { id: 5,  codigo: '897096-5-LR26',   nombre: 'Contratación Creditos para Nube Publica de Google',        pdf: 'Informe_de_evaluación_firmado_Creditos_Google.pdf' },
  { id: 8,  codigo: '869591-2-LP26',   nombre: 'Desarrollo de la Nueva Plataforma PAC',                    pdf: 'Informe_Comisión_evaluadora_ID_869591-2-LP26.pdf' },
  { id: 10, codigo: '1002584-16-LR25', nombre: 'CLOUD MICROSOFT AZURE PARA LA DEP',                        pdf: 'REX_N°280_DECLÁRESE_INADMISIBLE_LA_OFERTA_QUE_SE_INDICA__Y_ADJUDI.pdf' },
];

let ok = 0, fail = 0;

for (const lic of licitaciones) {
  const pdfPath = path.join(LOTE_DIR, lic.codigo, lic.pdf);
  const result = await pipelineAnalisisCompleto(lic.id, lic.nombre, pdfPath, lic.pdf);
  if (result.error) {
    console.log(`FAIL [${lic.codigo}]: ${result.error}`);
    fail++;
  } else {
    console.log(`OK   [${lic.codigo}]: workspace=${result.workspaceId} doc=${result.documentoId}`);
    ok++;
  }
  await new Promise(r => setTimeout(r, 1000));
}

console.log(`\nResultado: ${ok} OK, ${fail} FAIL`);
