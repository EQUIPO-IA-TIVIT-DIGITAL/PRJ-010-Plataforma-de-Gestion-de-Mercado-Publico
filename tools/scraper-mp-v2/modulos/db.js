import pg from 'pg';

const { Pool } = pg;

let pool = null;

export function initDB() {
  if (pool) return pool;

  const config = {
    host: process.env.DB_HOST || 'localhost',
    port: parseInt(process.env.DB_PORT || '5433', 10),
    user: process.env.DB_USER || 'mpm',
    password: process.env.DB_PASSWORD || 'mpm_password',
    database: process.env.DB_NAME || 'mpm',
    max: 5,
    idleTimeoutMillis: 30000,
    connectionTimeoutMillis: 10000,
  };

  console.log(`[DB] Conectando a PostgreSQL: ${config.host}:${config.port}/${config.database}`);
  pool = new Pool(config);
  return pool;
}

export async function closeDB() {
  if (pool) {
    await pool.end().catch(() => {});
    pool = null;
    console.log('[DB] Conexion cerrada');
  }
}

function mapearTipo(tipoValue) {
  if (!tipoValue) return 'Licitacion';
  const parenMatch = tipoValue.match(/\(([A-Z]+)\)/);
  if (parenMatch) {
    const code = parenMatch[1];
    if (code === 'TD') return 'TratoDirecto';
    if (code === 'CA') return 'CompraAgil';
    if (code === 'CM') return 'ConvenioMarco';
    return 'Licitacion';
  }
  if (tipoValue.includes('Licitación') || tipoValue.includes('Licitacion')) return 'Licitacion';
  if (tipoValue.includes('Trato Directo') || tipoValue.includes('TratoDirecto')) return 'TratoDirecto';
  if (tipoValue.includes('Compra Ágil') || tipoValue.includes('CompraAgil')) return 'CompraAgil';
  if (tipoValue.includes('Convenio Marco') || tipoValue.includes('ConvenioMarco')) return 'ConvenioMarco';
  return 'Licitacion';
}

function mapearMoneda(monedaValue) {
  if (!monedaValue) return 'CLP';
  const m = monedaValue.toLowerCase();
  if (m.includes('peso') || m.includes('clp')) return 'CLP';
  if (m.includes('dolar') || m.includes('usd')) return 'USD';
  if (m.includes('euro') || m.includes('eur')) return 'EUR';
  if (m.includes('uf')) return 'UF';
  if (m.includes('fomento')) return 'UF';
  return monedaValue.substring(0, 5);
}

function mapearEstado(estadoValue) {
  if (!estadoValue) return 6;
  const e = estadoValue.toLowerCase();
  if (e.includes('adjudicad')) return 5;
  if (e.includes('publicad') || e.includes('publicada')) return 1;
  if (e.includes('cerrad')) return 6;
  if (e.includes('desiert')) return 3;
  if (e.includes('revocad')) return 4;
  if (e.includes('suspendid')) return 15;
  return 6;
}

export async function upsertLicitacion(datos) {
  const p = initDB();
  const client = await p.connect();
  try {
    const tipoMapeado = mapearTipo(datos.tipo);
    const estadoMapeado = mapearEstado(datos.estado);

    const rawData = JSON.stringify({
      scraper_fecha_extraccion: datos.fechaExtraccion,
      titulo_pagina: datos.tituloPagina,
      demandante: datos.organismo?.razonSocial || datos.demandante || '',
      fechas: datos.fechas || {},
      moneda: datos.moneda || '',
      tipo_original: datos.tipo || '',
      adjuntos: datos.todosAdjuntos || [],
      raw_text_preview: (datos.rawText || '').substring(0, 50000),
      url_ficha: datos.urlFicha || '',
    });

    const fechaPub = datos.fechas?.publicacion
      ? parseFechaMP(datos.fechas.publicacion)
      : (datos.fechaPublicacion ? parseFechaMP(datos.fechaPublicacion) : null);
    const fechaCierre = datos.fechas?.cierre
      ? parseFechaMP(datos.fechas.cierre)
      : (datos.fechaCierre ? parseFechaMP(datos.fechaCierre) : null);
    const fechaAdj = datos.fechas?.adjudicacion
      ? parseFechaMP(datos.fechas.adjudicacion) : null;

    const result = await client.query(
      `CALL usp_Licitacion_UpsertFromScraper(
        $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, NULL, NULL
      )`,
      [
        datos.codigo || '',
        datos.nombre || 'Sin nombre',
        datos.descripcion || null,
        estadoMapeado,
        tipoMapeado,
        datos.organismo?.razonSocial || datos.demandante || null,
        datos.organismo?.unidad || null,
        mapearMoneda(datos.moneda),
        null,
        fechaPub,
        fechaCierre,
        fechaAdj,
        datos.urlFicha || null,
        rawData,
      ]
    );

    const p_id = result.rows[0]?.p_id || 0;
    const p_error = result.rows[0]?.p_error_msg || '';

    if (p_error && !p_error.startsWith('SYS')) {
      console.log(`[DB] Upsert exitoso: ${datos.codigo} (ID: ${p_id})`);
      return { licitacionId: p_id, error: null };
    }

    if (p_error) {
      console.log(`[DB] Error en upsert: ${p_error}`);
      return { licitacionId: null, error: p_error };
    }

    console.log(`[DB] Upsert exitoso: ${datos.codigo} (ID: ${p_id})`);
    return { licitacionId: p_id, error: null };
  } catch (e) {
    console.log(`[DB] Error en upsertLicitacion: ${e.message}`);
    return { licitacionId: null, error: e.message };
  } finally {
    client.release();
  }
}

export async function registrarAdjunto(licitacionId, adjuntoInfo) {
  const p = initDB();
  const client = await p.connect();
  try {
    const nombreArchivo = nombreArchivoDesdeRuta(adjuntoInfo.rutaStorage || adjuntoInfo.rutaLocal || '');
    const tipo = adjuntoInfo.esActa ? 'acta_evaluacion' : 'anexo';
    const grid = adjuntoInfo.grid || '';

    const result = await client.query(
      `CALL usp_Licitaciones_Adjuntos_Upsert(
        $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, NULL, NULL
      )`,
      [
        licitacionId,
        tipo,
        nombreArchivo,
        adjuntoInfo.rutaStorage || adjuntoInfo.rutaLocal || '',
        adjuntoInfo.nombre || nombreArchivo,
        adjuntoInfo.rutaLocal || null,
        adjuntoInfo.tamanioBytes || null,
        adjuntoInfo.mimeType || null,
        grid,
        !!adjuntoInfo.esActa,
        adjuntoInfo.analisisEstado || 'pendiente',
        adjuntoInfo.workspaceId || null,
      ]
    );

    const p_id = result.rows[0]?.p_id || 0;
    const p_error = result.rows[0]?.p_error_msg || '';

    if (p_error && !p_error.startsWith('SYS')) {
      return { adjuntoId: p_id, error: null };
    }
    if (p_error) {
      return { adjuntoId: null, error: p_error };
    }
    return { adjuntoId: p_id, error: null };
  } catch (e) {
    return { adjuntoId: null, error: e.message };
  } finally {
    client.release();
  }
}

export async function obtenerUltimaSync() {
  const p = initDB();
  try {
    const result = await p.query(
      `SELECT usp_ScraperSync_GetLastCompleted($1) as ultima_sync`,
      ['SCRAPER']
    );
    return result.rows[0]?.ultima_sync || '2000-01-01T00:00:00.000Z';
  } catch (e) {
    console.log(`[DB] Error obteniendo ultima sync: ${e.message}`);
    return '2000-01-01T00:00:00.000Z';
  }
}

export async function iniciarSyncLog(fechaDesde, fechaHasta) {
  const p = initDB();
  const client = await p.connect();
  try {
    const result = await client.query(
      `CALL usp_ScraperSync_Start($1, $2, $3, NULL, NULL)`,
      ['SCRAPER', fechaDesde || null, fechaHasta || null]
    );
    const syncId = result.rows[0]?.p_id || 0;
    console.log(`[DB] Sync log iniciado: ID ${syncId}`);
    return { syncId, error: null };
  } catch (e) {
    return { syncId: null, error: e.message };
  } finally {
    client.release();
  }
}

export async function finalizarSyncLog(syncId, stats) {
  const p = initDB();
  const client = await p.connect();
  try {
    const detalleErrores = stats.erroresDetalle && stats.erroresDetalle.length > 0
      ? JSON.stringify(stats.erroresDetalle) : null;

    await client.query(
      `CALL usp_ScraperSync_End(
        $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, NULL
      )`,
      [
        syncId,
        stats.registrosProcesados || 0,
        stats.nuevos || 0,
        stats.actualizados || 0,
        stats.errores || 0,
        detalleErrores,
        stats.totalLicitaciones || 0,
        stats.totalConActa || 0,
        stats.totalSinActa || 0,
        stats.totalAnalizados || 0,
        stats.duracionMs || null,
        stats.estado || 'completado',
      ]
    );
    console.log(`[DB] Sync log finalizado: ID ${syncId}`);
    return { error: null };
  } catch (e) {
    return { error: e.message };
  } finally {
    client.release();
  }
}

export async function tieneAnalisisCompletado(licitacionId) {
  if (!licitacionId) return false;
  const p = initDB();
  try {
    const result = await p.query(
      `SELECT 1 FROM analisis_workspaces WHERE licitacion_id = $1 AND estado = 'completado' LIMIT 1`,
      [licitacionId]
    );
    return result.rows.length > 0;
  } catch (e) {
    console.log(`[DB] Error verificando analisis existente: ${e.message}`);
    return false;
  }
}

export async function licitacionYaExiste(codigoExterno) {
  const p = initDB();
  try {
    const result = await p.query(
      `SELECT usp_Licitacion_YaExistePorCodigo($1) as id`,
      [codigoExterno]
    );
    const id = result.rows[0]?.id || 0;
    return id > 0 ? id : null;
  } catch (e) {
    return null;
  }
}

function parseFechaMP(fechaStr) {
  if (!fechaStr || fechaStr === 'N/A' || fechaStr === 'No hay informaci\u00f3n') return null;
  const trimmed = fechaStr.trim();
  const patterns = [
    /^(\d{2})-(\d{2})-(\d{4})\s+(\d{2}):(\d{2}):(\d{2})$/,
    /^(\d{2})-(\d{2})-(\d{4})$/,
    /^(\d{2})\/(\d{2})\/(\d{4})$/,
  ];
  for (const pat of patterns) {
    const m = trimmed.match(pat);
    if (m) {
      const [, d, mon, y, h, min, s] = m;
      return `${y}-${mon}-${d}${h ? `T${h}:${min}:${s}` : 'T00:00:00'}`;
    }
  }
  const d = new Date(trimmed);
  if (!isNaN(d.getTime())) return d.toISOString();
  return null;
}

function nombreArchivoDesdeRuta(ruta) {
  if (!ruta) return 'documento';
  const partes = ruta.replace(/\\/g, '/').split('/');
  return partes[partes.length - 1] || 'documento';
}

export async function crearTablaSessionSiNoExiste() {
  const p = initDB();
  const client = await p.connect();
  try {
    await client.query(`
      CREATE TABLE IF NOT EXISTS scraper_session (
        id INT PRIMARY KEY,
        session_data JSONB NOT NULL,
        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
      );
    `);
  } catch (e) {
    console.error(`[DB] Error creando tabla scraper_session: ${e.message}`);
  } finally {
    client.release();
  }
}

export async function guardarEstadoSesion(sessionData) {
  const p = initDB();
  const client = await p.connect();
  try {
    await crearTablaSessionSiNoExiste();
    const dataStr = JSON.stringify(sessionData);
    await client.query(`
      INSERT INTO scraper_session (id, session_data, updated_at)
      VALUES (1, $1::jsonb, CURRENT_TIMESTAMP)
      ON CONFLICT (id) DO UPDATE
      SET session_data = EXCLUDED.session_data, updated_at = CURRENT_TIMESTAMP;
    `, [dataStr]);
    console.log('[DB] Estado de sesion guardado correctamente en la BD.');
    return true;
  } catch (e) {
    console.error(`[DB] Error guardando estado de sesion: ${e.message}`);
    return false;
  } finally {
    client.release();
  }
}

export async function obtenerEstadoSesion() {
  const p = initDB();
  const client = await p.connect();
  try {
    await crearTablaSessionSiNoExiste();
    const res = await client.query(`
      SELECT session_data FROM scraper_session WHERE id = 1 LIMIT 1;
    `);
    if (res.rows.length > 0) {
      return res.rows[0].session_data;
    }
    return null;
  } catch (e) {
    console.error(`[DB] Error obteniendo estado de sesion: ${e.message}`);
    return null;
  } finally {
    client.release();
  }
}

/**
 * Invalida la sesion persistida (login fallido, bloqueo robot o sesion expirada detectada):
 * evita que el proximo ciclo arranque reutilizando cookies envenenadas.
 */
export async function limpiarEstadoSesion() {
  const p = initDB();
  const client = await p.connect();
  try {
    await crearTablaSessionSiNoExiste();
    await client.query(`DELETE FROM scraper_session WHERE id = 1;`);
    console.log('[DB] Estado de sesion invalidado (eliminado de la BD).');
    return true;
  } catch (e) {
    console.error(`[DB] Error invalidando estado de sesion: ${e.message}`);
    return false;
  } finally {
    client.release();
  }
}