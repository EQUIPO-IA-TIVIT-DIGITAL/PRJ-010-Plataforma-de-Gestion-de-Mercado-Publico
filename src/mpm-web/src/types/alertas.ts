export interface ReglaAlerta {
  id: number;
  keyword: string;
  sinonimosIa: string[] | null;
  montoMinimo: number | null;
  montoMaximo: number | null;
  tiposLicitacion: string[] | null;
  organismos: string[] | null;
  activa: boolean;
  notificarTelegram: boolean;
}

export interface CrearReglaRequest {
  keyword: string;
  montoMinimo?: number | null;
  montoMaximo?: number | null;
  tiposLicitacion?: string[] | null;
  organismos?: string[] | null;
  notificarTelegram?: boolean;
}

export interface ProbarAlertaRequest {
  licitacionId: number;
  codigoExterno: string;
  nombre: string;
  descripcion?: string | null;
  monto?: number | null;
  tipoLicitacion?: string | null;
  organismo?: string | null;
}

export interface ProbarAlertaResponse {
  alertaDisparadaId: number;
  esPrueba: boolean;
  notificacionInAppCreada: boolean;
  notificacionTelegramEnviada: boolean;
  notificacionTelegramError: string | null;
}
