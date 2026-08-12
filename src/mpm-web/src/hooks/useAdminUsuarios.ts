import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiPut } from '../lib/apiClient';
import type { ApiResponse } from '../types/api';
import type { AdminUsuarioItem, CrearUsuarioRequest, AdminRol } from '../types/admin';

const BASE = '/api/v1/admin/usuarios';

export function useAdminUsuarios(search: string, pagina: number, paginaSize: number) {
  return useQuery({
    queryKey: ['admin', 'usuarios', search, pagina, paginaSize],
    queryFn: async () => {
      const qs = new URLSearchParams({ pagina: String(pagina), paginaSize: String(paginaSize) });
      if (search.trim()) qs.set('search', search.trim());
      const json = await apiGet<ApiResponse<AdminUsuarioItem[]>>(`${BASE}?${qs.toString()}`);
      return json.data;
    },
    enabled: search.length === 0 || search.length >= 2,
  });
}

export function useCrearAdminUsuario() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CrearUsuarioRequest) => apiPost<ApiResponse<AdminUsuarioItem>>(BASE, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'usuarios'] }),
  });
}

export function useActualizarEstadoUsuario() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, activo }: { id: number; activo: boolean }) =>
      apiPut<ApiResponse<unknown>>(`${BASE}/${id}/estado`, { activo }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'usuarios'] }),
  });
}

export function useActualizarRolUsuario() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, rol }: { id: number; rol: AdminRol }) =>
      apiPut<ApiResponse<unknown>>(`${BASE}/${id}/rol`, { rol }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'usuarios'] }),
  });
}

export function useSetAccountManager() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, esAccountManager }: { id: number; esAccountManager: boolean }) =>
      apiPut<ApiResponse<unknown>>(`${BASE}/${id}/account-manager`, { esAccountManager }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'usuarios'] }),
  });
}

export function useEnviarRecuperacion() {
  return useMutation({
    mutationFn: (email: string) => apiPost<ApiResponse<unknown>>('/api/v1/auth/forgot-password', { email }),
  });
}
