namespace TasksManagement.Modules.Tasks.Features.ListTasks;

public class ListTasksResponse
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? AssignedTo { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

namespace TasksManagement.Modules.Tasks.Features.GetTask;

public class GetTaskResponse
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public int CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedDate { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
}

namespace TasksManagement.Modules.Tasks.Features.CreateTask;

public class CreateTaskResponse
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

namespace TasksManagement.Modules.Tasks.Features.UpdateTask;

public class UpdateTaskResponse
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

namespace TasksManagement.Modules.Tasks.Features.ActivateTask;

public class ActivateTaskResponse
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

namespace TasksManagement.Modules.Tasks.Features.ListComments;

public class ListCommentsResponse
{
    public int CommentId { get; set; }
    public int TaskId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

namespace TasksManagement.Modules.Tasks.Features.CreateComment;

public class CreateCommentResponse
{
    public int CommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
