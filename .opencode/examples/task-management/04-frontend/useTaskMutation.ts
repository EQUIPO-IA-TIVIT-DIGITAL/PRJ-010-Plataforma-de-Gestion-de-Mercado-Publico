import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/lib/api';
import type {
  CreateTaskRequest,
  UpdateTaskRequest,
  CreateCommentRequest,
} from '../types';

export function useCreateTask() {
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: (data: CreateTaskRequest) =>
      api.post('/tasks', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tasks', 'list'] });
    },
  });

  return {
    create: (data: CreateTaskRequest, options?: { onSuccess?: () => void }) =>
      mutation.mutate(data, {
        onSuccess: () => { options?.onSuccess?.(); },
      }),
    isPending: mutation.isPending,
    error: mutation.error,
  };
}

export function useUpdateTask() {
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: ({ id, data }: { id: number; data: UpdateTaskRequest }) =>
      api.put(`/tasks/${id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tasks'] });
    },
  });

  return {
    update: (id: number, data: UpdateTaskRequest, options?: { onSuccess?: () => void }) =>
      mutation.mutate({ id, data }, {
        onSuccess: () => { options?.onSuccess?.(); },
      }),
    isPending: mutation.isPending,
    error: mutation.error,
  };
}

export function useDeleteTask() {
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: (id: number) => api.delete(`/tasks/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tasks', 'list'] });
    },
  });

  return {
    delete: (id: number, options?: { onSuccess?: () => void }) =>
      mutation.mutate(id, {
        onSuccess: () => { options?.onSuccess?.(); },
      }),
    isPending: mutation.isPending,
  };
}

export function useActivateTask() {
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: (id: number) => api.post(`/tasks/${id}/activate`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tasks'] });
    },
  });

  return {
    activate: (id: number, options?: { onSuccess?: () => void }) =>
      mutation.mutate(id, {
        onSuccess: () => { options?.onSuccess?.(); },
      }),
    isPending: mutation.isPending,
  };
}

export function useCompleteTask() {
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: (id: number) => api.post(`/tasks/${id}/complete`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tasks'] });
    },
  });

  return {
    complete: (id: number, options?: { onSuccess?: () => void }) =>
      mutation.mutate(id, {
        onSuccess: () => { options?.onSuccess?.(); },
      }),
    isPending: mutation.isPending,
  };
}

export function useCreateComment() {
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: ({ taskId, data }: { taskId: number; data: CreateCommentRequest }) =>
      api.post(`/tasks/${taskId}/comments`, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['tasks', variables.taskId, 'comments'] });
    },
  });

  return {
    createComment: (taskId: number, data: CreateCommentRequest, options?: { onSuccess?: () => void }) =>
      mutation.mutate({ taskId, data }, {
        onSuccess: () => { options?.onSuccess?.(); },
      }),
    isPending: mutation.isPending,
  };
}

export function useDeleteComment() {
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: ({ taskId, commentId }: { taskId: number; commentId: number }) =>
      api.delete(`/tasks/${taskId}/comments/${commentId}`),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['tasks', variables.taskId, 'comments'] });
    },
  });

  return {
    deleteComment: (taskId: number, commentId: number, options?: { onSuccess?: () => void }) =>
      mutation.mutate({ taskId, commentId }, {
        onSuccess: () => { options?.onSuccess?.(); },
      }),
    isPending: mutation.isPending,
  };
}
