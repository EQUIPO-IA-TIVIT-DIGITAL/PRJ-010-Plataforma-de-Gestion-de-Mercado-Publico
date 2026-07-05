---
name: database-security
description: 'SQL security validations: reserved words, invalid characters, error
  code catalog, input validation patterns, and safe dynamic sorting. Trigger: When
  implementing SP/query validations, error codes, or SQL injection prevention.'
metadata:
  phase:
  - construction
  layer:
  - database
  enforcement: mandatory
  depends_on:
  - database
  consumed_by:
  - database-sp
  agent_roles:
  - control-agent
  - design-agent
  validation_profile: security-review
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Validate reserved words before any dynamic name usage | ALWAYS | Prevent SQL injection via object names |
| Validate invalid characters on all free-text inputs | ALWAYS | Prevent injection via special chars |
| Use QUOTENAME + whitelist for dynamic sorting | ALWAYS | Only safe columns in ORDER BY |
| Use standard error code prefixes (VAL_, {MOD}_, SYS_, AUTH_) | ALWAYS | Consistent error handling |
| Return errors as `SELECT ErrorCode, Field, Message` + `RETURN` | ALWAYS | Compatible with backend error handler |
| Normalize pagination params, don't reject | ALWAYS | UX-friendly |
| NEVER use string concatenation in SQL queries | NEVER | SQL injection prevention |
| Always use parameterized queries | ALWAYS | Input safety |

## Reserved Word Validation (SQL Server)
```sql
CREATE OR ALTER FUNCTION Cnfg.IsReservedWord (@ParamIWord VARCHAR(100)) RETURNS BIT AS
BEGIN
    DECLARE @VResult BIT = 0;
    IF EXISTS (SELECT 1 FROM Cnfg.SQLReservedWords WHERE Word = UPPER(LTRIM(RTRIM(@ParamIWord))))
        SET @VResult = 1;
    RETURN @VResult;
END
```

## Invalid Characters Validation (SQL Server)
```sql
CREATE OR ALTER FUNCTION Cnfg.HasInvalidCharacters (@ParamIValue VARCHAR(500)) RETURNS BIT AS
BEGIN
    DECLARE @VResult BIT = 0;
    IF @ParamIValue LIKE '%[-;''"%*]%'
       OR @ParamIValue LIKE '%[-][-]%'
       OR @ParamIValue LIKE '%[/][*]%'
       OR @ParamIValue LIKE '%[*][/]%'
       OR CHARINDEX(CHAR(0), @ParamIValue) > 0
        SET @VResult = 1;
    RETURN @VResult;
END
```

## Error Code Catalog

### Validation Errors (VAL_)
| Code | Description |
|------|-------------|
| VAL_001 | Required field |
| VAL_002 | Invalid format |
| VAL_003 | SQL reserved word |
| VAL_004 | Invalid characters |
| VAL_005 | Invalid date range |
| VAL_006 | Invalid JSON syntax |
| VAL_007 | Value out of range |
| VAL_008 | Length exceeded |

### Business Errors ({MOD}_)
| Code | Description |
|------|-------------|
| {MOD}_001 | Record not found |
| {MOD}_002 | Duplicate record |
| {MOD}_003 | Record in use (cannot delete) |
| {MOD}_004 | State does not allow this operation |
| {MOD}_005 | Limit exceeded |

### System Errors (SYS_)
| Code | Description |
|------|-------------|
| SYS_001 | Internal system error |

### Auth Errors (AUTH_)
| Code | Description |
|------|-------------|
| AUTH_001 | Unauthorized |
| AUTH_002 | Token expired |
| AUTH_003 | Insufficient permissions |

## Safe Dynamic Sorting (Anti-Injection, SQL Server)
```sql
DECLARE @AllowedColumns TABLE (ColName VARCHAR(50));
INSERT INTO @AllowedColumns VALUES ('Code'), ('Name'), ('Amount'), ('RecordCreationDate');

DECLARE @VProcessedSort VARCHAR(200) = REPLACE(REPLACE(@ParamISortBy,' ',''),';','');
DECLARE @VSortColumnValidated VARCHAR(MAX) = (
    SELECT STRING_AGG(QUOTENAME(s.value), ', ')
    FROM STRING_SPLIT(@VProcessedSort, ',') s
    WHERE EXISTS (SELECT 1 FROM @AllowedColumns WHERE ColName = s.value)
);
IF ISNULL(@VSortColumnValidated, '') = ''
    SET @VSortColumnValidated = QUOTENAME('RecordCreationDate');

DECLARE @VValidSortOrder VARCHAR(4) =
    CASE WHEN UPPER(@ParamISortOrder) IN ('ASC','DESC') THEN UPPER(@ParamISortOrder) ELSE 'ASC' END;
```

## PostgreSQL Equivalent
```sql
-- Whitelist approach for dynamic ORDER BY
IF sort_column NOT IN ('name', 'created_at', 'amount') THEN
    sort_column := 'created_at';
END IF;
-- Then build query with EXECUTE ... USING
```
