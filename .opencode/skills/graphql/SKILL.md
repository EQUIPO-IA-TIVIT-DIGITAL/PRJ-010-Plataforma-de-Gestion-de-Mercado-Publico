---
name: graphql
description: 'GraphQL API design: schema design, resolvers, N+1 problem, DataLoader,
  mutations, subscriptions, authentication, error handling, cursor-based pagination.
  Trigger: When designing or implementing GraphQL APIs in .NET, Node.js, or Python.'
metadata:
  phase:
  - construction
  layer:
  - backend
  enforcement: recommended
  depends_on:
  - backend-api
  - security
  - error-handling
  consumed_by:
  - agent-backend
  - agent-fullstack
  agent_roles:
  - design-agent
  - delivery-agent
  validation_profile: architecture-consistency
  mcp_usage: none
---

## Propósito

Diseñar e implementar APIs GraphQL correctas, eficientes y seguras: schema primero, resolvers sin N+1, paginación cursor-based, autenticación y errores consistentes en múltiples lenguajes.

## Objetivo

1. ¿Cómo se diseña el schema GraphQL (tipos, queries, mutations, subscriptions)?
2. ¿Cómo se evita el problema N+1 en resolvers?
3. ¿Cómo se implementa paginación cursor-based según la especificación Relay?
4. ¿Cómo se maneja autenticación y autorización en GraphQL?
5. ¿Cómo se estructuran errores y excepciones?
6. ¿Cómo se implementan suscripciones en tiempo real?

## Relación con otras skills

- `backend-api` comparte patrones de respuesta y estructura de proyecto.
- `security` provee autenticación, autorización y rate limiting aplicables a GraphQL.
- `error-handling` define el formato de errores que GraphQL debe adoptar en `errors` array.
- `performance` aporta DataLoader y caché para resolver N+1.

## Qué debe hacer el agente

1. Diseñar schema primero (SDL). El schema es el contrato, no la implementación.
2. Usar DataLoader para batching de acceso a datos y evitar N+1.
3. Implementar paginación cursor-based con `Connection`/`Edge`/`PageInfo`.
4. Centralizar autenticación en contexto global del request.
5. Estructurar errores con `extensions.code` categorizado para el frontend.
6. Usar input types dedicados para mutaciones, no argumentos sueltos.
7. Implementar subscriptions solo cuando haya eventos reales del servidor.
8. Deprecar campos con `@deprecated` en lugar de romper el schema.

## Alcance

Incluye: schema design, resolvers, DataLoader, paginación, auth, errores, subscriptions, testing.
No incluye: federación Apollo, GraphQL Mesh, schema stitching, BFF vs gateway decisions.

## Principios

- Schema-first: el schema SDL es el contrato, no el código del resolver.
- Un resolver no debe hacer más de una llamada a DB. Si lo hace, usa DataLoader.
- Las mutaciones devuelven el tipo mutado. Siempre. No scalars.
- Input types reutilizables, no argumentos planos.
- Los errores de negocio van en `errors[]`, no en `null` data.
- Las queries expuestas deben tener límite de profundidad o cantidad.

## Technical Design

### Schema SDL

```graphql
type Query {
  user(id: ID!): User
  users(first: Int!, after: String): UserConnection!
}
type Mutation {
  createUser(input: CreateUserInput!): CreateUserPayload!
}
type User {
  id: ID!
  name: String!
  posts(first: Int!, after: String): PostConnection!
}
```

### DataLoader (.NET HotChocolate)

```csharp
public class UserByIdDataLoader : BatchDataLoader<int, User>
{
    protected override async Task<IReadOnlyDictionary<int, User>> LoadBatchAsync(
        IReadOnlyList<int> keys, CancellationToken ct)
    {
        var users = await _db.QueryAsync<User>(
            "SELECT * FROM Users WHERE Id IN @Ids", new { Ids = keys });
        return users.ToDictionary(u => u.Id);
    }
}
```

### DataLoader (Node.js)

```typescript
const userLoader = new DataLoader(async (ids: readonly number[]) => {
  const users = await db.select().from('users').whereIn('id', ids);
  return ids.map(id => users.find(u => u.id === id) ?? null);
});
```

### DataLoader (Python + Strawberry)

```python
from strawberry.dataloader import DataLoader

async def load_users(ids: list[int]) -> list[User | None]:
    users = await db.fetch("SELECT * FROM users WHERE id = ANY($1)", ids)
    lookup = {u["id"]: u for u in users}
    return [lookup.get(i) for i in ids]

user_loader = DataLoader(load_fn=load_users)
```

### Paginación cursor-based (Relay)

```graphql
type UserConnection {
  edges: [UserEdge!]!
  pageInfo: PageInfo!
}
type UserEdge {
  node: User!
  cursor: String!
}
type PageInfo {
  hasNextPage: Boolean!
  hasPreviousPage: Boolean!
  startCursor: String
  endCursor: String
}
```

### Auth in resolvers

```csharp
[Authorize(Policy = "AdminOnly")]
public async Task<User> GetUser(Guid id, [Service] IUserService s)
    => await s.GetByIdAsync(id);
```

```python
@strawberry.type
class Query:
    @strawberry.field(permission_classes=[IsAuthenticated])
    async def user(self, id: int) -> User: ...
```

### Error format

```json
{
  "data": { "createUser": null },
  "errors": [{
    "message": "Email already exists",
    "extensions": { "code": "CONFLICT", "field": "email" }
  }]
}
```

## Preguntas guía

- ¿El schema refleja el dominio o la base de datos?
- ¿Cada resolver accede a DB una sola vez?
- ¿Las listas tienen paginación cursor-based o están desprotegidas?
- ¿Las mutaciones reciben `input` y devuelven `payload`?
- ¿La autenticación se valida en contexto de request?

## Salidas esperadas

- Schema SDL completo (types, queries, mutations, subscriptions).
- Resolvers con DataLoader registrados.
- Paginación cursor-based en todas las listas.
- Mutaciones con input/payload types.
- Middleware de autenticación y error handling.

## Criterios de calidad

- 0 queries N+1 en resolvers (verificable con logging de consultas SQL).
- Paginación obligatoria en toda lista con `first`/`after`.
- Errores con `extensions.code` categorizado.
- Input types reutilizados, no argumentos duplicados.

## Comportamiento esperado del agente

Cuando se detecte un resolver con múltiples queries SQL en un loop, el agente debe introducir DataLoader.
Cuando una lista no tenga paginación, debe agregar cursor-based pagination.
Cuando una mutación exponga argumentos planos, debe crear un `input` type.
Cuando un error de negocio devuelva `null` sin código, debe estructurarlo en `extensions`.

## Plantilla de respuesta

```
1. Schema SDL (types, queries, mutations, subscriptions).
2. DataLoader registration per entity.
3. Pagination on all list fields.
4. Auth middleware / resolver guard.
5. Error format with extensions.code.
6. Test cases for N+1 and auth.
```

## Ejemplos

### N+1 detection

```
Query: { users { posts { title } } }
Without DataLoader: 1 query for users + N queries for posts.
With DataLoader: 1 query for users + 1 batch query for posts.
```

### Subscription

```graphql
type Subscription {
  postCreated: Post!
}
```
```csharp
[Subscribe]
public Post PostCreated([EventMessage] Post post) => post;
```

## Checklist

- [ ] Schema diseñado SDL-first.
- [ ] DataLoader para cada entidad con acceso a DB en resolvers.
- [ ] Paginación cursor-based (Relay) en todas las listas.
- [ ] Input types para todas las mutaciones.
- [ ] Payload types con el objeto mutado.
- [ ] `@deprecated` en campos obsoletos.
- [ ] Auth check en contexto de request.
- [ ] Error format con `extensions.code`.
- [ ] Profundidad máxima de query configurada.
- [ ] Subscription implementada solo si hay eventos reales.
