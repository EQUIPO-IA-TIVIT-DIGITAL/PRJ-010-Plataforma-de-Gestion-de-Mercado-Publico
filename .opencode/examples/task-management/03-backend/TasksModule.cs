using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TasksManagement.Modules.Tasks.Features.ListTasks;
using TasksManagement.Modules.Tasks.Features.GetTask;
using TasksManagement.Modules.Tasks.Features.CreateTask;
using TasksManagement.Modules.Tasks.Features.UpdateTask;
using TasksManagement.Modules.Tasks.Features.DeleteTask;
using TasksManagement.Modules.Tasks.Features.ActivateTask;
using TasksManagement.Modules.Tasks.Features.CompleteTask;
using TasksManagement.Modules.Tasks.Features.ListComments;
using TasksManagement.Modules.Tasks.Features.CreateComment;
using TasksManagement.Modules.Tasks.Features.DeleteComment;

namespace TasksManagement.Modules.Tasks;

public static class TasksModule
{
    public static IServiceCollection AddTasksModule(this IServiceCollection services)
    {
        services.AddScoped<ListTasksHandler>();
        services.AddScoped<GetTaskHandler>();
        services.AddScoped<CreateTaskHandler>();
        services.AddScoped<UpdateTaskHandler>();
        services.AddScoped<DeleteTaskHandler>();
        services.AddScoped<UpdateTaskStatusHandler>();
        services.AddScoped<ListCommentsHandler>();
        services.AddScoped<CreateCommentHandler>();
        services.AddScoped<DeleteCommentHandler>();

        services.AddValidatorsFromAssemblyContaining<CreateTaskValidator>();

        return services;
    }

    public static void MapTasksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tasks")
            .WithTags("Tasks")
            .RequireAuthorization();

        ListTasksEndpoint.Map(group);
        GetTaskEndpoint.Map(group);
        CreateTaskEndpoint.Map(group);
        UpdateTaskEndpoint.Map(group);
        DeleteTaskEndpoint.Map(group);
        ActivateTaskEndpoint.Map(group);
        CompleteTaskEndpoint.Map(group);

        var commentGroup = group.MapGroup("/{taskId:int}/comments");
        ListCommentsEndpoint.Map(commentGroup);
        CreateCommentEndpoint.Map(commentGroup);
        DeleteCommentEndpoint.Map(commentGroup);
    }
}
