---
name: api-first-backend
description: 'Generate backend code from OpenAPI spec: Database objects, Handlers/Services,
  Endpoints/Controllers. DB-driven approach: data access first, then API layer. Trigger:
  When implementing backend from OpenAPI spec, generating code from endpoints.'
metadata:
  phase:
  - construction
  layer:
  - backend
  enforcement: mandatory
  depends_on:
  - api-first-spec
  - database-sp
  consumed_by:
  - agent-backend
  - agent-fullstack
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: skill-contract
---

## Workflow
OpenAPI Spec → Parse → DB objects (DB first) → Handler/Service → DTOs → Endpoint/Controller

## Method → Pattern Mapping

| HTTP Method | Action | Handler/Service | DB Object Prefix |
|-------------|--------|-----------------|------------------|
| `GET` (list) | List | `List{Entity}Handler` | `List{Entity}` |
| `GET` (single) | Get | `Get{Entity}Handler` | `Get{Entity}` |
| `POST` | Create | `Create{Entity}Handler` | `Create{Entity}` |
| `PUT` | Update | `Update{Entity}Handler` | `Update{Entity}` |
| `DELETE` | Delete | `Delete{Entity}Handler` | `Delete{Entity}` |
| `POST` (operation) | {Verb} | `{Verb}{Entity}Handler` | `{Verb}{Entity}` |
| `POST` (remove) | Remove | `Remove{SubEntity}Handler` | `Remove{SubEntity}` |
| `PUT` (reorder) | Reorder | `Reorder{SubEntities}Handler` | `Reorder{SubEntities}` |

## Type Mapping
| OpenAPI Type | .NET Type | Java Type | Python Type | SQL Type |
|--------------|-----------|-----------|-------------|----------|
| `integer` | `int` | `Integer` | `int` | `INT` |
| `integer` (int64) | `long` | `Long` | `int` | `BIGINT` |
| `number` | `decimal` | `BigDecimal` | `Decimal` | `DECIMAL(18,2)` |
| `string` | `string` | `String` | `str` | `NVARCHAR` |
| `string` (date) | `DateOnly` | `LocalDate` | `date` | `DATE` |
| `string` (date-time) | `DateTime` | `LocalDateTime` | `datetime` | `DATETIME` |
| `boolean` | `bool` | `Boolean` | `bool` | `BIT/BOOLEAN` |

## Error Pattern (SP/Query → Handler)
DB operations return business errors via result rows (not exceptions):
```sql
-- SQL Server / T-SQL example
IF @Amount <= 0
BEGIN
    SELECT 'VAL_001' AS ErrorCode, 'Amount' AS Field, 'Amount must be greater than 0' AS Message;
    RETURN;
END
```
Handler/Service maps error result to typed domain exception.

## Handler Pattern (.NET example)
```csharp
public class CreateEntityHandler(IDbConnection db)
{
    public async Task<CreateEntityResponse> Handle(CreateEntityRequest request, string currentUser)
    {
        var result = await db.QueryFirstOrDefaultAsync<CreateEntitySpResult>(
            EntityStoredProcedures.CreateEntity,
            new { ParamIName = request.Name, ParamICreationUser = currentUser },
            commandType: CommandType.StoredProcedure);
        SpResultHelper.ThrowIfError(result);
        return new CreateEntityResponse { EntityId = result.EntityId, Name = result.Name };
    }
}
```

## Endpoint Key Conventions
| Convention | Pattern |
|-----------|---------|
| Validation | Validate POST/PUT before reaching handler |
| Handler injection | Dependency Injection |
| Current user | From auth context / header token |
| Success response | `ApiResponse<T>.Ok(data)` |
| Created response | 201 with Location header |

## Common Operations
| Operation | Verb | Request Body |
|-----------|------|-------------|
| Submit | submit | `{}` or optional |
| Cancel | cancel | `{ reason? }` |
| Approve | approve | `{ notes? }` |
| Reject | reject | `{ reason }` |

## Checklist
- [ ] DB objects created
- [ ] DB errors handled properly (error result → typed exception)
- [ ] Handler/Service calls DB object
- [ ] Input validation for required/format/length
- [ ] Endpoint wired to handler
- [ ] Module/Controller registered
