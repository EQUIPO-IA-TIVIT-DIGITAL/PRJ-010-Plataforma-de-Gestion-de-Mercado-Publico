using Microsoft.AspNetCore.Http.HttpResults;
using TasksManagement.Shared;

namespace TasksManagement.Modules.Tasks.Features.ListTasks;

public static class ListTasksEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle)
            .Produces<ApiResponse<PaginatedResult<ListTasksResponse>>>(200)
            .ProducesValidationProblem()
            .WithSummary("List Tasks");
    }

    private static async Task<IResult> Handle(
        [AsParameters] ListTasksRequest request,
        [FromServices] ListTasksHandler handler,
        [FromServices] HeaderToken headerToken,
        CancellationToken ct)
    {
        var currentUser = headerToken?.EmployeeId ?? throw new UnauthorizedAccessException();
        var (items, pagination) = await handler.HandleAsync(request, ct);
        return Results.Ok(ApiResponse<PaginatedResult<ListTasksResponse>>.Ok(
            new PaginatedResult<ListTasksResponse>(items, pagination)));
    }
}

namespace TasksManagement.Modules.Tasks.Features.GetTask;

public static class GetTaskEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:int}", Handle)
            .Produces<ApiResponse<GetTaskResponse>>(200)
            .Produces<ApiError>(404)
            .WithSummary("Get Task by ID");
    }

    private static async Task<IResult> Handle(
        int id,
        [FromServices] GetTaskHandler handler,
        [FromServices] HeaderToken headerToken,
        CancellationToken ct)
    {
        _ = headerToken?.EmployeeId ?? throw new UnauthorizedAccessException();
        var result = await handler.HandleAsync(id, ct);
        return Results.Ok(ApiResponse<GetTaskResponse>.Ok(result));
    }
}

namespace TasksManagement.Modules.Tasks.Features.CreateTask;

public static class CreateTaskEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle)
            .Produces<ApiResponse<CreateTaskResponse>>(201)
            .ProducesValidationProblem()
            .WithSummary("Create Task");
    }

    private static async Task<IResult> Handle(
        CreateTaskRequest request,
        [FromServices] CreateTaskHandler handler,
        [FromServices] HeaderToken headerToken,
        CancellationToken ct)
    {
        var currentUser = headerToken?.EmployeeId ?? throw new UnauthorizedAccessException();
        var result = await handler.HandleAsync(request, currentUser, ct);
        return Results.Created($"/api/v1/tasks/{result.TaskId}",
            ApiResponse<CreateTaskResponse>.Ok(result));
    }
}

namespace TasksManagement.Modules.Tasks.Features.UpdateTask;

public static class UpdateTaskEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:int}", Handle)
            .Produces<ApiResponse<UpdateTaskResponse>>(200)
            .ProducesValidationProblem()
            .WithSummary("Update Task");
    }

    private static async Task<IResult> Handle(
        int id,
        UpdateTaskRequest request,
        [FromServices] UpdateTaskHandler handler,
        [FromServices] HeaderToken headerToken,
        CancellationToken ct)
    {
        var currentUser = headerToken?.EmployeeId ?? throw new UnauthorizedAccessException();
        var result = await handler.HandleAsync(id, request, currentUser, ct);
        return Results.Ok(ApiResponse<UpdateTaskResponse>.Ok(result));
    }
}

namespace TasksManagement.Modules.Tasks.Features.DeleteTask;

public static class DeleteTaskEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/{id:int}", Handle)
            .Produces<ApiResponse<bool>>(200)
            .WithSummary("Delete Task");
    }

    private static async Task<IResult> Handle(
        int id,
        [FromServices] DeleteTaskHandler handler,
        [FromServices] HeaderToken headerToken,
        CancellationToken ct)
    {
        var currentUser = headerToken?.EmployeeId ?? throw new UnauthorizedAccessException();
        var result = await handler.HandleAsync(id, currentUser, ct);
        return Results.Ok(ApiResponse<bool>.Ok(result));
    }
}

namespace TasksManagement.Modules.Tasks.Features.ActivateTask;

public static class ActivateTaskEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:int}/activate", Handle)
            .Produces<ApiResponse<ActivateTaskResponse>>(200)
            .WithSummary("Activate Task");
    }

    private static async Task<IResult> Handle(
        int id,
        [FromServices] UpdateTaskStatusHandler handler,
        [FromServices] HeaderToken headerToken,
        CancellationToken ct)
    {
        var currentUser = headerToken?.EmployeeId ?? throw new UnauthorizedAccessException();
        var result = await handler.HandleAsync(id, "ACTIVE", currentUser, ct);
        return Results.Ok(ApiResponse<ActivateTaskResponse>.Ok(result));
    }
}

namespace TasksManagement.Modules.Tasks.Features.CompleteTask;

public static class CompleteTaskEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/{id:int}/complete", Handle)
            .Produces<ApiResponse<CompleteTaskResponse>>(200)
            .WithSummary("Complete Task");
    }

    private static async Task<IResult> Handle(
        int id,
        [FromServices] UpdateTaskStatusHandler handler,
        [FromServices] HeaderToken headerToken,
        CancellationToken ct)
    {
        var currentUser = headerToken?.EmployeeId ?? throw new UnauthorizedAccessException();
        var result = await handler.HandleAsync(id, "CLOSED", currentUser, ct);
        return Results.Ok(ApiResponse<CompleteTaskResponse>.Ok(result));
    }
}

namespace TasksManagement.Modules.Tasks.Features.ListComments;

public static class ListCommentsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle)
            .Produces<ApiResponse<List<ListCommentsResponse>>>(200)
            .WithSummary("List Comments");
    }

    private static async Task<IResult> Handle(
        int taskId,
        [FromServices] ListCommentsHandler handler,
        CancellationToken ct)
    {
        var items = await handler.HandleAsync(taskId, ct);
        return Results.Ok(ApiResponse<List<ListCommentsResponse>>.Ok(items));
    }
}

namespace TasksManagement.Modules.Tasks.Features.CreateComment;

public static class CreateCommentEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle)
            .Produces<ApiResponse<CreateCommentResponse>>(201)
            .WithSummary("Add Comment");
    }

    private static async Task<IResult> Handle(
        int taskId,
        CreateCommentRequest request,
        [FromServices] CreateCommentHandler handler,
        [FromServices] HeaderToken headerToken,
        CancellationToken ct)
    {
        var currentUser = headerToken?.EmployeeId ?? throw new UnauthorizedAccessException();
        var result = await handler.HandleAsync(taskId, request, currentUser, ct);
        return Results.Created($"/api/v1/tasks/{taskId}/comments/{result.CommentId}",
            ApiResponse<CreateCommentResponse>.Ok(result));
    }
}

namespace TasksManagement.Modules.Tasks.Features.DeleteComment;

public static class DeleteCommentEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/{commentId:int}", Handle)
            .Produces<ApiResponse<bool>>(200)
            .WithSummary("Delete Comment");
    }

    private static async Task<IResult> Handle(
        int taskId,
        int commentId,
        [FromServices] DeleteCommentHandler handler,
        [FromServices] HeaderToken headerToken,
        CancellationToken ct)
    {
        var currentUser = headerToken?.EmployeeId ?? throw new UnauthorizedAccessException();
        var result = await handler.HandleAsync(commentId, currentUser, ct);
        return Results.Ok(ApiResponse<bool>.Ok(result));
    }
}
