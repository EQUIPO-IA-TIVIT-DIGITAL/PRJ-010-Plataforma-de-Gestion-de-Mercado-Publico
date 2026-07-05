---
name: database
description: 'SQL database conventions for projects: schemas, naming, parameters,
  errors, pagination, logging. Covers SQL Server, PostgreSQL, and MySQL patterns.
  Trigger: When working with SQL databases, tables, schemas, or database design.'
metadata:
  phase:
  - construction
  layer:
  - database
  enforcement: mandatory
  depends_on: []
  consumed_by:
  - database-modeling
  - database-audit
  - database-security
  - database-sp
  - data-access
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Use `WITH(NOLOCK)` hints on reads (SQL Server) | RECOMMENDED | Prevent locks in high-concurrency |
| Filter soft-deleted records in all queries | ALWAYS | Avoid stale data leaks |
| Use timezone-aware timestamps | ALWAYS | Avoid timezone bugs |
| Primary Keys with IDENTITY: use DESC ordering (SQL Server) | RECOMMENDED | Recent records first |
| Booleans as `BIT NOT NULL DEFAULT 0` (SQL Server) or `BOOLEAN NOT NULL DEFAULT FALSE` | ALWAYS | No NULLs in boolean logic |
| Use `SELECT *` | NEVER | Performance, fragile mappings |

## Standard Schemas (SQL Server)
| Schema | Purpose |
|--------|---------|
| `Log` | Logging and errors |
| `Sec` | Security/Auth |
| `Cnfg` | Configuration/Catalogs |
| `Core` | Main transactional business |
| `Mstr` | Master data |
| `Ext` | External data (ERP integrations) |
| `Rpt` | Report views |

For PostgreSQL/MySQL: use separate schemas or prefix conventions (e.g., `log_`, `core_`).

## Naming Conventions

### Tables & Columns
| Type | Pattern | Example |
|------|---------|---------|
| Table | Singular, UpperCamelCase, max 50 chars | `Transaction`, `Employee` |
| Primary Key | `{TableName}Id` | `TransactionId` |
| Foreign Key | `{ReferencedTable}Id` | `StatusId` |
| Boolean | `Is{Name}` | `IsActive` |
| Date | `{Name}Date` | `ExpirationDate` |

**All names in English** — tables, columns, stored procedures/functions.

### ID Types
| Type | When to Use |
|------|-------------|
| `INT IDENTITY(1,1)` / `SERIAL` | Default — internal tables |
| `BIGINT IDENTITY` / `BIGSERIAL` | High volume tables |
| `UNIQUEIDENTIFIER` / `UUID` | External integration, distributed sync |

### Stored Procedures / Functions
| Operation | Pattern | Example |
|-----------|---------|---------|
| List (paginated) | `List{Entity}` | `Core.ListItems` |
| Get by ID | `Get{Entity}` | `Personnel.GetEmployee` |
| Create | `Create{Entity}` | `Core.CreateItem` |
| Update | `Update{Entity}` | `Core.UpdateItem` |
| Delete (soft) | `Delete{Entity}` | `Core.DeleteItem` |
| State transition | `{Verb}{Entity}` | `Operations.SubmitTask` |

### Constraints & Indexes
| Type | SQL Server Pattern | Example |
|------|------------|---------|
| Primary Key | `PK_{Schema}_{Table}` | `PK_Core_Transaction` |
| Foreign Key | `FK_{Schema}_{Table}_{Relation}` | `FK_Core_Transaction_Status` |
| Unique | `UN_{Schema}_{Table}_{Columns}` | `UN_Mstr_Person_DocumentNumber` |
| Check | `CK_{Schema}_{Table}_{Column}` | `CK_Cnfg_MasterTable_RecordStatus` |
| Index NC | `IXN_{Schema}_{Table}_{Columns}` | `IXN_Core_Transaction_StatusId` |

## Parameters & Variables (SQL Server SP convention)
| Type | Prefix | Example |
|------|--------|---------|
| Input param | `@ParamI` | `@ParamIPage` |
| Output param | `@ParamO` | `@ParamOTotalRecords` |
| Variable | `@V` | `@VOffset` |
| Constant | `@C` | `@CMaxPageSize` |

## Audit Columns (Required on All Transactional Tables)
| Column | SQL Server Type | Purpose |
|--------|------------|---------|
| RecordCreationUser | `VARCHAR(50) NOT NULL` | Who created |
| RecordCreationDate | `DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET()` | When created |
| RecordEditUser | `VARCHAR(50) NULL` | Who last modified |
| RecordEditDate | `DATETIMEOFFSET(7) NULL` | When last modified |
| RecordStatus | `CHAR(1) NOT NULL DEFAULT 'A'` | Soft delete status |

For PostgreSQL: use `TIMESTAMPTZ` instead of `DATETIMEOFFSET`.

### RecordStatus Values
| Value | Meaning | Usage |
|-------|---------|-------|
| `A` | Active | Default for new records |
| `I` | Inactive | Disabled but visible in admin |
| `*` | Logical Delete | Soft-deleted, filtered out in all queries |

## Error Codes
| Prefix | Use | HTTP |
|--------|-----|------|
| `VAL_` | Input validation | 400 |
| `{MOD}_001` | Not found | 404 |
| `{MOD}_002` | Duplicate/Conflict | 409 |
| `{MOD}_003+` | Business rule | 422 |
| `AUTH_` | Authorization | 403 |
| `SYS_` | System error | 500 |

## Pagination (SQL Server)
```sql
SELECT ..., COUNT(*) OVER() AS [TotalCount]
FROM [{Schema}].[{Entity}]
ORDER BY ...
OFFSET (@ParamIPage - 1) * @ParamIPageSize ROWS
FETCH NEXT @ParamIPageSize ROWS ONLY;
```

For PostgreSQL: use `LIMIT @page_size OFFSET (@page - 1) * @page_size`.

## Table Access Order (Prevent Deadlocks)
1. Master data (Mstr)
2. Configuration (Cnfg)
3. Transactional (Core / Custom schema)
4. Logging (Log — last)
