import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { ConversacionResumen, ConversacionFilter } from '../types/mensajeria';
import type { ApiResponse } from '../types/api';

export function useConversaciones(params: ConversacionFilter) {
  return useQuery({
    queryKey: ['conversaciones', params],
    queryFn: async () => {
      const qs = new URLSearchParams({
        page: params.page.toString(),
        pageSize: params.pageSize.toString(),
        sortBy: params.sortBy,
        sortDir: params.sortDir,
      });
      if (params.search) qs.append('search', params.search);

      const json = await apiGet<ApiResponse<{ items: ConversacionResumen[]; page: number; pageSize: number; totalRecords: number; totalPages: number }>>(`/api/v1/conversaciones?${qs}`);
      return json.data;
    },
    staleTime: 30_000,
  });
}
