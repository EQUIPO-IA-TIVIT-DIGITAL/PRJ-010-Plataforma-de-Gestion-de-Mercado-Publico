export interface OfertaCompetidor {
  licitacionId: number;
  codigoExterno: string;
  nombreLicitacion: string;
  organismo: string | null;
  fechaCierre: string | null;
  rutProveedor: string | null;
  nombreProveedor: string;
  montoOferta: number | null;
  estadoOferta: string | null;
}

export interface AnalizarCompetidorRequest {
  nombreCompetidor: string;
  fechaDesde: string;
  fechaHasta: string;
  confirmar: boolean;
}

export interface AnalisisCompetidorContenido {
  patrones: string;
  organismosFrecuentes: string[];
  montoPromedioOfertado: number | null;
  tasaExito: string;
  recomendaciones: string[];
}

export interface AnalisisCompetidorResponse {
  cacheado: boolean;
  cantidadLicitaciones: number;
  contenido: AnalisisCompetidorContenido | null;
  requiereConfirmacion: boolean;
}

// spec 031 (US4): actividad total de mercado -- incluye licitaciones donde TIVIT no participó
export interface LicitacionActividadMercado {
  licitacionCodigo: string;
  nombre: string;
  montoOferta: number | null;
  estadoOferta: string | null;
  tivitParticipo: boolean;
}

export interface ActividadMercadoResponse {
  estado: 'generando' | 'listo' | 'error';
  nombreCompetidor: string;
  cantidadLicitaciones: number | null;
  montoTotalAdjudicado: number | null;
  licitaciones: { licitaciones: LicitacionActividadMercado[] } | null;
}
