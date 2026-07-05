---
name: database-modeling
description: 'Data modeling standards: schemas, table design, column types, constraints,
  indexes, functions, views, CTEs, and transactions. Trigger: When designing tables,
  creating schemas, adding constraints, or modeling data structures.'
metadata:
  phase:
  - inception
  - construction
  layer:
  - database
  enforcement: mandatory
  depends_on:
  - database
  consumed_by:
  - database-sp
  - data-access
  agent_roles:
  - design-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| PK with IDENTITY use DESC ordering (SQL Server) | ALWAYS | Recent records first |
| Booleans must be `BIT NOT NULL DEFAULT 0` (SQL Server) | ALWAYS | Avoid NULL logic complexity |
| Dates must be timezone-aware (`DATETIMEOFFSET` / `TIMESTAMPTZ`) | ALWAYS | Consistency across timezones |
| Tables must be singular UpperCamelCase | ALWAYS | Naming consistency |
| Max 50 chars for table names | ALWAYS | Avoid truncation in tooling |
| Check Constraint mandatory on RecordStatus | ALWAYS | Data integrity |
| Use GUID/UUID only for external integration | CONDITIONAL | Distributed systems |

## Schema Selection Rules
- Schema MUST be the one chosen during Requirements Analysis
- Do NOT abbreviate or invent alternatives
- If the team chose `HR`, use `HR` — NOT `Emp`, `Empl`, `RRHH`

## Table Template (SQL Server)
```sql
CREATE TABLE {Schema}.{Table} (
    {Table}Id INT IDENTITY(1,1) NOT NULL,
    -- business columns...
    RecordCreationUser VARCHAR(50) NOT NULL,
    RecordCreationDate DATETIMEOFFSET(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    RecordEditUser VARCHAR(50) NULL,
    RecordEditDate DATETIMEOFFSET(7) NULL,
    RecordStatus CHAR(1) NOT NULL DEFAULT 'A',
    CONSTRAINT PK_{Schema}_{Table} PRIMARY KEY CLUSTERED ({Table}Id DESC),
    CONSTRAINT CK_{Schema}_{Table}_RecordStatus CHECK (RecordStatus IN ('A', 'I', '*'))
);
```

## Table Template (PostgreSQL)
```sql
CREATE TABLE {schema}.{table} (
    {table}_id SERIAL PRIMARY KEY,
    -- business columns...
    record_creation_user VARCHAR(50) NOT NULL,
    record_creation_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    record_edit_user VARCHAR(50),
    record_edit_date TIMESTAMPTZ,
    record_status CHAR(1) NOT NULL DEFAULT 'A',
    CONSTRAINT ck_{schema}_{table}_record_status CHECK (record_status IN ('A', 'I', '*'))
);
```

## Index Best Practices — INCLUDE Clause (SQL Server)
```sql
-- Include frequently queried columns to avoid key lookups
CREATE NONCLUSTERED INDEX IXN_{Schema}_{Table}_{Column}
ON {Schema}.{Table} ({FilterColumn})
INCLUDE ({Col1}, {Col2}, {Col3})
WHERE RecordStatus = 'A';
```
Guidelines: 3-5 INCLUDE columns most commonly accessed with that filter.

## CTEs vs Temp Tables
| Scenario | Use CTE | Use #Temp |
|----------|---------|-----------|
| Simple query, one-time use | | |
| Hierarchical data (tree) | Recursive | |
| Reuse result multiple times | | |
| Large source (>100k rows) | | with index |
| Pagination over filtered data | | |

## Transaction Rules
| Rule | Description |
|------|-------------|
| `SET XACT_ABORT ON` | Mandatory (SQL Server) — auto rollback on error |
| `XACT_STATE() <> 0` | Use instead of `@@TRANCOUNT` for rollback |
| Minimal scope | Open transaction as late as possible |
| No SELECTs inside | Read queries go outside the transaction |
| No nesting | Avoid nested transactions |
| Table access order | `Mstr → Cnfg → Core/{Custom} → Log` |

## Functions and Views (SQL Server)
| Type | Pattern | Example |
|------|---------|---------|
| Scalar Function | `{Schema}.{Name}` | `Cnfg.IsReservedWord` |
| Table Function | `{Schema}.Get{Name}` | `Cnfg.GetCatalogItems` |
| View | `{Schema}.VW_{Name}` | `Rpt.VW_EntitiesWithStatus` |

## File Organization
```
database/
├── {Schema}/
│   ├── Tables/
│   │   └── {Schema}.{Table}.sql
│   ├── StoredProcedures/
│   │   └── {Schema}.{Operation}{Entity}.sql
│   ├── Functions/
│   └── Views/
└── migrations/
    └── V{timestamp}__{description}.sql
```
