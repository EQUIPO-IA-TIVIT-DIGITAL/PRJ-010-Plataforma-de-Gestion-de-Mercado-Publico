---
name: security
description: 'Security patterns for applications: input validation, authorization,
  CORS, SQL injection prevention. Covers OWASP Top 10 controls for .NET, Java, Python,
  and React/Angular/Vue. Trigger: When implementing validation, authorization, CORS,
  or security headers.'
metadata:
  phase:
  - construction
  layer:
  - backend
  enforcement: mandatory
  depends_on: []
  consumed_by:
  - authentication
  - authorization
  - database-security
  - backend-api
  agent_roles:
  - control-agent
  - design-agent
  validation_profile: security-review
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Use parameterized queries always | ALWAYS | SQL injection prevention |
| Validate on backend, not just frontend | ALWAYS | Frontend can be bypassed |
| Use centralized authorization checks | ALWAYS | No scattered permission logic |
| Sanitize user-generated content on display | ALWAYS | XSS prevention |
| Never log sensitive data (passwords, tokens, PII) | NEVER | Data breach risk |
| Never expose stack traces in API responses | NEVER | Information disclosure |
| Use HTTPS everywhere | ALWAYS | Transport security |
| Set security headers (CSP, HSTS, X-Frame-Options) | ALWAYS | Defense in depth |

## SQL Injection Prevention
Parameterized queries prevent SQL injection:
```csharp
// .NET Dapper — parameterized
var user = await db.QuerySingleAsync<User>("SELECT * FROM Users WHERE Id = @Id", new { Id = id });

// NEVER string concatenation
var user = await db.QuerySingleAsync<User>($"SELECT * FROM Users WHERE Id = {id}");
```

```python
# Python SQLAlchemy — parameterized
result = await db.execute(text("SELECT * FROM users WHERE id = :id"), {"id": user_id})

# NEVER f-string in SQL
result = await db.execute(text(f"SELECT * FROM users WHERE id = {user_id}"))
```

## Input Validation

| Layer | Tool | Error Format |
|-------|------|--------------|
| Backend (.NET) | FluentValidation | `{ code: "VAL_001", field: "Name", message: "..." }` |
| Backend (Java) | Bean Validation (`@NotNull`, `@Size`) | Spring error format |
| Backend (Python) | Pydantic | Pydantic validation error |
| Frontend | Form library rules | Inline form field error |

## XSS Prevention
- React: escapes by default. `dangerouslySetInnerHTML` only with `DOMPurify.sanitize()`
- Angular: escapes by default. `[innerHTML]` binding sanitizes automatically
- Vue: `v-html` directive should be avoided; use `DOMPurify` when needed

## CORS Configuration (.NET)
```csharp
builder.Services.AddCors(opt => opt.AddPolicy("AllowFrontend", policy => {
    policy.WithOrigins("https://app.example.com")
          .AllowAnyHeader()
          .AllowAnyMethod();
}));
app.UseCors("AllowFrontend");
```

## Security Headers
```
Content-Security-Policy: default-src 'self'
X-Frame-Options: DENY
X-Content-Type-Options: nosniff
Strict-Transport-Security: max-age=31536000; includeSubDomains
```

## Source Code Repository Security

| Control | GitHub | Azure DevOps | Bitbucket |
|---------|--------|--------------|-----------|
| Branch restrictions | Branch protection rules | Branch policies | Branch permissions |
| Require PR review | Required reviewers | Approval policies | Required approvals |
| Auth method | PAT / SSH / GitHub App | PAT / SSH / Azure AD | PAT / SSH / App passwords |

**CRITICAL (AWS CodeCommit)**: NO "branch protection" feature exists — use **IAM policies** + **Approval Rule Templates** instead.

## Secrets Management
| Environment | Tool |
|-------------|------|
| Local development | `.env.local` (gitignored) |
| CI/CD | GitHub Secrets / Azure Key Vault |
| Production | AWS Secrets Manager / Azure Key Vault / HashiCorp Vault |

**NEVER** store secrets in source code, config files committed to git, or logs.

## Rate Limiting
Apply rate limiting at gateway or API level to prevent brute force and DoS:
```csharp
// .NET — Fixed Window Rate Limiter
builder.Services.AddRateLimiter(opt => opt.AddFixedWindowLimiter("api", o => {
    o.PermitLimit = 100;
    o.Window = TimeSpan.FromMinutes(1);
}));
```
