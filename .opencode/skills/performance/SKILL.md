---
name: performance
description: 'Performance patterns: pagination, caching, query optimization, large
  data handling. Covers backend (.NET, Java, Python) and frontend (React, Angular,
  Vue). Trigger: When implementing pagination, caching, query optimization, or large
  data handling.'
metadata:
  phase:
  - construction
  layer:
  - database
  - backend
  enforcement: recommended
  depends_on:
  - database
  - backend-api
  consumed_by:
  - agent-backend
  - agent-fullstack
  agent_roles:
  - design-agent
  - control-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Always paginate list endpoints | ALWAYS | Prevent memory issues |
| Use `SELECT` only needed columns | ALWAYS | Reduce data transfer |
| Index foreign keys and search columns | ALWAYS | Query performance |
| Cache reference data, not transactional | ALWAYS | Stale data risk |
| Never return unbounded lists | NEVER | Memory / network overload |

## Pagination API Response
```json
{
  "success": true,
  "data": { "items": [...] },
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalRecords": 150,
    "totalPages": 8,
    "hasNext": true,
    "hasPrevious": false
  }
}
```

## Frontend Pagination (TanStack Query)
```typescript
// Keep previous data while loading next page
const query = useQuery({
  queryKey: ['entities', page, filters],
  queryFn: () => api.get('/entities', { params: { page, ...filters } }),
  placeholderData: keepPreviousData,
});
```

## Query Optimization (SQL Server)
```sql
-- EXISTS instead of COUNT for existence check
IF EXISTS (SELECT 1 FROM Core.Entity WHERE EntityId = @Id AND RecordStatus = 'A')

-- TOP 1 for single-row check
SELECT TOP 1 EntityId FROM Core.Entity WHERE Name = @Name

-- WITH(NOLOCK) for read-only reports
SELECT * FROM Core.Entity WITH(NOLOCK) WHERE ...

-- NEVER: SELECT *
-- NEVER: Unbounded query without WHERE
```

## Caching Strategy

| Cache | TTL | Use for |
|-------|-----|---------|
| Reference data | 1 hour | Status lists, categories |
| User permissions | 5 min | Role/permission data |
| Configuration | 10 min | Feature flags |
| Session data | 15 min | User preferences |
| **NO cache** | — | Transactional data |

## Caching Implementation
```csharp
// .NET IMemoryCache
var cached = _cache.GetOrCreate("reference-data", entry => {
    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
    return _repo.GetReferenceData();
});
```

```python
# Python (Redis via redis-py or aioredis)
@cache(expire=3600)  # 1 hour
async def get_reference_data(): ...
```

## Frontend Caching (TanStack Query)
```typescript
// Reference data: long stale time
const statusQuery = useQuery({
  queryKey: ['status-list'],
  queryFn: () => api.get('/catalogs/status'),
  staleTime: 1000 * 60 * 60,  // 1 hour
});

// Transactional: no stale
const entityQuery = useQuery({
  queryKey: ['entity', id],
  queryFn: () => api.get(`/entities/${id}`),
  staleTime: 1000 * 30,  // 30 seconds
});
```

## Large Data Handling
| Scenario | Technique |
|----------|-----------|
| Large exports | Stream / chunked response |
| Long lists in UI | Virtual scrolling (react-window, CDK virtual scroll) |
| Infinite lists | Cursor-based pagination |
| Heavy computations | Background jobs / queues |
| Large file uploads | Chunked upload + presigned URLs |

## Database Indexing
```sql
-- Index foreign keys used in JOINs
CREATE INDEX IXN_{Schema}_{Table}_{Column} ON {Schema}.{Table} ({Column})
INCLUDE ({FrequentlySelectedCol1}, {FrequentlySelectedCol2});

-- Partial index for filtered queries (SQL Server: filtered index)
WHERE RecordStatus = 'A'
```

## Response Compression
```csharp
// .NET — enable gzip/brotli
builder.Services.AddResponseCompression(opt => {
    opt.EnableForHttps = true;
    opt.Providers.Add<BrotliCompressionProvider>();
    opt.Providers.Add<GzipCompressionProvider>();
});
```
