namespace TasksManagement.Modules.Tasks.Features.ListTasks;

public record ListTasksRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
    public string? SearchFilter { get; init; }
    public string? Status { get; init; }
    public int? AssignedTo { get; init; }
}

namespace TasksManagement.Modules.Tasks.Features.CreateTask;

public record CreateTaskRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Priority { get; init; }
    public int? AssignedTo { get; init; }
}

public class CreateTaskValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithErrorCode("VAL_001").WithMessage("Title is required")
            .MaximumLength(200).WithErrorCode("VAL_008").WithMessage("Title max length is 200");

        RuleFor(x => x.Priority)
            .Must(p => p is null or "LOW" or "MEDIUM" or "HIGH" or "CRITICAL")
            .WithErrorCode("VAL_002").WithMessage("Invalid priority value");
    }
}

namespace TasksManagement.Modules.Tasks.Features.UpdateTask;

public record UpdateTaskRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Priority { get; init; }
    public int? AssignedTo { get; init; }
}

namespace TasksManagement.Modules.Tasks.Features.CreateComment;

public record CreateCommentRequest
{
    public string Content { get; init; } = string.Empty;
}

public class CreateCommentValidator : AbstractValidator<CreateCommentRequest>
{
    public CreateCommentValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithErrorCode("VAL_001").WithMessage("Content is required")
            .MaximumLength(2000).WithErrorCode("VAL_008").WithMessage("Content max length is 2000");
    }
}
