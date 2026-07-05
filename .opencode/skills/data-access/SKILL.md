---
name: data-access
description: 'Data access handler patterns: calling stored procedures/queries, mapping
  results, error handling. Primary examples use Dapper (.NET); patterns apply to JdbcTemplate
  (Java), SQLAlchemy (Python). Trigger: When implementing data access handlers, calling
  stored procedures, or mapping results.'
metadata:
  phase:
  - construction
  layer:
  - backend
  enforcement: mandatory
  depends_on:
  - database-sp
  - backend-api
  consumed_by:
  - api-first-backend
  - agent-backend
  - agent-fullstack
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Call error helper after reading SP result | ALWAYS | Proper error handling |
| Use `QueryAsync` for simple lists | ALWAYS | Single ResultSet with COUNT(*) OVER() |
| Return paginated tuple `(Data, Pagination)` for lists | ALWAYS | Consistent pattern |
| Use cancellation tokens (.NET) | ALWAYS | Proper async handling |
| Set sort defaults in Handler/Service | ALWAYS | Single source of truth |
| Pass `currentUser` for audit trail | ALWAYS | Auditability |
| Use parameterized queries always | ALWAYS | SQL injection prevention |
| No interface for handlers (.NET pattern) | ALWAYS | Concrete class with constructor injection |

## Handler Pattern (.NET + Dapper)
```csharp
public class Get{Entity}Handler(IDbConnection db)
{
    public async Task<Get{Entity}Response> HandleAsync(string employeeId, CancellationToken ct = default)
    {
        var command = new CommandDefinition(
            {Entity}StoredProcedures.Get{Entity},
            new { ParamIEmployeeId = employeeId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        var result = await db.QuerySingleAsync<dynamic>(command);
        SpResultHelper.ThrowIfError(result);
        var dict = (IDictionary<string, object>)result;

        return new Get{Entity}Response
        {
            EntityId = dict.GetValue<int>("EntityId"),
            Name = dict.GetValue<string>("Name") ?? string.Empty,
            Email = dict.GetValue<string>("Email"),  // nullable
        };
    }
}
```

## Handler Types Quick Reference
| Type | Dapper Method | Returns |
|------|---------------|---------|
| List | `QueryAsync` | `(Response, PaginationResult)` |
| Get | `QuerySingleAsync` | `Response` |
| Create | `QuerySingleAsync` | `Response` |
| Update | `QuerySingleAsync` | `Response` |
| Delete | `QuerySingleAsync` | `Response` (ID confirmation) |

## SP Error Mapping
| SP Code | Exception | HTTP |
|---------|-----------|------|
| `VAL_*` | ValidationException | 400 |
| `{MOD}_001` | NotFoundException | 404 |
| `{MOD}_002` | ConflictException | 409 |
| `{MOD}_003+` | BusinessRuleException | 422 |
| `AUTH_*` | ForbiddenException | 403 |
| `SYS_*` | InternalException | 500 |

## Java Spring Boot Equivalent
```java
@Repository
public class {Entity}Repository {
    public {Entity}Dto findById(Long id) {
        return jdbcTemplate.queryForObject(
            "EXEC Schema.Get{Entity} @ParamIId = ?",
            new Object[]{id},
            (rs, rowNum) -> mapRow(rs)
        );
    }
}
```

## Python SQLAlchemy Equivalent
```python
async def get_entity(db: AsyncSession, entity_id: int) -> EntityModel:
    result = await db.execute(
        text("EXEC Schema.GetEntity @ParamIId = :id"),
        {"id": entity_id}
    )
    row = result.fetchone()
    if not row:
        raise NotFoundException("Entity not found")
    return EntityModel(**row._mapping)
```

## StoredProcedures Constants (.NET)
```csharp
public static class {Module}StoredProcedures
{
    private const string Schema = "{Schema}";
    public const string Get{Entity} = $"{Schema}.Get{Entity}";
    public const string Create{Entity} = $"{Schema}.Create{Entity}";
    public const string List{Entity} = $"{Schema}.List{Entity}";
    public const string Update{Entity} = $"{Schema}.Update{Entity}";
    public const string Delete{Entity} = $"{Schema}.Delete{Entity}";
}
```

## MasterTable Mapping (.NET)
```csharp
public static MasterTable? MapMasterTable(IDictionary<string, object> dict, string prefix)
{
    var id = dict.GetValue<int?>($"{prefix}.MasterTableId");
    if (id == null) return null;
    return new MasterTable {
        MasterTableId = id.Value,
        Name = dict.GetValue<string>($"{prefix}.Name") ?? string.Empty,
        Value = dict.GetValue<string>($"{prefix}.Value") ?? string.Empty
    };
}
```
