import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { AreaNegocioItem, CatalogosResponse } from '../types/catalogo';

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

async function fetchAreasNegocio(): Promise<AreaNegocioItem[]> {
  const json = await apiGet<{ data: AreaNegocioItem[] }>('/api/v1/catalogos/areas-negocio');
  return json.data;
}

export function useAreasNegocio() {
  return useQuery({
    queryKey: ['catalogos', 'areas-negocio'],
    queryFn: fetchAreasNegocio,
    staleTime: 5 * 60 * 1000, // 5 minutes cache
  });
}
