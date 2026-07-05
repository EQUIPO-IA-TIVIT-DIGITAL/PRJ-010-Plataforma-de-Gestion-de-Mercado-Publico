---
name: backend-api
description: 'Backend API structure: modules, features, requests, responses, endpoints/controllers.
  Primary examples use .NET Minimal API; patterns apply to Java Spring, Python FastAPI,
  Node.js. Trigger: When creating API endpoints, requests, responses, or project structure.'
metadata:
  phase:
  - construction
  layer:
  - backend
  enforcement: mandatory
  depends_on:
  - database-sp
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
| Use typed response wrappers | ALWAYS | Type safety |
| Validate all inputs before reaching business logic | ALWAYS | Fast fail |
| Use `[AsParameters]` for query-string binding (.NET) | ALWAYS | Proper binding |
| Put sort defaults in Handler/Service, not Request | ALWAYS | Single source of truth |
| Use auth context for current user identity | ALWAYS | Never trust client-sent user ID |
| Endpoints/Controllers are thin — no business logic | ALWAYS | Separation of concerns |
| SP constants class: `{Module}StoredProcedures.cs` (.NET) | ALWAYS | Centralize SP names |

## Project Structure (.NET Minimal API)
```
Modules/{Module}/
├── {Module}Module.cs
├── {Module}StoredProcedures.cs
└── Features/{Entity}/
    ├── List{Entity}/  (Endpoint, Handler, Request, Response, Validator)
    ├── Get{Entity}/
    ├── Create{Entity}/
    ├── Update{Entity}/
    └── Delete{Entity}/
```

## Project Structure (Java Spring Boot)
```
{module}/
├── controller/{Entity}Controller.java
├── service/{Entity}Service.java
├── repository/{Entity}Repository.java
├── dto/request/ and dto/response/
└── model/{Entity}.java
```

## Project Structure (Python FastAPI)
```
{module}/
├── router.py
├── service.py
├── repository.py
├── schemas.py
└── models.py
```

## Endpoint Pattern (.NET)
```csharp
public static class Get{Entity}Endpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle)
            .Produces<ApiResponse<Get{Entity}Response>>(200)
            .WithSummary("Get {Entity}");
    }

    private static async Task<IResult> Handle(
        [FromServices] Get{Entity}Handler handler,
        [FromServices] HeaderToken headerToken,
        CancellationToken ct)
    {
        var currentUser = headerToken?.EmployeeId ?? throw new UnauthorizedAccessException();
        var result = await handler.HandleAsync(currentUser, ct);
        return Results.Ok(ApiResponse<Get{Entity}Response>.Ok(result));
    }
}
```

## HTTP Method → Status Code Mapping
| Method | Success Code | Pattern |
|--------|-------------|---------|
| GET (single) | 200 | `ApiResponse<T>.Ok(data)` |
| GET (list) | 200 | `ApiResponse.OkList(items, pagination)` |
| POST | 201 | `Results.Created(location, data)` |
| PUT | 200 | `ApiResponse<T>.Ok(data)` |
| DELETE | 200 | `ApiResponse<T>.Ok(data)` |

## Module Registration (.NET)
```csharp
public static class {Module}Module
{
    public static IServiceCollection Add{Module}Module(this IServiceCollection services)
    {
        services.AddScoped<Get{Entity}Handler>();
        services.AddValidatorsFromAssemblyContaining<Update{Entity}Validator>();
        return services;
    }
    public static void Map{Module}Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/{entity}").WithTags("{Entity}");
        Get{Entity}Endpoint.Map(group);
    }
}
```

## Validation (FluentValidation — .NET)
| Code | Validate in |
|------|-------------|
| VAL_001 (Required) | API |
| VAL_002 (Format) | API |
| VAL_003 (Duplicate) | DB/SP |
| VAL_007 (Out of range) | API |
| VAL_008 (Length exceeded) | API |

```csharp
RuleFor(x => x.Name)
    .NotEmpty().WithErrorCode("VAL_001").WithMessage("Name is required")
    .MaximumLength(500).WithErrorCode("VAL_008");
```

## Java Spring Boot Equivalent
```java
@RestController @RequestMapping("/api/v1/{entities}")
public class {Entity}Controller {
    @GetMapping("/{id}")
    public ResponseEntity<ApiResponse<{Entity}Dto>> get(@PathVariable Long id) {
        return ResponseEntity.ok(ApiResponse.ok(service.findById(id)));
    }
}
```

## Python FastAPI Equivalent
```python
@router.get("/{entity_id}", response_model=ApiResponse[EntityOut])
async def get_entity(entity_id: int, current_user: User = Depends(get_current_user)):
    return ApiResponse.ok(service.get_by_id(entity_id))
```
