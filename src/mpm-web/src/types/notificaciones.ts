export interface NotificacionItem {
  id: number
  usuarioId: string
  tipo: string
  titulo: string
  mensaje: string
  metadata?: string | null
  leido: boolean
  createdAt: string
}

export interface NotificacionesCount {
  count: number
}
