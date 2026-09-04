import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { ApiResponse } from '../types/api';
import type { AdminLogItem, LogTipo } from '../types/admin';

export interface AdminLogsParams {
  tipo?: LogTipo | null;
  estado?: string | null;
  limite?: number;
}

export function useAdminLogs(params: AdminLogsParams) {
  return useQuery({
    queryKey: ['admin', 'logs', params],
    queryFn: async () => {
      const qs = new URLSearchParams();
      if (params.tipo) qs.set('tipo', params.tipo);
      if (params.estado) qs.set('estado', params.estado);
      qs.set('limite', String(params.limite ?? 100));
      const json = await apiGet<ApiResponse<AdminLogItem[]>>(`/api/v1/admin/logs?${qs.toString()}`);
      return json.data;
    },
  });
}
