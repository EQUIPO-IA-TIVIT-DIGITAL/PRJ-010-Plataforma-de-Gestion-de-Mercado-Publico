# Scaffolding Generator

Generates backend, frontend, database, and test scaffolding from an `api-first-spec` markdown document.

## Usage

```bash
# Basic usage
python3 generate.py ../docs/01-spec.md --output ./output

# With namespace and schema overrides
python3 generate.py spec.md --output ./my-module --namespace "Tivit.MyModule" --schema "app"
```

## What it generates

```
output/
├── backend/
│   ├── [Entity]Module.cs        # Minimal API module registration
│   ├── [Entity]Endpoints.cs     # REST endpoints (List/Get/Create/Update/Delete)
│   ├── [Entity]Handler.cs       # Dapper handlers for SPs
│   ├── [Entity]Request.cs       # Request DTOs
│   └── [Entity]Response.cs      # Response DTOs
├── frontend/
│   ├── types.ts                 # TypeScript interfaces
│   ├── use[Entity]List.ts       # TanStack Query hooks
│   ├── use[Entity]Mutation.ts   # Mutation hooks (create/update/delete)
│   ├── [Entity]List.tsx         # Ant Design table component
│   ├── [Entity]Form.tsx         # Create/Edit form
│   ├── [Entity]Page.tsx         # Full page with modal integration
│   └── index.ts                 # Barrel exports
├── database/
│   ├── 001_create_[entity].sql   # Table creation
│   └── 002_sp_[entity]_crud.sql  # CRUD stored procedures
└── tests/
    └── [module].spec.ts          # Playwright E2E tests
```

## Spec document format

The generator expects a markdown document with:

- A `# Module: Name` title
- An `## Entity / ERD` section with pipe-table entity definitions
- An `## Endpoints` section with method/path/description table
- An optional `## DTOs / Types` section with request/response definitions

See `example-spec.md` for a complete example.

## Placeholders

Templates use Python `string.Template` format:

| Placeholder | Description |
|-------------|-------------|
| `$MODULE` | PascalCase module name |
| `$ENTITY` | PascalCase entity name |
| `$entities` | camelCase plural entity name |
| `$SCHEMA` | Database schema name |
| `$NAMESPACE` | .NET namespace |

## Templates

Templates are in `templates/` and use `$PLACEHOLDER` syntax. Available templates:

- `endpoint.cs.j2` - Minimal API endpoints
- `handler.cs.j2` - Dapper data handlers
- `types.ts.j2` - TypeScript interfaces
- `hook.ts.j2` - TanStack Query hooks
- `component.tsx.j2` - Ant Design table component
- `sql_create.sql.j2` - CREATE TABLE script
- `sql_sp.sql.j2` - CRUD stored procedures
- `test.spec.ts.j2` - Playwright E2E test
