---
name: authorization
description: 'Authorization patterns: permission models, role-based access control
  (RBAC),  resource-level access, and frontend permission rendering. Trigger: When
  implementing permissions, roles, or access control.'
metadata:
  phase:
  - construction
  layer:
  - backend
  - frontend
  enforcement: mandatory
  depends_on:
  - authentication
  - security
  consumed_by:
  - backend-api
  - react-hooks
  agent_roles:
  - design-agent
  - control-agent
  validation_profile: security-review
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Authorize server-side always | ALWAYS | Client-side is bypassable |
| Use centralized permission check | ALWAYS | No scattered `if (role == ...)` |
| Return 403 for insufficient permissions | ALWAYS | Consistent HTTP semantics |
| Never expose permission details in error messages | NEVER | Information disclosure |
| Frontend: hide UI elements based on permissions | ALWAYS | UX; not a security control |

## Authorization Models

| Model | Description | Best For |
|-------|-------------|----------|
| RBAC | Roles → Permissions | Simple role-based systems |
| ABAC | Attributes (context) → Decision | Complex, dynamic access rules |
| Permission-based | Fine-grained actions on resources | Flexible enterprise apps |
| Ownership-based | User owns the resource | User-generated content |
| Hierarchical | Org-level → Department → User | Org-chart access |

## RBAC Pattern (.NET)
```csharp
// Attribute-based
[Authorize(Roles = "Admin,Manager")]
public static async Task<IResult> Handle(...) { ... }

// Policy-based (recommended for complex rules)
builder.Services.AddAuthorization(opt => {
    opt.AddPolicy("CanManageUsers", p => p.RequireRole("Admin").RequireClaim("department", "HR"));
});

// In endpoint:
app.MapPost("/users", Handle).RequireAuthorization("CanManageUsers");
```

## RBAC Pattern (Java Spring)
```java
@PreAuthorize("hasRole('ADMIN') or hasPermission(#entityId, 'ENTITY', 'WRITE')")
public EntityDto updateEntity(Long entityId, UpdateRequest request) { ... }
```

## RBAC Pattern (Python FastAPI)
```python
def require_permission(permission: str):
    def dependency(current_user: User = Depends(get_current_user)):
        if permission not in current_user.permissions:
            raise HTTPException(status_code=403, detail="Insufficient permissions")
        return current_user
    return dependency

@router.post("/entities", dependencies=[Depends(require_permission("entities:write"))])
async def create_entity(...): ...
```

## Resource-Level Authorization
```csharp
// Check ownership or org-unit membership before returning/modifying
public async Task<EntityDto> GetEntity(int entityId, string currentUserId)
{
    var entity = await _repo.FindById(entityId);
    if (entity == null) throw new NotFoundException();
    
    if (!await _authz.CanAccess(currentUserId, entityId, "READ"))
        throw new ForbiddenException("AUTH_003");
    
    return entity;
}
```

## Hierarchical Access Pattern (SQL Server SP)
```sql
-- 1. Self-access check
-- 2. Admin/elevated role check
-- 3. Same org-unit check
-- 4. Hierarchy descendant check (CTE)
IF @VHasPermission = 0
BEGIN
    SELECT 'AUTH_001' AS ErrorCode, 'userId' AS Field, 'Unauthorized' AS Message;
    RETURN;
END
```

## Frontend Permission Rendering (React)
```typescript
// Hook to read user permissions from auth context
const { permissions, roles } = useCurrentUser();

// Conditional rendering
{permissions.includes('entities:write') && (
  <Button onClick={handleCreate}>Create</Button>
)}

// Angular equivalent
// *ngIf="hasPermission('entities:write')"

// Vue equivalent
// v-if="hasPermission('entities:write')"
```

## Permission Naming Convention
```
{resource}:{action}
Examples:
  entities:read
  entities:write
  entities:delete
  admin:users:manage
```

## Error Codes
| Code | HTTP | Meaning |
|------|------|---------|
| `AUTH_001` | 403 | Unauthorized (no permission) |
| `AUTH_002` | 401 | Token expired |
| `AUTH_003` | 403 | Insufficient permissions for resource |
