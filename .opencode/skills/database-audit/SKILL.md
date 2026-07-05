---
name: database-audit
description: 'Audit columns, logging tables, and error capture for SQL databases.
  Covers soft delete (RecordStatus), Log tables, and error logging stored procedures.
  Trigger: When creating tables with audit columns, implementing error logging, or
  HTTP auditing.'
metadata:
  phase:
  - construction
  layer:
  - database
  enforcement: mandatory
  depends_on:
  - database
  consumed_by:
  - database-modeling
  - database-sp
  agent_roles:
  - design-agent
  - control-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| ALL transactional tables MUST have 5 audit columns | ALWAYS | Consistent tracking |
| RecordStatus MUST have a CHECK constraint | ALWAYS | Prevents invalid values |
| Use timezone-aware timestamps | ALWAYS | Consistent across timezones |
| Soft delete sets RecordStatus = '*', NEVER physical DELETE | ALWAYS | Audit trail preservation |
| ALWAYS filter `RecordStatus = 'A'` in JOINs | ALWAYS | Prevent stale data leaks |
| Error capture in every CATCH block | ALWAYS | Centralized error logging |

## Soft Delete Pattern
```sql
-- SQL Server
UPDATE {Schema}.{Table}
SET RecordStatus = '*',
    RecordEditUser = @ParamIRecordEditUser,
    RecordEditDate = SYSDATETIMEOFFSET()
WHERE {Table}Id = @ParamIId;
```

## User Parameters in DB Operations
| Operation | Required Parameter |
|-----------|--------------------|
| CREATE | `@ParamIRecordCreationUser VARCHAR(50)` |
| UPDATE | `@ParamIRecordEditUser VARCHAR(50)` |
| DELETE (soft) | `@ParamIRecordEditUser VARCHAR(50)` |

## Log Tables

### Log.LogDB (SQL Server — DB error log)
```sql
[LogDBId] INT IDENTITY(1,1),
[ErrorNumber] INT,
[ErrorSeverity] INT,
[ErrorState] INT,
[ErrorProcedure] VARCHAR(150),
[ErrorLine] INT,
[ErrorMessage] VARCHAR(500),
[CreateDate] DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET()
-- PK: LogDBId DESC
```

### Log.AuditHttp (HTTP request audit)
```sql
[AuditHttpId] INT IDENTITY(1,1),
[HttpStatusCode] INT,
[Path] VARCHAR(500),
[Method] VARCHAR(10),
[RequestBody] NVARCHAR(MAX),
[ResponseBody] NVARCHAR(MAX),
[CorrelationId] VARCHAR(50),
[IpAddress] VARCHAR(50),
[Duration] VARCHAR(20),
[CreateDate] DATETIMEOFFSET(7)
```

## Error Logging SP Pattern (SQL Server)
```sql
CREATE OR ALTER PROCEDURE [Log].[GetErrorInfo] AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @VErrorNumber INT = ERROR_NUMBER();
    DECLARE @VErrorMessage VARCHAR(500) = ERROR_MESSAGE();
    DECLARE @VLogId INT;
    
    INSERT INTO [Log].[LogDB] ([ErrorNumber], [ErrorSeverity], [ErrorState], [ErrorProcedure], [ErrorLine], [ErrorMessage])
    VALUES (ERROR_NUMBER(), ERROR_SEVERITY(), ERROR_STATE(), ERROR_PROCEDURE(), ERROR_LINE(), ERROR_MESSAGE());
    
    SET @VLogId = SCOPE_IDENTITY();
    
    SELECT 'SYS_001' AS ErrorCode, NULL AS Field,
        CONCAT('Internal error [Ref:', @VLogId, ']') AS Message;
END
```

## Usage in SPs
```sql
BEGIN TRY
    -- SP logic...
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    EXEC Log.GetErrorInfo;
END CATCH
```

## PostgreSQL Equivalent
```sql
-- Use EXCEPTION block
BEGIN
    -- logic
EXCEPTION WHEN OTHERS THEN
    INSERT INTO log_errors (error_message, created_at)
    VALUES (SQLERRM, NOW());
    RAISE;
END;
```
