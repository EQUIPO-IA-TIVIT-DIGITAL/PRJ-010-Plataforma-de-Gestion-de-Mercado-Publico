/**
 * apiMpDetalle.js - 0: Migrar licitacion.js -> ApiMpService.GetDetalleAsync
 * Intenta obtener detalle de licitación vía API pública de Mercado Público
 * antes de caer al scraping Playwright de ficha.
 * Parity con MPM.Modules.Licitaciones.Services.ApiMpService.cs:31-69
 */

const API_BASE = 'https://api.mercadopublico.cl/servicios/v1/publico/licitaciones.json';

function parseTipoDesdeCodigo(codigoExterno) {
  if (!codigoExterno) return 'Licitacion';
  const partes = codigoExterno.split('-');
  if (partes.length < 3) return 'Licitacion';
  const letras = partes[2].replace(/[^A-Za-z]/g, '');
  if (!letras) return 'Licitacion';
  return letras.toUpperCase();
}

function codigoEstadoToLabel(codigo) {
  const c = Number(codigo);
  if (c === 5) return 'Publicada';
  if (c === 6) return 'Cerrada';
  if (c === 7) return 'Desierta';
  if (c === 8) return 'Adjudicada';
  if (c === 15 || c === 18 || c === 19) return 'Revocada';
  if (c === 1) return 'Publicada';
  return 'Publicada';
}

async function fetchConRetry(url, retries = 3) {
  for (let attempt = 0; attempt <= retries; attempt++) {
    try {
      const resp = await fetch(url, { headers: { 'Accept': 'application/json' } });
      const text = await resp.text();
      // 429 explícito
      if (resp.status === 429) {
        if (attempt < retries) {
          const delay = (attempt + 1) * 3000;
          console.log(`[API-MP] 429 Too Many Requests, reintentando en ${delay}ms (intento ${attempt + 1}/${retries})`);
          await new Promise(r => setTimeout(r, delay));
          continue;
        }
        throw new Error(`429 Too Many Requests`);
      }
      // 200 con Codigo > 200 (ej 10500 peticiones simultáneas, 203 ticket inválido)
      if (text) {
        try {
          const j = JSON.parse(text);
          if (j.Codigo && Number(j.Codigo) > 200) {
            const msg = j.Mensaje || 'Error API MP';
            // 10500 es rate-limit encubierto -> reintentar como 429
            if (Number(j.Codigo) === 10500 && attempt < retries) {
              const delay = (attempt + 1) * 3000;
              console.log(`[API-MP] Codigo 10500 (peticiones simultáneas), reintentando en ${delay}ms`);
              await new Promise(r => setTimeout(r, delay));
              continue;
            }
            throw new Error(`API Error ${j.Codigo}: ${msg}`);
          }
        } catch (e) {
          if (e.message && e.message.startsWith('API Error')) throw e;
          // no es JSON de error, ignorar parse error
        }
      }
      if (!resp.ok) {
        throw new Error(`HTTP ${resp.status}`);
      }
      return text;
    } catch (e) {
      if (attempt >= retries) throw e;
      // solo reintentar en 429/10500, no en otros
      if (e.message.includes('429') || e.message.includes('10500')) continue;
      throw e;
    }
  }
}

/**
 * Intenta obtener detalle vía API. Retorna datos en forma compatible con
 * extraerDatosDePagina() + upsertLicitacion() o null si falla.
 */
export async function obtenerDetalleViaApi(codigoExterno) {
  const ticket = process.env.MP_TICKET;
  if (!ticket) {
    console.log('[API-MP] MP_TICKET no configurado, saltando API y usando scraping');
    return null;
  }
  if (!codigoExterno) return null;

  const url = `${API_BASE}?ticket=${encodeURIComponent(ticket)}&codigo=${encodeURIComponent(codigoExterno)}`;
  console.log(`[API-MP] Consultando detalle via API: ${codigoExterno}`);

  try {
    const text = await fetchConRetry(url, 3);
    const parsed = JSON.parse(text);
    const listado = parsed.Listado;
    if (!Array.isArray(listado) || listado.length === 0) {
      console.log(`[API-MP] Sin resultados para ${codigoExterno}`);
      return null;
    }
    const item = listado[0];
    // Mapear a forma esperada por db.js::upsertLicitacion y agente-mp.js
    const codigoEstadoNum = item.CodigoEstado != null ? Number(item.CodigoEstado) : 5;
    const estadoLabel = codigoEstadoToLabel(codigoEstadoNum);
    const tipo = parseTipoDesdeCodigo(item.CodigoExterno) || item.Tipo || 'Licitacion';

    const datos = {
      codigo: item.CodigoExterno,
      nombre: item.Nombre || 'Sin nombre',
      descripcion: item.Descripcion || null,
      estado: estadoLabel,
      codigo_estado: codigoEstadoNum,
      tipo: tipo,
      moneda: item.Moneda || 'CLP',
      monto_estimado: item.MontoEstimado ?? null,
      monto: { totalEstimado: item.MontoEstimado != null ? String(item.MontoEstimado) : null },
      organismo: {
        razonSocial: item.Comprador?.NombreOrganismo || null,
        rut: item.Comprador?.CodigoOrganismo || null,
        unidad: item.Comprador?.NombreUnidad || null,
      },
      demandante: item.Comprador?.NombreOrganismo || null,
      fechas: {
        publicacion: item.Fechas?.FechaPublicacion || null,
        cierre: item.FechaCierre || item.Fechas?.FechaCierre || null,
        adjudicacion: item.Fechas?.FechaAdjudicacion || null,
      },
      fechaPublicacion: item.Fechas?.FechaPublicacion || null,
      fechaCierre: item.FechaCierre || item.Fechas?.FechaCierre || null,
      link: `https://www.mercadopublico.cl/Procurement/Modules/RFB/DetailsAcquisition.aspx?idlicitacion=${item.CodigoExterno}`,
      tituloPagina: `Ficha ${item.CodigoExterno} (API)`,
      rawText: JSON.stringify(item).substring(0, 80000),
      fechaExtraccion: new Date().toISOString(),
      fuente: 'api_mercadopublico',
      raw_api_item: item,
    };

    console.log(`[API-MP] OK ${codigoExterno}: ${datos.nombre.substring(0, 60)} | estado=${estadoLabel} monto=${datos.monto_estimado}`);
    return datos;
  } catch (e) {
    console.log(`[API-MP] Error obteniendo ${codigoExterno} via API: ${e.message} -> fallback a scraping`);
    return null;
  }
}
