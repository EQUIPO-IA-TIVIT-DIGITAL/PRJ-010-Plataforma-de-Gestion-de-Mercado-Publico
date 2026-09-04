import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { AnalisisComercialEstado, IniciarAnalisisComercialResult } from '../types/licitacion';

const BASE = (codigoExterno: string) =>
  `/api/v1/licitaciones/${encodeURIComponent(codigoExterno)}/analisis-comercial`;

/** Estado/resultado del análisis comercial; polling mientras 'analizando'. */
export function useAnalisisComercialEstado(codigoExterno: string | null) {
  return useQuery({
    queryKey: ['licitacion-analisis-comercial', codigoExterno],
    queryFn: () => apiGet<{ data: AnalisisComercialEstado }>(BASE(codigoExterno!)),
    enabled: !!codigoExterno,
    staleTime: 15_000,
    retry: 1,
    refetchInterval: (query) =>
      (query.state.data?.data.estado === 'analizando' ? 3000 : false) as false | 3000,
  });
}

/** Dispara el análisis comercial de los documentos descargados. */
export function useIniciarAnalisisComercial() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (codigoExterno: string) =>
      apiPost<{ data: IniciarAnalisisComercialResult }>(BASE(codigoExterno)),
    onSuccess: (_data, codigoExterno) => {
      queryClient.invalidateQueries({ queryKey: ['licitacion-analisis-comercial', codigoExterno] });
    },
  });
}
