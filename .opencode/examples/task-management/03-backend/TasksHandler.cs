using Dapper;
using System.Data;

namespace TasksManagement.Modules.Tasks.Features.ListTasks;

public class ListTasksHandler(IDbConnection db)
{
    public async Task<(List<ListTasksResponse>, PaginationResult)> HandleAsync(
        ListTasksRequest request, CancellationToken ct = default)
    {
        var command = new CommandDefinition(
            "Tasks.ListTasks",
            new
            {
                ParamIPage = request.Page,
                ParamIPageSize = request.PageSize,
                ParamISortBy = request.SortBy ?? "CreatedDate",
                ParamISortOrder = request.SortOrder ?? "DESC",
                ParamISearchFilter = request.SearchFilter,
                ParamIStatus = request.Status,
                ParamIAssignedTo = request.AssignedTo
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        var results = await db.QueryAsync<dynamic>(command);
        var items = new List<ListTasksResponse>();
        int totalCount = 0;

        foreach (var row in results)
        {
            var dict = (IDictionary<string, object>)row;
            SpResultHelper.ThrowIfError(dict);

            totalCount = dict.GetValue<int>("TotalCount");
            items.Add(new ListTasksResponse
            {
                TaskId = dict.GetValue<int>("TaskId"),
                Title = dict.GetValue<string>("Title") ?? string.Empty,
                Priority = dict.GetValue<string>("Priority") ?? string.Empty,
                Status = dict.GetValue<string>("Status") ?? string.Empty,
                AssignedTo = dict.GetValue<int?>("AssignedTo"),
                AssignedToName = dict.GetValue<string>("AssignedToName") ?? string.Empty,
                CreatedDate = dict.GetValue<DateTime>("CreatedDate"),
            });
        }

        return (items, new PaginationResult(request.Page, request.PageSize, totalCount));
    }
}

namespace TasksManagement.Modules.Tasks.Features.GetTask;

public class GetTaskHandler(IDbConnection db)
{
    public async Task<GetTaskResponse> HandleAsync(int taskId, CancellationToken ct = default)
    {
        var command = new CommandDefinition(
            "Tasks.GetTask",
            new { ParamITaskId = taskId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        var result = await db.QuerySingleAsync<dynamic>(command);
        SpResultHelper.ThrowIfError((IDictionary<string, object>)result);

        var dict = (IDictionary<string, object>)result;
        return new GetTaskResponse
        {
            TaskId = dict.GetValue<int>("TaskId"),
            Title = dict.GetValue<string>("Title") ?? string.Empty,
            Description = dict.GetValue<string>("Description"),
            Priority = dict.GetValue<string>("Priority") ?? string.Empty,
            Status = dict.GetValue<string>("Status") ?? string.Empty,
            AssignedTo = dict.GetValue<int?>("AssignedTo"),
            AssignedToName = dict.GetValue<string>("AssignedToName"),
            CreatedBy = dict.GetValue<int>("CreatedBy"),
            CreatedByName = dict.GetValue<string>("CreatedByName"),
            CreatedDate = dict.GetValue<DateTime>("CreatedDate"),
            UpdatedBy = dict.GetValue<int?>("UpdatedBy"),
            UpdatedDate = dict.GetValue<DateTime?>("UpdatedDate"),
        };
    }
}

namespace TasksManagement.Modules.Tasks.Features.CreateTask;

public class CreateTaskHandler(IDbConnection db)
{
    public async Task<CreateTaskResponse> HandleAsync(
        CreateTaskRequest request, int currentUserId, CancellationToken ct = default)
    {
        var command = new CommandDefinition(
            "Tasks.CreateTask",
            new
            {
                ParamITitle = request.Title,
                ParamIDescription = request.Description,
                ParamIPriority = request.Priority ?? "MEDIUM",
                ParamIAssignedTo = request.AssignedTo,
                ParamICurrentUserId = currentUserId
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        var result = await db.QuerySingleAsync<dynamic>(command);
        SpResultHelper.ThrowIfError((IDictionary<string, object>)result);

        var dict = (IDictionary<string, object>)result;
        return new CreateTaskResponse
        {
            TaskId = dict.GetValue<int>("TaskId"),
            Title = dict.GetValue<string>("Title") ?? string.Empty,
            Status = dict.GetValue<string>("Status") ?? string.Empty,
        };
    }
}

namespace TasksManagement.Modules.Tasks.Features.UpdateTask;

public class UpdateTaskHandler(IDbConnection db)
{
    public async Task<UpdateTaskResponse> HandleAsync(
        int taskId, UpdateTaskRequest request, int currentUserId, CancellationToken ct = default)
    {
        var command = new CommandDefinition(
            "Tasks.UpdateTask",
            new
            {
                ParamITaskId = taskId,
                ParamITitle = request.Title,
                ParamIDescription = request.Description,
                ParamIPriority = request.Priority,
                ParamIAssignedTo = request.AssignedTo,
                ParamICurrentUserId = currentUserId
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        var result = await db.QuerySingleAsync<dynamic>(command);
        SpResultHelper.ThrowIfError((IDictionary<string, object>)result);

        var dict = (IDictionary<string, object>)result;
        return new UpdateTaskResponse
        {
            TaskId = dict.GetValue<int>("TaskId"),
            Title = dict.GetValue<string>("Title") ?? string.Empty,
            Status = dict.GetValue<string>("Status") ?? string.Empty,
        };
    }
}

namespace TasksManagement.Modules.Tasks.Features.DeleteTask;

public class DeleteTaskHandler(IDbConnection db)
{
    public async Task<bool> HandleAsync(int taskId, int currentUserId, CancellationToken ct = default)
    {
        var command = new CommandDefinition(
            "Tasks.DeleteTask",
            new { ParamITaskId = taskId, ParamICurrentUserId = currentUserId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        var results = await db.QueryAsync<dynamic>(command);
        var first = results.FirstOrDefault();
        if (first != null)
            SpResultHelper.ThrowIfError((IDictionary<string, object>)first);

        return true;
    }
}

namespace TasksManagement.Modules.Tasks.Features.ActivateTask;

public class UpdateTaskStatusHandler(IDbConnection db)
{
    public async Task<ActivateTaskResponse> HandleAsync(
        int taskId, string newStatus, int currentUserId, CancellationToken ct = default)
    {
        var command = new CommandDefinition(
            "Tasks.UpdateTaskStatus",
            new
            {
                ParamITaskId = taskId,
                ParamINewStatus = newStatus,
                ParamICurrentUserId = currentUserId
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        var result = await db.QuerySingleAsync<dynamic>(command);
        SpResultHelper.ThrowIfError((IDictionary<string, object>)result);

        var dict = (IDictionary<string, object>)result;
        return new ActivateTaskResponse
        {
            TaskId = dict.GetValue<int>("TaskId"),
            Title = dict.GetValue<string>("Title") ?? string.Empty,
            Status = dict.GetValue<string>("Status") ?? string.Empty,
        };
    }
}

namespace TasksManagement.Modules.Tasks.Features.ListComments;

public class ListCommentsHandler(IDbConnection db)
{
    public async Task<List<ListCommentsResponse>> HandleAsync(
        int taskId, CancellationToken ct = default)
    {
        var command = new CommandDefinition(
            "Tasks.ListComments",
            new { ParamITaskId = taskId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        var results = await db.QueryAsync<dynamic>(command);
        var items = new List<ListCommentsResponse>();

        foreach (var row in results)
        {
            var dict = (IDictionary<string, object>)row;
            items.Add(new ListCommentsResponse
            {
                CommentId = dict.GetValue<int>("CommentId"),
                TaskId = dict.GetValue<int>("TaskId"),
                Content = dict.GetValue<string>("Content") ?? string.Empty,
                CreatedBy = dict.GetValue<int>("CreatedBy"),
                CreatedByName = dict.GetValue<string>("CreatedByName") ?? string.Empty,
                CreatedDate = dict.GetValue<DateTime>("CreatedDate"),
            });
        }

        return items;
    }
}

namespace TasksManagement.Modules.Tasks.Features.CreateComment;

public class CreateCommentHandler(IDbConnection db)
{
    public async Task<CreateCommentResponse> HandleAsync(
        int taskId, CreateCommentRequest request, int currentUserId, CancellationToken ct = default)
    {
        var command = new CommandDefinition(
            "Tasks.CreateComment",
            new
            {
                ParamITaskId = taskId,
                ParamIContent = request.Content,
                ParamICurrentUserId = currentUserId
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        var result = await db.QuerySingleAsync<dynamic>(command);
        SpResultHelper.ThrowIfError((IDictionary<string, object>)result);

        var dict = (IDictionary<string, object>)result;
        return new CreateCommentResponse
        {
            CommentId = dict.GetValue<int>("CommentId"),
            Content = dict.GetValue<string>("Content") ?? string.Empty,
            CreatedByName = dict.GetValue<string>("CreatedByName") ?? string.Empty,
            CreatedDate = dict.GetValue<DateTime>("CreatedDate"),
        };
    }
}

namespace TasksManagement.Modules.Tasks.Features.DeleteComment;

public class DeleteCommentHandler(IDbConnection db)
{
    public async Task<bool> HandleAsync(int commentId, int currentUserId, CancellationToken ct = default)
    {
        var command = new CommandDefinition(
            "Tasks.DeleteComment",
            new { ParamICommentId = commentId, ParamICurrentUserId = currentUserId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        var results = await db.QueryAsync<dynamic>(command);
        var first = results.FirstOrDefault();
        if (first != null)
            SpResultHelper.ThrowIfError((IDictionary<string, object>)first);

        return true;
    }
}
