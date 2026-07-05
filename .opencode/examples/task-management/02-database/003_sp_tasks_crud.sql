/* ============================================================
   SP: Tasks.CreateTask
   Description: Creates a new task
   Author: Framework Generated
   Version: 1.0
   CHANGE HISTORY:
   | Version | Date | Author | Change |
   |---------|------|--------|--------|
   | 1.0 | 2025-03-01 | Framework | Initial version |
   ============================================================ */
CREATE OR ALTER PROCEDURE [Tasks].[CreateTask]
    @ParamITitle NVARCHAR(200),
    @ParamIDescription NVARCHAR(2000) = NULL,
    @ParamIPriority NVARCHAR(20) = 'MEDIUM',
    @ParamIAssignedTo INT = NULL,
    @ParamICurrentUserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        IF @ParamITitle IS NULL OR LTRIM(RTRIM(@ParamITitle)) = ''
        BEGIN
            SELECT 'VAL_001' AS ErrorCode, 'Title' AS Field, 'Title is required' AS Message;
            RETURN;
        END

        IF LEN(@ParamITitle) > 200
        BEGIN
            SELECT 'VAL_008' AS ErrorCode, 'Title' AS Field, 'Title max length is 200' AS Message;
            RETURN;
        END

        IF EXISTS (
            SELECT 1 FROM [Tasks].[Tasks] WITH(NOLOCK)
            WHERE [Title] = @ParamITitle AND [CreatedBy] = @ParamICurrentUserId AND [RecordStatus] = 'A'
        )
        BEGIN
            SELECT 'TSK_002' AS ErrorCode, 'Title' AS Field, 'Task title already exists' AS Message;
            RETURN;
        END

        INSERT INTO [Tasks].[Tasks] (
            [Title], [Description], [Priority], [Status], [AssignedTo], [CreatedBy]
        ) VALUES (
            LTRIM(RTRIM(@ParamITitle)),
            NULLIF(LTRIM(RTRIM(@ParamIDescription)), ''),
            @ParamIPriority,
            'DRAFT',
            @ParamIAssignedTo,
            @ParamICurrentUserId
        );

        DECLARE @VNewId INT = SCOPE_IDENTITY();

        SELECT
            t.[TaskId], t.[Title], t.[Description], t.[Priority],
            t.[Status], t.[AssignedTo], t.[CreatedBy], t.[CreatedDate],
            t.[UpdatedBy], t.[UpdatedDate]
        FROM [Tasks].[Tasks] t WITH(NOLOCK)
        WHERE t.[TaskId] = @VNewId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        EXEC [Log].[GetErrorInfo];
    END CATCH
END;
GO

/* ============================================================
   SP: Tasks.GetTask
   Description: Gets a task by ID with user names
   ============================================================ */
CREATE OR ALTER PROCEDURE [Tasks].[GetTask]
    @ParamITaskId INT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SELECT
            t.[TaskId], t.[Title], t.[Description], t.[Priority],
            t.[Status], t.[AssignedTo],
            ua.[Name] AS [AssignedToName],
            t.[CreatedBy],
            uc.[Name] AS [CreatedByName],
            t.[CreatedDate], t.[UpdatedBy], t.[UpdatedDate]
        FROM [Tasks].[Tasks] t WITH(NOLOCK)
        LEFT JOIN [Users].[Users] ua WITH(NOLOCK) ON t.[AssignedTo] = ua.[UserId]
        LEFT JOIN [Users].[Users] uc WITH(NOLOCK) ON t.[CreatedBy] = uc.[UserId]
        WHERE t.[TaskId] = @ParamITaskId AND t.[RecordStatus] = 'A';

        IF @@ROWCOUNT = 0
        BEGIN
            SELECT 'TSK_001' AS ErrorCode, 'TaskId' AS Field, 'Task not found' AS Message;
            RETURN;
        END
    END TRY
    BEGIN CATCH
        EXEC [Log].[GetErrorInfo];
    END CATCH
END;
GO

/* ============================================================
   SP: Tasks.UpdateTask
   Description: Updates an existing task
   ============================================================ */
CREATE OR ALTER PROCEDURE [Tasks].[UpdateTask]
    @ParamITaskId INT,
    @ParamITitle NVARCHAR(200) = NULL,
    @ParamIDescription NVARCHAR(2000) = NULL,
    @ParamIPriority NVARCHAR(20) = NULL,
    @ParamIAssignedTo INT = NULL,
    @ParamICurrentUserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM [Tasks].[Tasks] WITH(NOLOCK) WHERE [TaskId] = @ParamITaskId AND [RecordStatus] = 'A')
        BEGIN
            SELECT 'TSK_001' AS ErrorCode, 'TaskId' AS Field, 'Task not found' AS Message;
            RETURN;
        END

        UPDATE [Tasks].[Tasks]
        SET
            [Title] = COALESCE(NULLIF(LTRIM(RTRIM(@ParamITitle)), ''), [Title]),
            [Description] = CASE WHEN @ParamIDescription IS NOT NULL THEN NULLIF(LTRIM(RTRIM(@ParamIDescription)), '') ELSE [Description] END,
            [Priority] = COALESCE(@ParamIPriority, [Priority]),
            [AssignedTo] = COALESCE(@ParamIAssignedTo, [AssignedTo]),
            [UpdatedBy] = @ParamICurrentUserId,
            [UpdatedDate] = SYSUTCDATETIME()
        WHERE [TaskId] = @ParamITaskId;

        SELECT
            t.[TaskId], t.[Title], t.[Description], t.[Priority],
            t.[Status], t.[AssignedTo], t.[CreatedBy], t.[CreatedDate],
            t.[UpdatedBy], t.[UpdatedDate]
        FROM [Tasks].[Tasks] t WITH(NOLOCK)
        WHERE t.[TaskId] = @ParamITaskId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        EXEC [Log].[GetErrorInfo];
    END CATCH
END;
GO

/* ============================================================
   SP: Tasks.DeleteTask
   Description: Soft deletes a task
   ============================================================ */
CREATE OR ALTER PROCEDURE [Tasks].[DeleteTask]
    @ParamITaskId INT,
    @ParamICurrentUserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        DECLARE @VCreatedBy INT;

        SELECT @VCreatedBy = [CreatedBy]
        FROM [Tasks].[Tasks] WITH(NOLOCK)
        WHERE [TaskId] = @ParamITaskId AND [RecordStatus] = 'A';

        IF @VCreatedBy IS NULL
        BEGIN
            SELECT 'TSK_001' AS ErrorCode, 'TaskId' AS Field, 'Task not found' AS Message;
            RETURN;
        END

        IF @VCreatedBy <> @ParamICurrentUserId
        BEGIN
            SELECT 'TSK_004' AS ErrorCode, 'TaskId' AS Field, 'Only the creator can delete this task' AS Message;
            RETURN;
        END

        UPDATE [Tasks].[Tasks]
        SET [RecordStatus] = 'I', [UpdatedBy] = @ParamICurrentUserId, [UpdatedDate] = SYSUTCDATETIME()
        WHERE [TaskId] = @ParamITaskId;

        SELECT 1 AS Result;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        EXEC [Log].[GetErrorInfo];
    END CATCH
END;
GO

/* ============================================================
   SP: Tasks.ListTasks
   Description: Paginated list of tasks
   ============================================================ */
CREATE OR ALTER PROCEDURE [Tasks].[ListTasks]
    @ParamIPage INT = 1,
    @ParamIPageSize INT = 20,
    @ParamISortBy NVARCHAR(50) = 'CreatedDate',
    @ParamISortOrder NVARCHAR(4) = 'DESC',
    @ParamISearchFilter NVARCHAR(200) = NULL,
    @ParamIStatus NVARCHAR(20) = NULL,
    @ParamIAssignedTo INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        DECLARE @VSearchPattern NVARCHAR(202) = NULL;
        DECLARE @VAllowedColumns NVARCHAR(MAX) = 'Title,CreatedDate,Priority,Status';

        IF @ParamISearchFilter IS NOT NULL AND LTRIM(RTRIM(@ParamISearchFilter)) <> ''
            SET @VSearchPattern = '%' + LTRIM(RTRIM(@ParamISearchFilter)) + '%';

        IF @ParamISortBy NOT IN (SELECT [value] FROM STRING_SPLIT(@VAllowedColumns, ','))
            SET @ParamISortBy = 'CreatedDate';
        IF @ParamISortOrder NOT IN ('ASC', 'DESC')
            SET @ParamISortOrder = 'DESC';

        SELECT
            t.[TaskId], t.[Title], t.[Priority], t.[Status],
            t.[AssignedTo], u.[Name] AS [AssignedToName],
            t.[CreatedDate], t.[CreatedBy],
            COUNT(*) OVER() AS [TotalCount]
        FROM [Tasks].[Tasks] t WITH(NOLOCK)
        LEFT JOIN [Users].[Users] u WITH(NOLOCK) ON t.[AssignedTo] = u.[UserId]
        WHERE t.[RecordStatus] = 'A'
            AND (@VSearchPattern IS NULL
                OR t.[Title] LIKE @VSearchPattern
                OR t.[Description] LIKE @VSearchPattern)
            AND (@ParamIStatus IS NULL OR t.[Status] = @ParamIStatus)
            AND (@ParamIAssignedTo IS NULL OR t.[AssignedTo] = @ParamIAssignedTo)
        ORDER BY
            CASE WHEN @ParamISortOrder = 'ASC' THEN
                CASE @ParamISortBy
                    WHEN 'CreatedDate' THEN CONVERT(NVARCHAR(50), t.[CreatedDate], 126)
                    WHEN 'Title' THEN t.[Title]
                    WHEN 'Priority' THEN t.[Priority]
                    WHEN 'Status' THEN t.[Status]
                END
            END ASC,
            CASE WHEN @ParamISortOrder = 'DESC' THEN
                CASE @ParamISortBy
                    WHEN 'CreatedDate' THEN CONVERT(NVARCHAR(50), t.[CreatedDate], 126)
                    WHEN 'Title' THEN t.[Title]
                    WHEN 'Priority' THEN t.[Priority]
                    WHEN 'Status' THEN t.[Status]
                END
            END DESC
        OFFSET (@ParamIPage - 1) * @ParamIPageSize ROWS
        FETCH NEXT @ParamIPageSize ROWS ONLY;
    END TRY
    BEGIN CATCH
        EXEC [Log].[GetErrorInfo];
    END CATCH
END;
GO

/* ============================================================
   SP: Tasks.UpdateTaskStatus
   Description: Changes task status (state machine)
   ============================================================ */
CREATE OR ALTER PROCEDURE [Tasks].[UpdateTaskStatus]
    @ParamITaskId INT,
    @ParamINewStatus NVARCHAR(20),
    @ParamICurrentUserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        DECLARE @VCurrentStatus NVARCHAR(20);

        SELECT @VCurrentStatus = [Status]
        FROM [Tasks].[Tasks] WITH(NOLOCK)
        WHERE [TaskId] = @ParamITaskId AND [RecordStatus] = 'A';

        IF @VCurrentStatus IS NULL
        BEGIN
            SELECT 'TSK_001' AS ErrorCode, 'TaskId' AS Field, 'Task not found' AS Message;
            RETURN;
        END

        IF NOT (
            (@VCurrentStatus = 'DRAFT' AND @ParamINewStatus = 'ACTIVE')
            OR (@VCurrentStatus = 'ACTIVE' AND @ParamINewStatus IN ('CLOSED'))
            OR (@VCurrentStatus = 'CLOSED' AND @ParamINewStatus = 'ACTIVE')
        )
        BEGIN
            SELECT 'TSK_003' AS ErrorCode, 'Status' AS Field,
                'Cannot transition from ' + @VCurrentStatus + ' to ' + @ParamINewStatus AS Message;
            RETURN;
        END

        IF @ParamINewStatus = 'ACTIVE' AND @VCurrentStatus = 'DRAFT'
        BEGIN
            IF EXISTS (
                SELECT 1 FROM [Tasks].[Tasks] WITH(NOLOCK)
                WHERE [TaskId] = @ParamITaskId AND ([Title] IS NULL OR [AssignedTo] IS NULL)
            )
            BEGIN
                SELECT 'BUS_001' AS ErrorCode, 'Status' AS Field,
                    'Cannot activate task: title and assigned user are required' AS Message;
                RETURN;
            END
        END

        UPDATE [Tasks].[Tasks]
        SET [Status] = @ParamINewStatus,
            [UpdatedBy] = @ParamICurrentUserId,
            [UpdatedDate] = SYSUTCDATETIME()
        WHERE [TaskId] = @ParamITaskId;

        SELECT
            t.[TaskId], t.[Title], t.[Description], t.[Priority],
            t.[Status], t.[AssignedTo], t.[CreatedBy], t.[CreatedDate],
            t.[UpdatedBy], t.[UpdatedDate]
        FROM [Tasks].[Tasks] t WITH(NOLOCK)
        WHERE t.[TaskId] = @ParamITaskId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        EXEC [Log].[GetErrorInfo];
    END CATCH
END;
GO
