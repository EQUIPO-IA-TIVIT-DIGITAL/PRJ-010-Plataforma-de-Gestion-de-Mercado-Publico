import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import type { AiProviderSettings, ActualizarAiProviderRequest } from '../types/systemConfig'
import { apiGet, apiPut } from '../lib/apiClient'

const BASE = '/api/system/ai-provider'

export function useAiProviderSettings() {
  return useQuery({
    queryKey: ['system', 'ai-provider'],
    queryFn: () => apiGet<{ data: AiProviderSettings }>(BASE),
    refetchInterval: 30000,
  })
}

export function useActualizarAiProvider() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: ActualizarAiProviderRequest) =>
      apiPut<{ data: AiProviderSettings }>(BASE, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['system', 'ai-provider'] })
    },
  })
}
