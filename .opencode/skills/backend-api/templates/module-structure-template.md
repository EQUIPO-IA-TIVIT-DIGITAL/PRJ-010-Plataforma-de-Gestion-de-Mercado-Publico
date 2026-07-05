# Module Structure: {ModuleName}

## Folder Layout

```
src/{Layer}/
└── {ModuleName}/
    ├── {ModuleName}Module.cs          # DI registration + endpoint mapping
    ├── {ModuleName}StoredProcedures.cs # SP name constants
    ├── Features/
    │   ├── List{Entity}/
    │   │   ├── List{Entity}Endpoint.cs   # Route definition
    │   │   ├── List{Entity}Handler.cs    # Data access
    │   │   ├── List{Entity}Request.cs    # Query params
    │   │   ├── List{Entity}Response.cs   # Result DTO
    │   │   └── List{Entity}Validator.cs  # FluentValidation
    │   ├── Get{Entity}/
    │   │   ├── Get{Entity}Endpoint.cs
    │   │   ├── Get{Entity}Handler.cs
    │   │   ├── Get{Entity}Request.cs
    │   │   └── Get{Entity}Response.cs
    │   ├── Create{Entity}/
    │   │   ├── Create{Entity}Endpoint.cs
    │   │   ├── Create{Entity}Handler.cs
    │   │   ├── Create{Entity}Request.cs
    │   │   ├── Create{Entity}Response.cs
    │   │   └── Create{Entity}Validator.cs
    │   ├── Update{Entity}/
    │   │   ├── Update{Entity}Endpoint.cs
    │   │   ├── Update{Entity}Handler.cs
    │   │   ├── Update{Entity}Request.cs
    │   │   ├── Update{Entity}Response.cs
    │   │   └── Update{Entity}Validator.cs
    │   └── Delete{Entity}/
    │       ├── Delete{Entity}Endpoint.cs
    │       ├── Delete{Entity}Handler.cs
    │       ├── Delete{Entity}Request.cs
    │       └── Delete{Entity}Response.cs
    └── Shared/
        └── ApiResponse.cs             # Response wrapper
```

## File Responsibilities

| File | Responsibility |
|------|----------------|
| `{Module}Module.cs` | Registers handlers in DI, maps endpoint group |
| `{Module}StoredProcedures.cs` | Centralized SP name constants |
| `*Endpoint.cs` | Route + HTTP verb + response types (no logic) |
| `*Handler.cs` | Calls SP via Dapper, maps results |
| `*Request.cs` | Input DTO (record/class) |
| `*Response.cs` | Output DTO (record/class) |
| `*Validator.cs` | FluentValidation rules |

## Example Module Registration

```csharp
public static class {ModuleName}Module
{
    public static IServiceCollection Add{ModuleName}Module(this IServiceCollection services)
    {
        services.AddScoped<List{Entity}Handler>();
        services.AddScoped<Get{Entity}Handler>();
        services.AddScoped<Create{Entity}Handler>();
        services.AddScoped<Update{Entity}Handler>();
        services.AddScoped<Delete{Entity}Handler>();

        services.AddValidatorsFromAssemblyContaining<Create{Entity}Validator>();

        return services;
    }

    public static void Map{ModuleName}Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/{entities}")
            .WithTags("{Entity}")
            .RequireAuthorization();

        List{Entity}Endpoint.Map(group);
        Get{Entity}Endpoint.Map(group);
        Create{Entity}Endpoint.Map(group);
        Update{Entity}Endpoint.Map(group);
        Delete{Entity}Endpoint.Map(group);
    }
}
```
