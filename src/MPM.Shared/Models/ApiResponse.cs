using System;
using System.Collections.Generic;

namespace MPM.Shared.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<ErrorDetail>? Errors { get; set; }
        public PaginationInfo? Pagination { get; set; }
        public string? CorrelationId { get; set; }

        public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
        public static ApiResponse<T> Fail(string message, List<ErrorDetail>? errors = null)
            => new() { Success = false, Message = message, Errors = errors };
    }

    public class ErrorDetail
    {
        public string Code { get; set; } = string.Empty;
        public string? Field { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PaginationInfo
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public bool HasNext => Page < TotalPages;
        public bool HasPrevious => Page > 1;
    }
}
