import fs from 'fs';
import path from 'path';

export function crearCarpetaLote(basePath) {
  const hoy = new Date();
  const fecha = `${hoy.getFullYear()}-${String(hoy.getMonth() + 1).padStart(2, '0')}-${String(hoy.getDate()).padStart(2, '0')}`;
  const carpeta = path.join(basePath, `lote-${fecha}`);

  if (!fs.existsSync(carpeta)) {
    fs.mkdirSync(carpeta, { recursive: true });
  }

  console.log(`[STORAGE] Carpeta de lote: ${carpeta}`);
  return carpeta;
}

export function crearCarpetaLicitacion(carpetaLote, codigo) {
  const codigoLimpio = (codigo || 'sin-codigo').replace(/[\/\\?%*:|"<>]/g, '_').trim().substring(0, 80);
  const carpeta = path.join(carpetaLote, codigoLimpio);

  if (!fs.existsSync(carpeta)) {
    fs.mkdirSync(carpeta, { recursive: true });
  }

  return carpeta;
}

export function guardarDatosLicitacion(carpetaLicitacion, datos) {
  const archivo = path.join(carpetaLicitacion, 'datos.json');
  const datosGuardar = {
    ...datos,
    fechaExtraccion: datos.fechaExtraccion || new Date().toISOString(),
  };

  fs.writeFileSync(archivo, JSON.stringify(datosGuardar, null, 2));
  console.log(`[STORAGE] Datos guardados: ${archivo}`);
  return archivo;
}

export function guardarResumen(carpetaLote, licitaciones) {
  const archivo = path.join(carpetaLote, 'resumen.json');

  const exitosas = licitaciones.filter(l => l.estado === 'completo' && l.actaDescargada);
  const erroresActa = licitaciones.filter(l => l.estado === 'completo' && !l.actaDescargada);
  const errores = licitaciones.filter(l => l.estado === 'error');

  const resumen = {
    lote: path.basename(carpetaLote),
    fechaEjecucion: new Date().toISOString(),
    configuracion: {
      fechaDesde: process.env.MP_FECHA_DESDE || '01/01/2026',
      estado: 'Adjudicada',
      region: 'Todas',
    },
    estadisticas: {
      totalLicitaciones: licitaciones.length,
      exitosas: exitosas.length,
      sinActa: erroresActa.length,
      conError: errores.length,
    },
    licitaciones: licitaciones.map(l => ({
      codigo: l.codigo || '',
      nombre: l.nombre || '',
      descripcion: (l.descripcion || '').substring(0, 200),
      demandante: l.demandante || '',
      fechaPublicacion: l.fechaPublicacion || l.fechas?.publicacion || '',
      fechaCierre: l.fechaCierre || l.fechas?.cierre || '',
      estado: l.estado || '',
      actaEvaluacion: l.actaEvaluacion || null,
      actaDescargada: l.actaDescargada || false,
      errorActa: l.error || l.errorActa || null,
      urlFicha: l.urlFicha || '',
      carpeta: l.carpetaLicitacion || '',
    })),
  };

  fs.writeFileSync(archivo, JSON.stringify(resumen, null, 2));
  console.log(`[STORAGE] Resumen guardado: ${archivo}`);

  return resumen;
}

export function cargarResumen(carpetaLote) {
  const archivo = path.join(carpetaLote, 'resumen.json');
  if (!fs.existsSync(archivo)) {
    return null;
  }

  try {
    const contenido = fs.readFileSync(archivo, 'utf-8');
    return JSON.parse(contenido);
  } catch (e) {
    console.log(`[STORAGE] Error cargando resumen: ${e.message}`);
    return null;
  }
}

export function guardarReporteTexto(carpetaLote, resumen) {
  const archivo = path.join(carpetaLote, 'reporte.txt');

  const lineas = [];
  lineas.push('='.repeat(70));
  lineas.push('REPORTE - AGENTE MERCADO PUBLICO');
  lineas.push('='.repeat(70));
  lineas.push(`Fecha: ${resumen.fechaEjecucion}`);
  lineas.push(`Lote: ${resumen.lote}`);
  lineas.push(`Configuración: Desde=${resumen.configuracion.fechaDesde}, Estado=${resumen.configuracion.estado}, Región=${resumen.configuracion.region}`);
  lineas.push('');
  lineas.push('ESTADÍSTICAS');
  lineas.push('-'.repeat(40));
  lineas.push(`Total licitaciones: ${resumen.estadisticas.totalLicitaciones}`);
  lineas.push(`Con Acta descargada: ${resumen.estadisticas.exitosas}`);
  lineas.push(`Sin Acta de Evaluación: ${resumen.estadisticas.sinActa}`);
  lineas.push(`Con error: ${resumen.estadisticas.conError}`);
  lineas.push('');
  lineas.push('DETALLE DE LICITACIONES');
  lineas.push('-'.repeat(40));

  for (const lic of resumen.licitaciones) {
    lineas.push('');
    lineas.push(`  Código: ${lic.codigo || 'N/A'}`);
    lineas.push(`  Nombre: ${lic.nombre || 'N/A'}`);
    lineas.push(`  Demandante: ${lic.demandante || 'N/A'}`);
    lineas.push(`  Publicación: ${lic.fechaPublicacion || 'N/A'}`);
    lineas.push(`  Cierre: ${lic.fechaCierre || 'N/A'}`);
    lineas.push(`  Acta: ${lic.actaDescargada ? '✓ Descargada' : '✗ ' + (lic.errorActa || 'Sin Acta')}`);
    lineas.push(`  Estado: ${lic.estado}`);
  }

  lineas.push('');
  lineas.push('='.repeat(70));
  lineas.push('FIN DEL REPORTE');
  lineas.push('='.repeat(70));

  fs.writeFileSync(archivo, lineas.join('\n'));
  console.log(`[STORAGE] Reporte guardado: ${archivo}`);
  return archivo;
}