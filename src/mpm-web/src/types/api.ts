export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message: string | null;
  errors: ErrorDetail[] | null;
  pagination: import('./licitacion').PaginationInfo | null;
}

export interface ErrorDetail {
  code: string | null;
  field: string | null;
  message: string | null;
}
