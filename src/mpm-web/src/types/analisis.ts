export interface WorkspaceItem {
  id: number
  licitacionId: number
  licitacionNombre: string
  nombre: string
  estado: 'pendiente' | 'listo' | 'analizando' | 'completado' | 'error'
  documentosCount: number
  ultimoAnalisisId?: number | null
  ultimoAnalisisFecha?: string | null
  createdAt: string
  // spec 031 (US3): fecha de adjudicación de la licitación -- es el campo por el que
  // ahora se ordena esta lista, se muestra para que el orden sea explicable.
  fechaAdjudicacion?: string | null
}

export interface WorkspaceDetalle {
  id: number
  licitacionId: number
  licitacionNombre: string
  nombre: string
  estado: string
  documentosCount: number
  ultimoAnalisisId?: number | null
  ultimoAnalisisDocumentoId?: number | null
  ultimoAnalisisDocumentoNombre?: string | null
  ultimoAnalisisFecha?: string | null
  createdAt: string
  updatedAt: string
}

export interface DocumentoItem {
  id: number
  nombreArchivo: string
  mimeType: string
  tamanioBytes: number
  createdAt: string
}

export interface DocumentoDetalle {
  id: number
  workspaceId: number
  nombreArchivo: string
  mimeType: string
  tamanioBytes: number
  rutaStorage: string
  createdAt: string
}

export interface ResultadoAnalisis {
  id: number
  workspaceId: number
  documentoId: number
  documentoNombre: string
  contenidoJson?: string | null
  modeloUsado: string
  tokensEntrada: number
  tokensSalida: number
  createdAt: string
}

export interface AnalisisResumen {
  id: number
  estado: string
  modeloUsado?: string | null
  tokensEntrada?: number | null
  tokensSalida?: number | null
  createdAt: string
}

export interface ChatMensaje {
  id: number
  rol: 'user' | 'assistant'
  contenido: string
  createdAt: string
}

export interface ChatResponse {
  respuesta: string
  conversacionId: number
  mensajes: ChatMensaje[]
}

export interface ChatHistorial {
  conversacionId: number
  mensajes: ChatMensaje[]
}

// Fase 3 — Dashboard Ejecutivo (US2)
export interface LicitacionResumenEjecutivo {
  workspaceId: number
  nombre: string
  tivitGano: boolean
  resultadoTivit: string
  montoAdjudicado?: number | null
  montoTivit?: number | null
  adjudicatario?: string | null
  adjudicatarioRut?: string | null
  puntajeTivit?: number | null
  puntajeGanador?: number | null
  puntajeMaximo?: number | null
  fechaAnalisis: string
  competidoresNombres: string[]
  competidorGano?: boolean
  resultadoCompetidor?: string
  montoCompetidor?: number | null
}

export interface CompetidorRanking {
  nombre: string
  rut?: string | null
  vecesCompetidor: number
  vecesGanador: number
  montoTotalAdjudicado: number
  licitaciones: LicitacionResumenEjecutivo[]
}

export interface DashboardEjecutivo {
  totalAnalizadas: number
  totalGanadas: number
  totalPerdidas: number
  montoTotalGanado: number
  montoTotalPerdido: number
  puntajePromedioTivit?: number | null
  puntajePromedioGanador?: number | null
  rankingCompetidores: CompetidorRanking[]
  factoresPerdidaFrecuentes: string[]
  licitaciones: LicitacionResumenEjecutivo[]
  aniosDisponibles: number[]
}
