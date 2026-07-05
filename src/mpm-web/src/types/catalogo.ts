export interface EstadoItem {
  codigo: number;
  nombre: string;
}

export interface TipoLicitacionItem {
  codigo: number;
  nombre: string;
  slug: string;
}

export interface MonedaItem {
  codigo: number;
  nombre: string;
  simbolo: string;
  codigoIso: string;
}

export interface CatalogosResponse {
  estadosLicitacion: EstadoItem[];
  tiposLicitacion: TipoLicitacionItem[];
  monedas: MonedaItem[];
}