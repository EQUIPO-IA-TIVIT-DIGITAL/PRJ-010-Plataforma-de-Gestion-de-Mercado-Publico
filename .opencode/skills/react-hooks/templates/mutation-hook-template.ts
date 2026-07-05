import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/lib/api';
import type {
  Create{Entity}Request,
  Create{Entity}Response,
  Update{Entity}Request,
  Update{Entity}Response,
} from '../types';

export function useCreate{Entity}() {
  const queryClient = useQueryClient();

  const mutation = useMutation<Create{Entity}Response, Error, Create{Entity}Request>({
    mutationFn: (data) =>
      api.post<Create{Entity}Response>('/{entities}', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['{entities}', 'list'] });
    },
  });

  return {
    create: (data: Create{Entity}Request, options?: { onSuccess?: () => void }) =>
      mutation.mutate(data, {
        onSuccess: () => {
          options?.onSuccess?.();
        },
      }),
    isPending: mutation.isPending,
    error: mutation.error,
  };
}

export function useUpdate{Entity}() {
  const queryClient = useQueryClient();

  const mutation = useMutation<
    Update{Entity}Response,
    Error,
    { id: number; data: Update{Entity}Request }
  >({
    mutationFn: ({ id, data }) =>
      api.put<Update{Entity}Response>(`/{entities}/${id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['{entities}'] });
    },
  });

  return {
    update: (
      id: number,
      data: Update{Entity}Request,
      options?: { onSuccess?: () => void },
    ) =>
      mutation.mutate(
        { id, data },
        {
          onSuccess: () => {
            options?.onSuccess?.();
          },
        },
      ),
    isPending: mutation.isPending,
    error: mutation.error,
  };
}

export function useDelete{Entity}() {
  const queryClient = useQueryClient();

  const mutation = useMutation<void, Error, number>({
    mutationFn: (id) => api.delete(`/{entities}/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['{entities}', 'list'] });
    },
  });

  return {
    delete: (id: number, options?: { onSuccess?: () => void }) =>
      mutation.mutate(id, {
        onSuccess: () => {
          options?.onSuccess?.();
        },
      }),
    isPending: mutation.isPending,
    error: mutation.error,
  };
}
