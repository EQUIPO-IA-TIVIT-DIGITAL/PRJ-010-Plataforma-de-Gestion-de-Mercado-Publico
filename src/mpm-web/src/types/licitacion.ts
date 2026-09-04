// src/types/licitacion.ts
// Tipos para licitaciones y el flujo de oferta (Fase 1..5)

export interface LicitacionListItem {
  id: number;
  codigoExterno: string;
  nombre: string;
  organismo: string;
  region: string | null;
  montoNetoEstimado: number | null;
  moneda: string | null;
  fechaCierre: string | null;
  estado: string;
  estadoOferta: string | null; // no_iniciada | pliegos_descargados | analisis_listo | go | no_go | propuesta_enviada
}

export interface LicitacionResumenOferta {
  id: number;
  codigoExterno: string;
  nombre: string;
  organismo: string;
  montoNetoEstimado: number | null;
  moneda: string | null;
  fechaCierre: string | null;
  estadoOferta: string;
  tieneDocumentos: boolean;
  totalDocumentos: number;
  tieneAnalisis: boolean;
  decision: string | null;
  tienePropuesta: boolean;
  totalPropuestas: number;
}

// 036-flujo-comercial-ofertas (Fase 1.1 / 1.2): documentos de la licitación.
// Nota: el GET de documentos devuelve AdjuntoDocumentoDto del backend, que además
// trae esActa/version/fechaGrilla (opcionales acá porque el shape coexiste con el
// de carga manual).
export interface LicitacionDocumentoItem {
  id: number;
  licitacionId: number;
  adjuntoId: number | null;
  nombreArchivo: string;
  extension: string;
  mimeType: string | null;
  tamanioBytes: number | null;
  checksumSha256: string | null;
  origen: string;
  metadataExtra: Record<string, unknown> | null;
  createdAt: string;
  esActa?: boolean;
  version?: number;
  fechaGrilla?: string | null;
}

export interface AdjuntoDocumento {
  id: number;
  nombreArchivo: string;
  tamanioBytes: number | null;
  extension: string;
  fechaDescarga: string | null;
}

export interface EstadoDocumentos {
  estadoConjunto: 'no_descargado' | 'descargando' | 'completado' | 'error';
  documentos: LicitacionDocumentoItem[];
  descargaError: string | null;
  conjuntoHash: string | null;
}

export interface DocumentosLicitacionResponse {
  licitacionId: number;
  codigoExterno: string;
  estadoConjunto: 'no_descargado' | 'descargando' | 'completado' | 'error';
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
export interface CensoPersonaSkill {
  nombre: string;
  nivel?: number | null;
  nivelTexto?: string | null;
}

export interface CensoPersonaCertificacion {
  nombre: string;
  fileId?: string | null;
}

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
  skillsDetalle?: CensoPersonaSkill[];
  certificaciones: string[];
  certificacionesDetalle?: CensoPersonaCertificacion[];
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

// 028-listado-licitaciones + 031-areas-negocio (Fase listado principal) + 0+1+2 capacitacion 14-08-2026
export interface LicitacionResumen {
  id: number;
  codigoExterno: string;
  nombre: string;
  tipo: string;
  estado: { codigo: number; nombre: string };
  organismo: string;
  fechaPublicacion: string | null;
  fechaCierre: string | null;
  montoEstimado: number | null;
  moneda: string;
  itemsCount: number;
}

// Detalle de licitación (GET /api/v1/licitaciones/:codigo): resumen + campos
// extendidos que usa LicitacionDetailDrawer ( opcionales porque no toda vista los trae).
export interface LicitacionDetalleItem {
  codigo: string;
  nombre: string;
  cantidad: number | null;
  unidadMedida: string | null;
  precioEstimado: number | null;
}

export interface LicitacionDetalle extends LicitacionResumen {
  items?: LicitacionDetalleItem[] | null;
  link?: string | null;
  unidadTecnica?: string | null;
}

export interface LicitacionFilter {
  page: number;
  pageSize: number;
  search?: string;
  estado?: number | null;
  tipo?: string | null;
  organismo?: string | null;
  fechaDesde?: string | null;
  fechaHasta?: string | null;
  sortBy: string;
  sortDir: 'asc' | 'desc';
  area?: number | null;
  sinClasificar?: boolean | null;
  montoDesde?: number | null;
  montoHasta?: number | null;
}

export interface PaginationInfo {
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
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
  totalCount?: number | null;
}

export interface EstadoConteo {
  codigoEstado: number;
  nombreEstado: string;
  cantidad: number;
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
