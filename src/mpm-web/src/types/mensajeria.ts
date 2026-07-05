import type { PaginationInfo } from './licitacion';

export const TIPO_CONVERSACION = { DIRECTO: 'directo', GRUPAL: 'grupal' } as const;
export type TipoConversacion = (typeof TIPO_CONVERSACION)[keyof typeof TIPO_CONVERSACION];

export const TIPO_MENSAJE = { TEXTO: 'texto', IMAGEN: 'imagen', ARCHIVO: 'archivo', SISTEMA: 'sistema' } as const;
export type TipoMensaje = (typeof TIPO_MENSAJE)[keyof typeof TIPO_MENSAJE];

export const ROL_PARTICIPANTE = { ADMIN: 'admin', MIEMBRO: 'miembro' } as const;
export type RolParticipante = (typeof ROL_PARTICIPANTE)[keyof typeof ROL_PARTICIPANTE];

export const ESTADO_PRESENCIA = { ONLINE: 'online', OFFLINE: 'offline', ESCRIBIENDO: 'escribiendo' } as const;
export type EstadoPresencia = (typeof ESTADO_PRESENCIA)[keyof typeof ESTADO_PRESENCIA];

export const ESTADO_MENSAJE = { ENTREGADO: 'entregado', LEIDO: 'leido' } as const;
export type EstadoMensaje = (typeof ESTADO_MENSAJE)[keyof typeof ESTADO_MENSAJE];

export interface ParticipanteItem {
  userId: string;
  nombre: string;
  rol: RolParticipante;
  avatarUrl: string | null;
  joinedAt: string | null;
  leftAt: string | null;
}

export interface MensajeResumen {
  id: number;
  userId: string;
  tipo: TipoMensaje;
  contenido: string;
  createdAt: string;
}

export interface MensajeDetalle {
  id: number;
  userId: string;
  userName: string;
  tipo: TipoMensaje;
  contenido: string | null;
  replyTo: MensajeResumen | null;
  adjuntos: AdjuntoItem[];
  estados: MensajeEstado[];
  editedAt: string | null;
  createdAt: string;
}

export interface AdjuntoItem {
  id: number;
  nombreArchivo: string;
  mimeType: string;
  tamanioBytes: number;
  downloadUrl: string;
  createdAt: string;
}

export interface AdjuntoDetalle {
  id: number;
  mensajeId: number;
  nombreArchivo: string;
  mimeType: string;
  tamanioBytes: number;
  rutaStorage: string;
  createdAt: string;
}

export interface MensajeEstado {
  userId: string;
  estado: EstadoMensaje;
  updatedAt: string;
}

export interface ConversacionResumen {
  id: number;
  tipo: TipoConversacion;
  asunto: string | null;
  licitacionId: number | null;
  licitacionNombre: string | null;
  participantes: ParticipanteItem[];
  ultimoMensaje: MensajeResumen | null;
  noLeidos: number;
  updatedAt: string;
}

export interface ConversacionDetalle {
  id: number;
  tipo: TipoConversacion;
  asunto: string | null;
  licitacionId: number | null;
  licitacionNombre: string | null;
  participantes: ParticipanteItem[];
  createdAt: string;
  updatedAt: string;
}

export interface PresenciaItem {
  userId: string;
  estado: EstadoPresencia;
  updatedAt: string | null;
}

export interface ConversacionFilter {
  page: number;
  pageSize: number;
  search: string | null;
  sortBy: string;
  sortDir: string;
}

export interface MensajeFilter {
  page: number;
  pageSize: number;
  before: number | null;
}

export interface CrearConversacionRequest {
  tipo: TipoConversacion;
  asunto: string | null;
  licitacionId: number | null;
  participanteIds: string[];
}

export interface ActualizarConversacionRequest {
  asunto: string;
}

export interface AgregarParticipanteRequest {
  userId: string;
  rol: RolParticipante;
}

export interface EnviarMensajeRequest {
  tipo: TipoMensaje;
  contenido: string | null;
  replyToId: number | null;
}

export interface EditarMensajeRequest {
  contenido: string;
}

export interface TypingRequest {
  conversacionId: number;
  escribiendo: boolean;
}

export interface ConversacionListResponse {
  items: ConversacionResumen[];
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
}

export interface MensajeListResponse {
  items: MensajeDetalle[];
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
}
