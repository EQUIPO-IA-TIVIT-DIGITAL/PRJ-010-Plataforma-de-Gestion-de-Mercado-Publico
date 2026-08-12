// Tipos del Centro de Administración (/admin/*)

export type AdminRol = 'SuperAdmin' | 'Admin' | 'Analista' | 'Usuario';

export interface AdminUsuarioItem {
  id: number;
  email: string;
  nombre: string;
  roles: AdminRol[];
  activo: boolean;
  ultimoLogin: string | null;
  tenantNombre: string | null;
  esAccountManager: boolean;
  totalCount: number;
}

export interface CrearUsuarioRequest {
  email: string;
  nombre: string;
  password: string;
  rol: AdminRol;
  tenantId?: string | null;
  tenantNombre?: string | null;
}

export type LogTipo = 'auth' | 'sync' | 'scraper' | 'extraccion' | 'ai_provider';

export interface AdminLogItem {
  id: number;
  tipo: LogTipo;
  fecha: string;
  estado: string;
  detalle: string;
  extra: string | null; // JSON string crudo del payload específico
}

export const TIPOS_LOGS: Record<LogTipo, { label: string; estados: string[] }> = {
  auth: { label: 'Inicios de sesión', estados: ['exito'] },
  sync: { label: 'Sincronizaciones', estados: ['EN_PROGRESO', 'EXITO', 'PARCIAL', 'FALLO'] },
  scraper: { label: 'Scraper', estados: ['iniciado', 'completado', 'error'] },
  extraccion: { label: 'Extracción de documentos', estados: ['exito', 'fallo', 'sin_adjuntos'] },
  ai_provider: { label: 'Cambios de proveedor IA', estados: ['activo', 'historial'] },
};
