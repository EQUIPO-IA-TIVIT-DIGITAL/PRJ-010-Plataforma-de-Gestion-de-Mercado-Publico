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
