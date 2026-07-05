# Entity-Relationship Diagram: {ModuleName}

## Mermaid ERD

```mermaid
erDiagram
    {Entity1} ||--o{ {Entity2} : "has"
    {Entity1} ||--o{ {Entity3} : "has"
    {Entity2} }o--|| {Catalog1} : "references"
    {Entity1} {
        int {Entity1}Id PK
        string Name
        string Code UK
        string Status
        int CreatedBy
        datetime CreatedDate
        int? UpdatedBy
        datetime? UpdatedDate
    }
    {Entity2} {
        int {Entity2}Id PK
        int {Entity1}Id FK
        string Description
        int {Catalog1}Id FK
        string RecordStatus
        int CreatedBy
        datetime CreatedDate
    }
    {Entity3} {
        int {Entity3}Id PK
        int {Entity1}Id FK
        decimal Amount
        date EffectiveDate
    }
    {Catalog1} {
        int {Catalog1}Id PK
        string Name
        string Value UK
        int SortOrder
    }
```

## Entity Descriptions

| Entity | Type | Description | Cardinality | Audit |
|--------|------|-------------|-------------|-------|
| `{Entity1}` | Main | {description} | Parent of {Entity2} | Yes |
| `{Entity2}` | Detail | {description} | Child of {Entity1} | Yes |
| `{Entity3}` | Detail | {description} | Child of {Entity1} | Yes |
| `{Catalog1}` | Catalog | {description} | Referenced by {Entity2} | No |

## Column Details

### {Entity1}

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| `{Entity1}Id` | `int` | No | `IDENTITY(1,1)` | Primary key |
| `Name` | `nvarchar(500)` | No | — | Entity name |
| `Code` | `nvarchar(50)` | No | — | Unique code |
| `Status` | `nvarchar(20)` | No | `'DRAFT'` | Current status |
| `CreatedBy` | `int` | No | — | User who created |
| `CreatedDate` | `datetime2` | No | `GETUTCDATE()` | Creation timestamp |
| `UpdatedBy` | `int` | Yes | — | Last modifier |
| `UpdatedDate` | `datetime2` | Yes | — | Last modification |

### {Entity2}

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| `{Entity2}Id` | `int` | No | `IDENTITY(1,1)` | Primary key |
| `{Entity1}Id` | `int` | No | — | FK to {Entity1} |
| `Description` | `nvarchar(1000)` | No | — | Detail description |
| `{Catalog1}Id` | `int` | No | — | FK to {Catalog1} |
| `RecordStatus` | `char(1)` | No | `'A'` | A=Active, I=Inactive |
| `CreatedBy` | `int` | No | — | User who created |
| `CreatedDate` | `datetime2` | No | `GETUTCDATE()` | Creation timestamp |

### {Catalog1}

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| `{Catalog1}Id` | `int` | No | `IDENTITY(1,1)` | Primary key |
| `Name` | `nvarchar(200)` | No | — | Display name |
| `Value` | `nvarchar(50)` | No | — | Unique code value |
| `SortOrder` | `int` | No | `0` | Display order |

## Indexes

| Table | Index | Columns | Unique | Description |
|-------|-------|---------|--------|-------------|
| `{Entity1}` | `IX_{Entity1}_Code` | `Code` | Yes | Unique code lookup |
| `{Entity1}` | `IX_{Entity1}_Status` | `Status` | No | Status filtering |
| `{Entity2}` | `IX_{Entity2}_{Entity1}Id` | `{Entity1}Id` | No | Parent lookup |
