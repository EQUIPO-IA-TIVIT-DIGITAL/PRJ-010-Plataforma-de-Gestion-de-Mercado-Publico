import fs from 'fs';
import path from 'path';
import pdfParse from 'pdf-parse';
import mammoth from 'mammoth';
import XLSX from 'xlsx';

export async function extraerTexto(rutaArchivo) {
  const ext = path.extname(rutaArchivo).toLowerCase();
  const nombre = path.basename(rutaArchivo);

  console.log(`Procesando: ${nombre}`);

  try {
    switch (ext) {
      case '.pdf':
        return await extraerPDF(rutaArchivo);
      case '.docx':
        return await extraerDOCX(rutaArchivo);
      case '.doc':
        console.log(`  ⚠️  Formato .doc antiguo, omitiendo: ${nombre}`);
        return null;
      case '.xls':
      case '.xlsx':
        return await extraerExcel(rutaArchivo);
      default:
        console.log(`  ⚠️  Formato no soportado: ${ext}`);
        return null;
    }
  } catch (error) {
    console.error(`  ❌ Error procesando ${nombre}:`, error.message);
    return null;
  }
}

async function extraerPDF(ruta) {
  const buffer = fs.readFileSync(ruta);
  const data = await pdfParse(buffer);
  return data.text;
}

async function extraerDOCX(ruta) {
  const result = await mammoth.extractRawText({ path: ruta });
  return result.value;
}

async function extraerExcel(ruta) {
  const workbook = XLSX.readFile(ruta);
  let texto = '';

  for (const sheetName of workbook.SheetNames) {
    const sheet = workbook.Sheets[sheetName];
    const data = XLSX.utils.sheet_to_json(sheet, { header: 1 });
    texto += `=== Hoja: ${sheetName} ===\n`;
    texto += data.map(row => row.join(' | ')).join('\n');
    texto += '\n\n';
  }

  return texto;
}

export function listarDocumentos(carpeta) {
  const archivos = fs.readdirSync(carpeta);
  const documentos = archivos
    .filter(f => {
      const ext = path.extname(f).toLowerCase();
      return ['.pdf', '.docx', '.doc', '.xls', '.xlsx'].includes(ext);
    })
    .map(f => ({
      nombre: f,
      ruta: path.join(carpeta, f),
      tipo: clasificarDocumento(f)
    }));

  return documentos;
}

function clasificarDocumento(nombre) {
  const n = nombre.toLowerCase();

  if (n.includes('oferta') && n.includes('tecnica')) return 'OFERTA_TECNICA';
  if (n.includes('oferta') && (n.includes('economica') || n.includes('precio'))) return 'OFERTA_ECONOMICA';
  if (n.includes('oferta')) return 'OFERTA';
  if (n.includes('base') || n.includes('licitacion') || n.includes('housing')) return 'BASES';
  if (n.includes('resolucion') || n.includes('rex')) return 'RESOLUCION';
  if (n.includes('certificacion') || n.includes('certificado')) return 'CERTIFICACION';
  if (n.includes('anexo')) return 'ANEXO';
  if (n.includes('rut')) return 'DOCUMENTO_LEGAL';
  if (n.includes('vigencia')) return 'DOCUMENTO_LEGAL';

  return 'OTRO';
}
