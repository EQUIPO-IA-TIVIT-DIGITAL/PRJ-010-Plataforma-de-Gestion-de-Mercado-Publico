---
name: api-integration
description: 'DB-to-API integration patterns: error mapping, pagination, validation,
  response structure. Trigger: When connecting stored procedures/queries to APIs,
  handling DB errors, or implementing pagination.'
metadata:
  phase:
  - construction
  layer:
  - backend
  enforcement: mandatory
  depends_on:
  - data-access
  - backend-api
  - database-sp
  consumed_by:
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
| Map DB errors to typed domain exceptions | ALWAYS | Consistent error handling |
| Use typed response wrappers (`ApiResponse<T>`) | ALWAYS | Never use untyped/generic |
| Validate format/required at API layer | ALWAYS | Fast fail before DB |
| Validate duplicates/FK/business rules at DB layer | ALWAYS | Data integrity |
| Cap pageSize at a maximum (e.g., 50 or 100) | ALWAYS | Prevent memory issues |

## ApiResponse Data Structure
| Endpoint Type | Data Structure |
|---------------|----------------|
| GET detail | `{ item: {...} }` |
| GET list | `{ items: [...] }` |
| POST create | `{ item: {...} }` |
| PUT update | `{ item: {...} }` |
| DELETE | `{ {entity}Id: int }` |

## Pagination Request Interface (.NET)
```csharp
public interface IPagedRequest
{
    int Page { get; set; }
    int PageSize { get; set; }
    string? Search { get; set; }
    string? SortBy { get; set; }
    string? SortOrder { get; set; }  // default DESC
}
```

## Pagination Defaults Behavior
| Parameter | Validation | Normalization |
|-----------|------------|---------------|
| `page` | Required (400 if <= 0) | — |
| `pageSize` | Required (400 if <= 0) | Cap at maxPageSize |
| `sortOrder` | Optional | Default "DESC", uppercase |

## Validation Error Codes
| Code | Description | Validate in |
|------|-------------|-------------|
| `VAL_001` | Required field | API |
| `VAL_002` | Invalid format | API |
| `VAL_003` | Duplicate value | DB |
| `VAL_004` | FK not exists | DB |
| `VAL_006` | Invalid JSON syntax | Middleware |
| `VAL_007` | Out of range | API |
| `VAL_008` | Length exceeded | API |

## FluentValidation Example (.NET)
```csharp
public class Update{Entity}Validator : AbstractValidator<Update{Entity}Request>
{
    public Update{Entity}Validator()
    {
        RuleFor(x => x.ContactPhone)
            .NotEmpty().WithErrorCode("VAL_001").WithMessage("Contact phone is required")
            .Matches(@"^\d{9,15}$").WithErrorCode("VAL_002").WithMessage("Phone must be 9-15 digits");
    }
}
```

## Java Bean Validation Equivalent
```java
public class Update{Entity}Request {
    @NotBlank(message = "VAL_001: Contact phone is required")
    @Pattern(regexp = "^\\d{9,15}$", message = "VAL_002: Phone must be 9-15 digits")
    private String contactPhone;
}
```

## Python Pydantic Equivalent
```python
class UpdateEntityRequest(BaseModel):
    contact_phone: str = Field(..., min_length=9, max_length=15, pattern=r'^\d+$')
```

## JSON Response Examples
```json
// List: { "success": true, "data": { "items": [...] }, "pagination": { "page":1, "totalRecords":25 } }
// Detail: { "success": true, "data": { "item": {...} }, "message": "Item retrieved" }
// Error: { "success": false, "errors": [{ "code": "VAL_001", "message": "...", "field": "Name" }] }
```

## Nested Validator (.NET)
```csharp
RuleForEach(x => x.Providers)
    .SetValidator(new Create{Entity}ProviderItemValidator())
    .When(x => x.Providers != null && x.Providers.Count > 0);
```
