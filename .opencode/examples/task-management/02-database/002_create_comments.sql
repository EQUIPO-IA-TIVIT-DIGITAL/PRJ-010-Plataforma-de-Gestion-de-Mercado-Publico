/* ============================================================
   Table: Comments
   Description: Notes attached to tasks
   Author: Framework Generated
   Version: 1.0
   ============================================================ */
CREATE TABLE [Tasks].[Comments] (
    [CommentId]    INT              IDENTITY(1,1) NOT NULL,
    [TaskId]       INT              NOT NULL,
    [Content]      NVARCHAR(2000)   NOT NULL,
    [CreatedBy]    INT              NOT NULL,
    [CreatedDate]  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [RecordStatus] CHAR(1)          NOT NULL DEFAULT 'A',

    CONSTRAINT [PK_Comments_CommentId] PRIMARY KEY CLUSTERED ([CommentId]),
    CONSTRAINT [FK_Comments_TaskId_Tasks] FOREIGN KEY ([TaskId]) REFERENCES [Tasks].[Tasks]([TaskId]),
    CONSTRAINT [FK_Comments_CreatedBy_Users] FOREIGN KEY ([CreatedBy]) REFERENCES [Users].[Users]([UserId]),
    CONSTRAINT [CK_Comments_RecordStatus] CHECK ([RecordStatus] IN ('A', 'I'))
);

CREATE NONCLUSTERED INDEX [IX_Comments_TaskId] ON [Tasks].[Comments] ([TaskId], [CreatedDate] DESC)
    WHERE [RecordStatus] = 'A';
