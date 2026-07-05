---
name: swagger
description: 'OpenAPI/Swagger documentation generation and maintenance. Trigger: When
  creating API docs, updating Swagger, generating OpenAPI specs.'
metadata:
  phase:
  - construction
  - operations
  layer:
  - backend
  enforcement: recommended
  depends_on:
  - backend-api
  consumed_by:
  - api-first-spec
  - api-catalog
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: documentation
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Keep Swagger always updated | ALWAYS | Must reflect real API state |
| Include request/response examples | ALWAYS | Easier testing and understanding |
| Document all error codes | ALWAYS | Clients need to handle them |
| Use OpenAPI 3.0 format | ALWAYS | Industry standard |

## File Location
Each backend module/service should expose its OpenAPI spec at `/swagger` or `/openapi`.

## Endpoint Documentation (.NET Minimal API example)
```csharp
app.MapPost("/", ...)
    .WithName("CreateContract")
    .WithTags("Contracts")
    .WithDescription("Creates a new contract")
    .Produces<ApiResponse<ContractDto>>(200)
    .Produces<ApiResponse<object>>(400)
    .Produces<ApiResponse<object>>(409)
    .Produces<ApiResponse<object>>(422);
```

## Swagger UI Configuration (.NET)
```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "...", Version = "v1", Description = "..." });
    c.IncludeXmlComments(xmlPath);
});
app.UseSwagger(); app.UseSwaggerUI();
```

## Authentication Schemes
Use `apiKey` schemes for token-based auth (not BearerAuth if your auth uses custom headers):
```yaml
securitySchemes:
  code: { type: apiKey, in: header, name: code }
  header: { type: apiKey, in: header, name: header }
```

## Checklist
- [ ] All endpoints have name, tags, description
- [ ] All endpoints document each response status code
- [ ] Request DTOs have comments/examples
- [ ] Response DTOs have comments
- [ ] Error codes documented
- [ ] Swagger UI accessible in dev/staging
- [ ] XML documentation enabled (for .NET)
