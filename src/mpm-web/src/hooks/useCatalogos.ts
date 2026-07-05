import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { CatalogosResponse } from '../types/catalogo';

async function fetchCatalogos(): Promise<CatalogosResponse> {
  const json = await apiGet<{ data: CatalogosResponse }>('/api/v1/catalogos');
  return json.data;
}

export function useCatalogos() {
  return useQuery({
    queryKey: ['catalogos'],
    queryFn: fetchCatalogos,
    staleTime: 5 * 60 * 1000, // 5 minutes cache
  });
}
