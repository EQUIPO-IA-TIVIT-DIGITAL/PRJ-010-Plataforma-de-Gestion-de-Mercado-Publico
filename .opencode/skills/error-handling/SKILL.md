---
name: error-handling
description: 'Error handling patterns across all layers (Database, Backend, Frontend).
  Trigger: When implementing error handling, exceptions, error responses, or error
  UI.'
metadata:
  phase:
  - construction
  layer:
  - backend
  enforcement: mandatory
  depends_on:
  - database-sp
  - data-access
  - shared-libs
  consumed_by:
  - agent-backend
  - agent-fullstack
  agent_roles:
  - design-agent
  - control-agent
  validation_profile: security-review
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Use typed exceptions, not generic `Exception` | ALWAYS | Automatic HTTP mapping |
| Return error codes, not just messages | ALWAYS | Frontend can handle programmatically |
| Log at the boundary, not everywhere | ALWAYS | Avoid duplicate logs |
| Never expose stack traces to clients | NEVER | Security risk |
| Never expose internal system details in error messages | NEVER | Information disclosure |

## Error Flow
```
DB (SELECT ErrorCode) → Handler (ThrowIfError) → Typed Exception → 
Exception Middleware → ApiResponse → Frontend (toast/form error)
```

## Layer-by-Layer Reference

### 1. Database — SELECT (not RAISERROR/RAISE EXCEPTION)
```sql
-- SQL Server
IF @Amount <= 0
BEGIN
    SELECT 'VAL_001' AS ErrorCode, 'Amount' AS Field, 'Amount must be greater than 0' AS Message;
    RETURN;
END
```

### 2. Backend Handler — No try/catch, use error helper
```csharp
// .NET example
var result = await _db.QuerySingleAsync<dynamic>("Schema.SP_CreateEntity", request);
SpResultHelper.ThrowIfError(result);  // throws typed exception if ErrorCode present
return new CreateEntityResponse { ... };
```

### 3. Exception Types → HTTP Status
| Exception | HTTP | When |
|-----------|------|------|
| `ValidationException` | 400 | Input invalid |
| `ForbiddenException` | 403 | Authorization failed |
| `NotFoundException` | 404 | Resource not found |
| `ConflictException` | 409 | Duplicate or state conflict |
| `BusinessRuleException` | 422 | Business rule violation |
| `InternalException` | 500 | Unhandled system error |

### 4. Frontend — Toast + Form errors
```typescript
// Logic hook pattern
const handleSubmit = async (data: FormData) => {
    try {
        await createMutation(data, {
            onSuccess: () => toast.success("Created successfully"),
        });
    } catch (err) {
        if (err.errors) {
            // Set form field errors from err.errors array
            err.errors.forEach(e => form.setError(e.field, { message: e.message }));
        } else {
            toast.error(err.message ?? "Unexpected error");
        }
    }
};
```

## Error Response Format
```json
{
  "success": false,
  "errors": [
    { "code": "VAL_001", "field": "Name", "message": "Name is required" }
  ]
}
```

## Java Spring Boot Equivalent
```java
@ControllerAdvice
public class GlobalExceptionHandler {
    @ExceptionHandler(ValidationException.class)
    public ResponseEntity<ApiResponse<?>> handleValidation(ValidationException ex) {
        return ResponseEntity.badRequest().body(ApiResponse.error(ex.getErrors()));
    }
}
```

## Python FastAPI Equivalent
```python
@app.exception_handler(ValidationException)
async def validation_exception_handler(request, exc):
    return JSONResponse(
        status_code=400,
        content={"success": False, "errors": exc.errors}
    )
```

## Security Considerations
- Log full error details server-side only
- Return only error code + user-friendly message to client
- Include a `Ref:` ID in SYS_ errors so clients can report it
- Never include SQL error messages in responses
