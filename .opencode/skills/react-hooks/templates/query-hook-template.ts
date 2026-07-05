import { useQuery } from '@tanstack/react-query';
import { api } from '@/shared/lib/api';
import type { {Entity}ListResponse, {Entity}DetailResponse, {Entity}QueryParams } from '../types';

export function use{Entity}List(params?: {Entity}QueryParams, enabled = true) {
  return useQuery<{Entity}ListResponse>({
    queryKey: ['{entities}', 'list', params],
    queryFn: () =>
      api.get<{Entity}ListResponse>('/{entities}', { params }),
    enabled,
    staleTime: 0,
  });
}

export function use{Entity}(id: number | null, enabled = true) {
  return useQuery<{Entity}DetailResponse>({
    queryKey: ['{entities}', id],
    queryFn: () =>
      api.get<{Entity}DetailResponse>(`/{entities}/${id}`),
    enabled: enabled && id !== null,
    staleTime: 0,
  });
}

export function use{Entity}Options() {
  return useQuery<{Entity}Option[]>({
    queryKey: ['{entities}', 'options'],
    queryFn: () =>
      api.get<{Entity}Option[]>('/{entities}', {
        params: { pageSize: 1000 },
        select: (data) =>
          data.data?.items?.map((item) => ({
            value: item.id,
            label: item.name,
          })) ?? [],
      }),
    staleTime: 5 * 60 * 1000,
  });
}
