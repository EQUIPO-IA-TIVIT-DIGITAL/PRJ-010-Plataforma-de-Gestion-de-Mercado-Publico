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

// 036-flujo-comercial-ofertas (Fase 1): documentos de licitación.
export interface AdjuntoDocumento {
  id: number;
  tipo: string;
  nombreArchivo: string;
  tamanioBytes: number | null;
  mimeType: string | null;
  sha256Hash: string | null;
  fechaGrilla: string | null;
  version: number;
  esActa: boolean;
  descargaEstado: string;
  descargadoAt: string | null;
}

export interface EstadoDocumentos {
  estadoConjunto: 'pendiente' | 'descargando' | 'completado' | 'error';
  descargaError: string | null;
  conjuntoHash: string | null;
  documentos: AdjuntoDocumento[];
}

export interface DescargarDocumentosResult {
  estadoConjunto: string;
  accion: string;
  descargados: number;
  reutilizados: number;
  actualizados: number;
  errores: number;
  descargaError: string | null;
  conjuntoHash: string | null;
}

// 036-flujo-comercial-ofertas (Fase 1.3): análisis comercial con IA.
export interface AnalisisComercialEstado {
  estado: 'pendiente' | 'analizando' | 'completado' | 'error';
  error: string | null;
  conjuntoHash: string | null;
  desactualizado: boolean;
  resumenEjecutivo: string | null;
  goNoGo: string | null;
  scoreConfianza: number | null;
  modeloUsado: string | null;
  tokensEntrada: number | null;
  tokensSalida: number | null;
  creadoPor: string | null;
  createdAt: string | null;
  updatedAt: string | null;
  resultado: Record<string, unknown> | null;
}

export interface IniciarAnalisisComercialResult {
  estado: string;
  cacheHit: boolean;
  conjuntoHash: string | null;
}

// 036-flujo-comercial-ofertas (Fase 2): match de capacidades TIVIT contra Census (spec censo.md).
export interface CensoPersona {
  nombre: string;
  email: string;
  corporateId: string;
  pais: string;
  cargo: string;
  /** Skills de la persona que matchean los requisitos buscados. */
  cobertura: number;
  /** Total de skills/certificaciones buscados (denominador de la cobertura). */
  totalRequeridos: number;
  skills: string[];
  certificaciones: string[];
}

export interface CensoResumen {
  totalPersonas: number;
  maxCobertura: number;
  /** Personas con cobertura >= 70%. */
  personasConCoberturaAlta: number;
}

/** Resultado completo del match (POST /match-capacidades y GET → `match`). */
export interface CensoMatchResult {
  ejecutadoEn: string;
  consultas: number;
  cacheUsadas: number;
  tecnologiasExpandidas: string[];
  personas: CensoPersona[];
  resumen: CensoResumen;
}

/** Estado + resultado del último match (GET /match-capacidades). */
export interface CensoMatchEstado {
  estado: 'no_ejecutado' | 'en_curso' | 'completado' | 'error';
  ultimoEjecutadoAt: string | null;
  match: CensoMatchResult | null;
}

/** Body opcional del POST /match-capacidades (body > preferencias > defaults). */
export interface CensoMatchRequest {
  tecnologias?: string[];
  certificaciones?: string[];
  filtrarPais?: boolean;
  pais?: string;
}

/** Preferencias del usuario para el match (GET /usuarios/me/preferencias-censo). */
export interface CensoPreferencias {
  filtrarPais: boolean;
  pais: string;
}

/** Body de actualización parcial (PUT /usuarios/me/preferencias-censo). */
export interface CensoPreferenciasUpdate {
  filtrarPais?: boolean;
  pais?: string;
}

// 036-flujo-comercial-ofertas (Fase 2): decisión GO/NO GO (spec decisiones.md).
export type DecisionValor = 'go' | 'no_go';

/** Decisión registrada (POST /decision) — incluye el snapshot IA (V142 → V144). */
export interface Decision {
  decisionId: number | null;
  codigoExterno: string;
  decision: DecisionValor | null;
  motivo: string | null;
  /** Snapshot de la recomendación IA al momento de decidir (strong_go|go|no_go|strong_no_go). */
  recomendacionIa: string | null;
  scoreConfianza: number | null;
  decididoPor: string | null;
  decididoAt: string | null;
  notificados: string[] | null;
  notificadoAt: string | null;
}

/** Estado vigente de la decisión (GET /decision) para la ficha de la licitación. */
export interface DecisionEstado extends Decision {
  decidida: boolean;
}
