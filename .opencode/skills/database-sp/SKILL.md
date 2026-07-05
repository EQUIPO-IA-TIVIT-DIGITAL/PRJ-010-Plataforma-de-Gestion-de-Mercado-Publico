---
name: database-sp
description: 'Stored procedure templates (List, Get, Create, Update, Delete, Search,
  Merge) and patterns. Trigger: When creating or modifying stored procedures or database
  functions.'
metadata:
  phase:
  - construction
  layer:
  - database
  enforcement: mandatory
  depends_on:
  - database
  - database-modeling
  - database-audit
  - database-security
  consumed_by:
  - data-access
  - api-first-backend
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: skill-contract
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Include header with full metadata + CHANGE HISTORY | ALWAYS | Traceability |
| Use TRY/CATCH with error logging | ALWAYS | Error handling |
| Use `SET NOCOUNT ON;` | ALWAYS | Performance |
| Entity name MANDATORY in all SP names | ALWAYS | `{Schema}.{Action}{Entity}` always |
| Use `WITH(NOLOCK)` on SELECT joins (SQL Server) | ALWAYS | Read performance |
| Use whitelist for dynamic sorting | ALWAYS | SQL injection prevention |
| Return created/updated record after mutation | ALWAYS | API consistency |
| Use dot notation for nested structures | ALWAYS | ORM/mapping compatibility |

## SP Types Decision
| Need | SP Type |
|------|---------|
| List with pagination | List |
| Get single record | Get |
| Insert new record | Create |
| Update existing record | Update |
| Soft delete | Delete |
| Advanced search w/o pagination | Search |
| Bulk sync (insert/update/delete) | Merge |

## Standard Structure (SQL Server)
```sql
/* ============================================================
   SP: {Schema}.{Operation}{Entity}
   Description: ...
   Author: ...
   Version: 1.0
   CHANGE HISTORY:
   | Version | Date | Author | Change |
   ============================================================ */
CREATE OR ALTER PROCEDURE [{Schema}].[{Operation}{Entity}]
    @ParamI... NVARCHAR(100),
    @ParamIPage INT = 1,
    @ParamIPageSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        ---------------------------------------------------------------
        -- STEP 1: Validations
        ---------------------------------------------------------------
        -- STEP 2: Operation
        ---------------------------------------------------------------
        -- STEP 3: Result
        ---------------------------------------------------------------
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        EXEC Log.GetErrorInfo;
    END CATCH
END
```

## CONSTANTS and VARIABLES
```sql
DECLARE @CStatusDraft INT = 1101;  -- @C prefix for constants
DECLARE @VGroupId INT;             -- @V prefix for variables
```

## Nested Structure (Dot Notation)
```sql
st.MasterTableId AS [Status.MasterTableId],
st.Name AS [Status.Name],
st.Value AS [Status.Value]
```

## Sorting Pattern (Preferred: STRING_SPLIT + CASE/WHEN)
```sql
DECLARE @VAllowedColumns NVARCHAR(MAX) = 'CreatedDate,Priority,Name,RecordCreationDate';
IF @ParamISortBy NOT IN (SELECT [value] FROM STRING_SPLIT(@VAllowedColumns, ','))
    SET @ParamISortBy = 'CreatedDate';
IF @ParamISortOrder NOT IN ('ASC', 'DESC')
    SET @ParamISortOrder = 'DESC';

ORDER BY
    CASE WHEN @ParamISortOrder = 'ASC' THEN
        CASE @ParamISortBy
            WHEN 'CreatedDate' THEN CONVERT(NVARCHAR(50), t.[CreatedDate], 126)
            WHEN 'Priority' THEN t.[Priority]
        END
    END ASC,
    CASE WHEN @ParamISortOrder = 'DESC' THEN
        CASE @ParamISortBy
            WHEN 'CreatedDate' THEN CONVERT(NVARCHAR(50), t.[CreatedDate], 126)
        END
    END DESC
OFFSET (@ParamIPage - 1) * @ParamIPageSize ROWS
FETCH NEXT @ParamIPageSize ROWS ONLY;
```

## Pagination — TotalCount in Single ResultSet
```sql
SELECT t.[{Entity}Id], ..., COUNT(*) OVER() AS [TotalCount]
FROM [{Schema}].[{Entity}] t WITH(NOLOCK)
WHERE t.[RecordStatus] = 'A'
ORDER BY ...
OFFSET (@ParamIPage - 1) * @ParamIPageSize ROWS FETCH NEXT @ParamIPageSize ROWS ONLY;
```

## Search Pattern
```sql
DECLARE @VSearchPattern NVARCHAR(102) = NULL;
IF @ParamISearchFilter IS NOT NULL AND LTRIM(RTRIM(@ParamISearchFilter)) <> ''
    SET @VSearchPattern = '%' + LTRIM(RTRIM(@ParamISearchFilter)) + '%';
-- Use in WHERE: AND (@VSearchPattern IS NULL OR ColumnA LIKE @VSearchPattern OR ColumnB LIKE @VSearchPattern)
```

## JSON Parameters Pattern
```sql
@ParamICasesJson NVARCHAR(MAX) = NULL

INSERT INTO Schema.Table (Col1, Col2)
SELECT Col1, Col2 FROM OPENJSON(@ParamICasesJson)
WITH (Col1 INT '$.col1', Col2 INT '$.col2');
```

## Error Handling — Two Mechanisms
1. **Business/Validation errors** → `SELECT ErrorCode, Field, Message; RETURN;` (not RAISERROR)
2. **System/SQL errors** → `BEGIN CATCH EXEC Log.GetErrorInfo; END CATCH`

## Authorization Pattern
```sql
IF @VHasPermission = 0
BEGIN
    SELECT 'AUTH_001' AS ErrorCode, 'userId' AS Field, 'Unauthorized' AS Message;
    RETURN;
END
```

## File Organization
```
database/{Schema}/StoredProcedures/
├── {Schema}.Create{Entity}.sql
├── {Schema}.Get{Entity}.sql
├── {Schema}.List{Entity}.sql
├── {Schema}.Update{Entity}.sql
└── {Schema}.Delete{Entity}.sql
```

## Checklist
- [ ] Header with CHANGE HISTORY table
- [ ] Parameters with `@ParamI` / `@ParamO` prefix
- [ ] Variables with `@V` / `@C` prefix
- [ ] `SET NOCOUNT ON;`
- [ ] `WITH(NOLOCK)` on all SELECT joins (SQL Server)
- [ ] TRY/CATCH with error logging
- [ ] `SET XACT_ABORT ON` if using transactions
- [ ] Safe sorting (whitelist pattern)
- [ ] Business errors via SELECT (not RAISERROR)
- [ ] Return created/updated record after mutations
- [ ] Dot notation for nested structures
- [ ] TotalCount via `COUNT(*) OVER()` in same ResultSet
