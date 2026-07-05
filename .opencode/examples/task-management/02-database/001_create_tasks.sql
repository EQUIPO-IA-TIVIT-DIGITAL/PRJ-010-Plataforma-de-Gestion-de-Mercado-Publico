/* ============================================================
   Table: Tasks
   Description: Work items with assignee, priority, and status
   Author: Framework Generated
   Version: 1.0
   ============================================================ */
CREATE TABLE [Tasks].[Tasks] (
    [TaskId]       INT              IDENTITY(1,1) NOT NULL,
    [Title]        NVARCHAR(200)    NOT NULL,
    [Description]  NVARCHAR(2000)   NULL,
    [Priority]     NVARCHAR(20)     NOT NULL DEFAULT N'MEDIUM',
    [Status]       NVARCHAR(20)     NOT NULL DEFAULT N'DRAFT',
    [AssignedTo]   INT              NULL,
    [CreatedBy]    INT              NOT NULL,
    [CreatedDate]  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedBy]    INT              NULL,
    [UpdatedDate]  DATETIME2        NULL,
    [RecordStatus] CHAR(1)          NOT NULL DEFAULT 'A',

    CONSTRAINT [PK_Tasks_TaskId] PRIMARY KEY CLUSTERED ([TaskId]),
    CONSTRAINT [FK_Tasks_CreatedBy_Users] FOREIGN KEY ([CreatedBy]) REFERENCES [Users].[Users]([UserId]),
    CONSTRAINT [FK_Tasks_AssignedTo_Users] FOREIGN KEY ([AssignedTo]) REFERENCES [Users].[Users]([UserId]),
    CONSTRAINT [CK_Tasks_Status] CHECK ([Status] IN ('DRAFT', 'ACTIVE', 'CLOSED')),
    CONSTRAINT [CK_Tasks_Priority] CHECK ([Priority] IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')),
    CONSTRAINT [CK_Tasks_RecordStatus] CHECK ([RecordStatus] IN ('A', 'I'))
);

CREATE NONCLUSTERED INDEX [IX_Tasks_Status] ON [Tasks].[Tasks] ([Status])
    INCLUDE ([Title], [Priority], [AssignedTo], [CreatedDate])
    WHERE [RecordStatus] = 'A';

CREATE NONCLUSTERED INDEX [IX_Tasks_AssignedTo] ON [Tasks].[Tasks] ([AssignedTo])
    WHERE [RecordStatus] = 'A';

CREATE NONCLUSTERED INDEX [IX_Tasks_CreatedDate] ON [Tasks].[Tasks] ([CreatedDate] DESC)
    WHERE [RecordStatus] = 'A';
