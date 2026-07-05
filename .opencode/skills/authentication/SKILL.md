---
name: authentication
description: 'Authentication patterns: token-based auth, session management, identity
  propagation. Covers JWT, OAuth2/OIDC, and custom token patterns for .NET, Java,
  Python, and React. Trigger: When implementing login, logout, tokens, session management,
  or auth flow.'
metadata:
  phase:
  - construction
  layer:
  - backend
  - frontend
  enforcement: mandatory
  depends_on:
  - security
  consumed_by:
  - authorization
  - backend-api
  - app-bootstrap
  - dotnet-gateway
  agent_roles:
  - design-agent
  - control-agent
  validation_profile: security-review
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Validate tokens at gateway/middleware, not in each endpoint | ALWAYS | Single validation point |
| Never store tokens in localStorage (prefer httpOnly cookies) | ALWAYS | XSS protection |
| Propagate user identity via internal header to microservices | ALWAYS | Consistent identity |
| Use short-lived access tokens + refresh token rotation | ALWAYS | Limit token exposure |
| Never log tokens, passwords, or auth credentials | NEVER | Security breach risk |
| Verify token expiration and signature server-side | ALWAYS | Cannot trust client |

## Authentication Patterns

| Pattern | Use Case |
|---------|----------|
| JWT (Bearer) | Stateless APIs, microservices |
| OAuth2 / OIDC | Third-party identity providers (Google, Azure AD, Cognito) |
| API Key | Server-to-server communication |
| Session Cookie (httpOnly) | Traditional web apps |
| Custom encrypted token | Internal service mesh |

## Standard Auth Flow (JWT)
```
Client → [POST /auth/login] → Auth Service → Validates credentials →
Returns { accessToken, refreshToken } → Client stores securely →
Client → [GET /resource, Authorization: Bearer {token}] → API validates token → Response
```

## Microservice Identity Propagation
```
Client → [auth headers] → API Gateway → Validates token → Extracts user claims →
Creates internal identity header → Forwards to internal services →
Internal services read identity from header (no re-validation)
```

## .NET JWT Setup
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt => {
        opt.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });
```

## Java Spring Boot JWT Setup
```java
@EnableWebSecurity
public class SecurityConfig {
    @Bean
    public SecurityFilterChain filterChain(HttpSecurity http) throws Exception {
        http.oauth2ResourceServer(oauth2 -> oauth2.jwt(Customizer.withDefaults()));
        return http.build();
    }
}
```

## Python FastAPI JWT
```python
async def get_current_user(token: str = Depends(oauth2_scheme)) -> User:
    try:
        payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
        user_id = payload.get("sub")
        return await get_user(user_id)
    except JWTError:
        raise HTTPException(status_code=401, detail="Could not validate credentials")
```

## Frontend Auth (React)
```typescript
// Store tokens in memory (or httpOnly cookie via server)
// Never localStorage for access tokens
const authStore = create((set) => ({
  user: null,
  setUser: (user) => set({ user }),
  logout: () => set({ user: null }),
}));

// Attach token to requests via interceptor/adapter
api.interceptors.request.use(config => {
  const token = authStore.getState().accessToken;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});
```

## Identity Header Pattern (Internal APIs)
```csharp
// Gateway creates: serialize user claims → base64/encrypted → HTTP header
// Internal API reads:
builder.Services.AddScoped<IdentityContext>(sp => {
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>();
    return IdentityContext.FromHeader(httpContext.Request.Headers["X-Identity"]);
});
```

## Auth Endpoints to Exclude from Validation
- `/health` — health checks
- `/swagger` or `/openapi` — API documentation
- `/auth/login` or `/auth/token` — login endpoints
- Public static assets

## OpenAPI Security Schemes
```yaml
# For custom header-based auth:
securitySchemes:
  code: { type: apiKey, in: header, name: code }
  header: { type: apiKey, in: header, name: header }
# For standard JWT:
securitySchemes:
  bearerAuth: { type: http, scheme: bearer, bearerFormat: JWT }
```
