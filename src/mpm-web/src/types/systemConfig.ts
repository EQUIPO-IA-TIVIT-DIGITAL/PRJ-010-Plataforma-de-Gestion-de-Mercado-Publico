// 033-migracion-qwen-g4 (US4): contrato del switch de proveedor de IA (super admin).

export type AiProvider = 'gemini' | 'openai'

export interface AiProviderSettings {
  provider: AiProvider
  model: string
  endpoint: string | null
  resolvedFrom: 'database' | 'environment'
  updatedByUsername: string | null
  updatedAt: string | null
}

export interface ActualizarAiProviderRequest {
  provider: AiProvider
  endpoint?: string | null
  model: string
}
