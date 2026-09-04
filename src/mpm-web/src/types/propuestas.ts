import type { CensoPersona, DecisionValor } from './licitacion';

export interface CatalogoExperiencia {
  id: number;
  titulo: string;
  cliente: string;
  descripcion: string | null;
  fechaInicio: string | null;
  fechaFin: string | null;
  montoUsd: number | null;
  pais: string | null;
  activo: boolean;
}

export interface CatalogoCertificacion {
  id: number;
  nombre: string;
  fileIdCensus: string | null;
  institucion: string | null;
  vigencia: string | null;
  titular?: string | null;
  tipo?: 'corporativa' | 'colaborador';
  activo: boolean;
  tieneArchivo?: boolean;
}

export interface CatalogoCapitulo {
  id: number;
  titulo: string;
  contenidoMarkdown: string | null;
  orden: number;
  activo: boolean;
}

export interface CatalogoPage<T> {
  items: T[];
  page: number;
  size: number;
  totalRecords: number;
  totalPages: number;
}

export interface RecomendacionRequest {
  codigoExterno?: string;
  requisitos?: {
    certificaciones: string[];
    tecnologias: string[];
    industria?: string;
  };
}

export interface RecomendacionCertificacion {
  id: number;
  nombre: string;
  institucion: string | null;
  score: number;
  categoria: string;
  tieneArchivo: boolean;
}

export interface RecomendacionExperiencia {
  id: number;
  titulo: string;
  cliente: string;
  score: number;
  categoria: string;
  motivo: string | null;
}

export interface RecomendacionResponse {
  fuente: string;
  requisitosUsados: RecomendacionRequest['requisitos'];
  certificaciones: RecomendacionCertificacion[];
  experiencias: RecomendacionExperiencia[];
  resumen: { recomendados: number; posibles: number; descartados: number };
}

export interface GenerarPropuestaRequest {
  capitulosIds?: number[];
  certificacionesIds?: number[];
  experienciasIds?: number[];
}

export interface GenerarPropuestaResponse {
  propuestaId: number;
  version: number;
  estado: string;
  rutaDescarga: string;
  generadoPor: string;
  generadoAt: string;
  resumen: {
    capitulos: number;
    certificaciones: number;
    certificacionesSinPdf: number;
    experiencias: number;
    archivosStorage: string;
  };
}

export interface PropuestaHistorial {
  propuestaId: number;
  version: number;
  estado: string;
  capitulos: number;
  certificaciones: number;
  experiencias: number;
  generadoPor: string | null;
  generadoAt: string | null;
  rutaDescarga: string | null;
}

export interface AvisarResponse {
  decisionId: number;
  codigoExterno: string;
  decision: DecisionValor;
  notificados: string[];
  notificadoAt: string;
  enviados: number;
}

export interface ExportarDriveResponse {
  driveFileId: string;
  webUrl: string;
  nombreArchivo: string;
  exportadoAt: string;
}

export type PersonaAvisable = Pick<CensoPersona, 'email' | 'nombre' | 'cargo'>;
