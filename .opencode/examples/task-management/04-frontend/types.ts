// --- API Response Wrappers ---

export interface ApiResponse<T> {
  data: T;
  success: boolean;
  message: string | null;
}

export interface PaginatedData<T> {
  items: T[];
  pagination: Pagination;
}

export interface Pagination {
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
}

// --- Task Types ---

export type TaskPriority = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL';
export type TaskStatus = 'DRAFT' | 'ACTIVE' | 'CLOSED';

export interface TaskListItem {
  taskId: number;
  title: string;
  priority: TaskPriority;
  status: TaskStatus;
  assignedTo: number | null;
  assignedToName: string;
  createdDate: string;
}

export interface TaskDetail {
  taskId: number;
  title: string;
  description: string | null;
  priority: TaskPriority;
  status: TaskStatus;
  assignedTo: number | null;
  assignedToName: string | null;
  createdBy: number;
  createdByName: string;
  createdDate: string;
  updatedBy: number | null;
  updatedDate: string | null;
}

export interface CreateTaskRequest {
  title: string;
  description?: string;
  priority?: TaskPriority;
  assignedTo?: number;
}

export interface UpdateTaskRequest {
  title?: string;
  description?: string;
  priority?: TaskPriority;
  assignedTo?: number;
}

// --- Comment Types ---

export interface CommentItem {
  commentId: number;
  taskId: number;
  content: string;
  createdBy: number;
  createdByName: string;
  createdDate: string;
}

export interface CreateCommentRequest {
  content: string;
}

// --- Query Params ---

export interface TaskListParams {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortOrder?: string;
  searchFilter?: string;
  status?: TaskStatus;
  assignedTo?: number;
}

// --- Form Types ---

export interface TaskFormValues {
  title: string;
  description: string;
  priority: TaskPriority;
  assignedTo: number | null;
}

export interface CommentFormValues {
  content: string;
}
