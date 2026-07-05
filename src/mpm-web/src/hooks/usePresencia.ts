import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { PresenciaItem } from '../types/mensajeria';
import type { ApiResponse } from '../types/api';

export function usePresencia(userIds: string[]) {
  return useQuery({
    queryKey: ['presencia', userIds],
    queryFn: async () => {
      if (userIds.length === 0) return [];
      const qs = new URLSearchParams({ userIds: userIds.join(',') });
      const json = await apiGet<ApiResponse<PresenciaItem[]>>(`/api/v1/presencia?${qs}`);
      return json.data;
    },
    enabled: userIds.length > 0,
    staleTime: 10_000,
    refetchInterval: 30_000,
  });
}
