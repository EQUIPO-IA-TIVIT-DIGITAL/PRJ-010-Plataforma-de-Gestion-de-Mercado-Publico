export const TIPO_LICITACION = {
  LICITACION: 'Licitacion',
  TRATO_DIRECTO: 'TratoDirecto',
  CONVENIO_MARCO: 'ConvenioMarco',
  COMPRA_AGIL: 'CompraAgil',
} as const;

export type TipoLicitacion = (typeof TIPO_LICITACION)[keyof typeof TIPO_LICITACION];

export interface LicitacionResumen {
  id: number;
  codigoExterno: string;
  nombre: string;
  tipo: TipoLicitacion;
  estado: { codigo: number; nombre: string };
  organismo: string;
  fechaPublicacion: string | null;
  fechaCierre: string | null;
  montoEstimado: number | null;
  moneda: string;
  itemsCount: number;
}

export interface LicitacionDetalle extends LicitacionResumen {
  descripcion: string | null;
  unidadTecnica: string | null;
  fechaAdjudicacion: string | null;
  fechaEstimadaAdjudicacion: string | null;
  link: string | null;
  items: LicitacionItem[];
}

export interface LicitacionItem {
  codigo: number;
  nombre: string;
  cantidad: number | null;
  unidadMedida: string | null;
  precioEstimado: number | null;
  categoria: string | null;
}

export interface LicitacionFilter {
  page?: number;
  pageSize?: number;
  search?: string;
  estado?: number | null;
  tipo?: string | null;
  organismo?: string;
  fechaDesde?: string | null;
  fechaHasta?: string | null;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  area?: number | null;
  sinClasificar?: boolean | null;
}

export interface EstadoConteo {
  codigoEstado: number;
  nombreEstado: string;
  cantidad: number;
}

export interface LicitacionSearchResult {
  codigoExterno: string;
  nombre: string;
  tipo: TipoLicitacion;
  organismo: string;
}

export interface LicitacionNaturalSearchResult {
  id: number;
  codigoExterno: string;
  nombre: string;
  descripcion: string | null;
  organismo: string | null;
  codigoEstado: number;
  tipo: string;
  fechaPublicacion: string | null;
  relevancia: number;
}

export interface PaginationInfo {
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
}
