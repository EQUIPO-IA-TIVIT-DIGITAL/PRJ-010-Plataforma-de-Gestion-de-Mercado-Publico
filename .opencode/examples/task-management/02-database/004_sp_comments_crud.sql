/* ============================================================
   SP: Tasks.CreateComment
   Description: Adds a comment to a task
   Author: Framework Generated
   Version: 1.0
   CHANGE HISTORY:
   | Version | Date | Author | Change |
   |---------|------|--------|--------|
   | 1.0 | 2025-03-01 | Framework | Initial version |
   ============================================================ */
CREATE OR ALTER PROCEDURE [Tasks].[CreateComment]
    @ParamITaskId INT,
    @ParamIContent NVARCHAR(2000),
    @ParamICurrentUserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        DECLARE @VTaskStatus NVARCHAR(20);

        SELECT @VTaskStatus = [Status]
        FROM [Tasks].[Tasks] WITH(NOLOCK)
        WHERE [TaskId] = @ParamITaskId AND [RecordStatus] = 'A';

        IF @VTaskStatus IS NULL
        BEGIN
            SELECT 'TSK_001' AS ErrorCode, 'TaskId' AS Field, 'Task not found' AS Message;
            RETURN;
        END

        IF @VTaskStatus = 'CLOSED'
        BEGIN
            SELECT 'BUS_003' AS ErrorCode, 'TaskId' AS Field, 'Cannot add comments to closed tasks' AS Message;
            RETURN;
        END

        IF @ParamIContent IS NULL OR LTRIM(RTRIM(@ParamIContent)) = ''
        BEGIN
            SELECT 'VAL_001' AS ErrorCode, 'Content' AS Field, 'Content is required' AS Message;
            RETURN;
        END

        INSERT INTO [Tasks].[Comments] ([TaskId], [Content], [CreatedBy])
        VALUES (@ParamITaskId, LTRIM(RTRIM(@ParamIContent)), @ParamICurrentUserId);

        DECLARE @VNewCommentId INT = SCOPE_IDENTITY();

        SELECT
            c.[CommentId], c.[TaskId], c.[Content],
            c.[CreatedBy], u.[Name] AS [CreatedByName], c.[CreatedDate]
        FROM [Tasks].[Comments] c WITH(NOLOCK)
        INNER JOIN [Users].[Users] u WITH(NOLOCK) ON c.[CreatedBy] = u.[UserId]
        WHERE c.[CommentId] = @VNewCommentId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        EXEC [Log].[GetErrorInfo];
    END CATCH
END;
GO

/* ============================================================
   SP: Tasks.ListComments
   Description: Lists all comments for a task
   ============================================================ */
CREATE OR ALTER PROCEDURE [Tasks].[ListComments]
    @ParamITaskId INT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SELECT
            c.[CommentId], c.[TaskId], c.[Content],
            c.[CreatedBy], u.[Name] AS [CreatedByName], c.[CreatedDate]
        FROM [Tasks].[Comments] c WITH(NOLOCK)
        INNER JOIN [Users].[Users] u WITH(NOLOCK) ON c.[CreatedBy] = u.[UserId]
        WHERE c.[TaskId] = @ParamITaskId AND c.[RecordStatus] = 'A'
        ORDER BY c.[CreatedDate] DESC;
    END TRY
    BEGIN CATCH
        EXEC [Log].[GetErrorInfo];
    END CATCH
END;
GO

/* ============================================================
   SP: Tasks.DeleteComment
   Description: Soft deletes a comment (only by creator)
   ============================================================ */
CREATE OR ALTER PROCEDURE [Tasks].[DeleteComment]
    @ParamICommentId INT,
    @ParamICurrentUserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        DECLARE @VCreatedBy INT;

        SELECT @VCreatedBy = [CreatedBy]
        FROM [Tasks].[Comments] WITH(NOLOCK)
        WHERE [CommentId] = @ParamICommentId AND [RecordStatus] = 'A';

        IF @VCreatedBy IS NULL
        BEGIN
            SELECT 'TSK_001' AS ErrorCode, 'CommentId' AS Field, 'Comment not found' AS Message;
            RETURN;
        END

        IF @VCreatedBy <> @ParamICurrentUserId
        BEGIN
            SELECT 'TSK_004' AS ErrorCode, 'CommentId' AS Field, 'Only the creator can delete this comment' AS Message;
            RETURN;
        END

        UPDATE [Tasks].[Comments]
        SET [RecordStatus] = 'I'
        WHERE [CommentId] = @ParamICommentId;

        SELECT 1 AS Result;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        EXEC [Log].[GetErrorInfo];
    END CATCH
END;
GO
