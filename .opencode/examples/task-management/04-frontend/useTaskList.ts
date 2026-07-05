import { useQuery } from '@tanstack/react-query';
import { api } from '@/shared/lib/api';
import type {
  ApiResponse,
  PaginatedData,
  TaskListItem,
  TaskListParams,
} from '../types';

export function useTaskList(params?: TaskListParams, enabled = true) {
  return useQuery<ApiResponse<PaginatedData<TaskListItem>>>({
    queryKey: ['tasks', 'list', params],
    queryFn: () =>
      api.get<ApiResponse<PaginatedData<TaskListItem>>>('/tasks', {
        params: {
          page: params?.page ?? 1,
          pageSize: params?.pageSize ?? 20,
          sortBy: params?.sortBy ?? 'CreatedDate',
          sortOrder: params?.sortOrder ?? 'DESC',
          searchFilter: params?.searchFilter,
          status: params?.status,
          assignedTo: params?.assignedTo,
        },
      }),
    enabled,
    staleTime: 0,
  });
}

export function useTask(id: number | null, enabled = true) {
  return useQuery<ApiResponse<TaskDetail>>({
    queryKey: ['tasks', id],
    queryFn: () =>
      api.get<ApiResponse<TaskDetail>>(`/tasks/${id}`),
    enabled: enabled && id !== null,
    staleTime: 0,
  });
}

export function useTaskComments(taskId: number | null) {
  return useQuery<ApiResponse<CommentItem[]>>({
    queryKey: ['tasks', taskId, 'comments'],
    queryFn: () =>
      api.get<ApiResponse<CommentItem[]>>(`/tasks/${taskId}/comments`),
    enabled: taskId !== null,
  });
}

export function useTaskUsers() {
  return useQuery<UserOption[]>({
    queryKey: ['users', 'options'],
    queryFn: () =>
      api.get<UserOption[]>('/users', {
        params: { pageSize: 1000 },
        select: (data: any) =>
          data.data?.items?.map((u: any) => ({
            value: u.userId,
            label: u.name,
          })) ?? [],
      }),
    staleTime: 5 * 60 * 1000,
  });
}
