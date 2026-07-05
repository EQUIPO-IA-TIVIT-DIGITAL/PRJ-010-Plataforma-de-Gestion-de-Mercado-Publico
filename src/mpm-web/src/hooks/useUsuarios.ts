import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { ApiResponse } from '../types/api';

export interface UsuarioItem {
  id: number;
  email: string;
  nombre: string;
  tenantNombre: string | null;
}

export function useUsuarios(search: string) {
  return useQuery({
    queryKey: ['usuarios', search],
    queryFn: async () => {
      const qs = search ? `?search=${encodeURIComponent(search)}` : '';
      const json = await apiGet<ApiResponse<UsuarioItem[]>>(`/api/v1/usuarios${qs}`);
      return json.data;
    },
    staleTime: 30_000,
    enabled: search.length === 0 || search.length >= 2,
  });
}
