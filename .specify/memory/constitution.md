# MPM Constitution
<!-- Mercado Público Management — Sistema de gestión y análisis de licitaciones públicas chilenas -->

## Core Principles

### I. Modular Monolith (NON-NEGOTIABLE)
Cada dominio de negocio es una class library independiente de .NET 8 registrada mediante un método de extensión `AddXxxModule()` en `Program.cs`. Los módulos no se referencian entre sí directamente — toda comunicación cross-module pasa por `MPM.Shared` o por interfaces inyectadas. La estructura interna de cada módulo es fija: `Controllers/`, `Services/`, `Data/`, `Models/`, `ModuleRegistration.cs`. No se crean módulos sin una responsabilidad de dominio clara.

### II. Stored Procedures First (NON-NEGOTIABLE)
**No se usa ORM (ningún EF Core, ni Linq2DB, ni similar).** Todo acceso a la base de datos ocurre a través de stored procedures PostgreSQL llamados via Dapper. Los procedimientos siguen la convención de nombres `usp_<Entidad>_<Verbo>` (e.g. `usp_Licitaciones_Listar`, `usp_SyncLog_Iniciar`). `Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true` está activo globalmente, por lo que las columnas `snake_case` mapean automáticamente a propiedades `PascalCase`.

### III. Migraciones como Scripts Embebidos
Las migraciones de base de datos son archivos `.sql` en `src/MPM.Api/Database/Scripts/`, embebidos como recursos y ejecutados por `DatabaseInitializer` al arrancar la API. La convención de nombres es `VXXX__Descripcion.sql` (e.g. `V070__Add_something.sql`). Los scripts se aplican en orden alfabético. **No se agregan columnas ni tablas fuera de este mecanismo.**

### IV. Multi-Tenancy por Middleware
`TenantMiddleware` extrae `user_id`, `tenant_id`, `username`, `roles` y `tenant_name` del JWT y los expone en `HttpContext.Items["TenantContext"]` como `TenantContext`. Todos los handlers de base de datos reciben el tenant context como parámetro; nunca se obtiene el tenant desde otro lugar. Los controladores no acceden directamente al `HttpContext` para leer claims — usan el `TenantContext` inyectado.

### V. Abstracción de Storage
El almacenamiento de archivos se accede exclusivamente a través de `IStorageService`. Dos implementaciones concretas: `LocalStorageService` (escribe en `/app/uploads`, default) y `GcsStorageService` (Google Cloud Storage). El proveedor se configura en `Storage:Provider` (`local` o `gcs`). **Ningún código de negocio o módulo referencia directamente el sistema de archivos ni el SDK de GCS.**

### VI. Real-Time via SignalR + Redis Backplane
La comunicación en tiempo real usa SignalR con backplane de Redis (`AddStackExchangeRedis`). El hub de mensajería está en `/hubs/mensajeria`. El token JWT para SignalR se pasa via query string `?access_token=` (manejado en `JwtBearerEvents.OnMessageReceived`). Los módulos que requieren notificaciones en tiempo real se conectan al hub, no implementan sus propios mecanismos de push.

### VII. Testing por Capas
- **Unit tests**: xUnit + Moq + FluentAssertions. Un proyecto de test por módulo (`tests/MPM.Modules.Xxx.Tests`), mirroring la estructura de `src/`.
- **Integration tests**: `tests/MPM.Tests` usa `Microsoft.AspNetCore.Mvc.Testing` con `WebApplicationFactory`. Cubre contratos HTTP y flujos cross-módulo.
- **E2E**: Playwright en `src/mpm-web/e2e/`. Se ejecuta con `npm run test:e2e`.
- Todo código nuevo debe tener cobertura de unit tests. Los cambios en contratos HTTP requieren test de integración.

## Stack Tecnológico

### Backend
- **Runtime**: .NET 8, C# con `Nullable enable` e `ImplicitUsings enable`
- **Base de datos**: PostgreSQL 15+ vía `Npgsql` 8.x + `Dapper` 2.x
- **Cache / Pub-Sub**: Redis via `StackExchange.Redis`
- **Real-time**: ASP.NET Core SignalR con backplane Redis
- **Auth**: JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **AI**: Google Gemini API (análisis de PDFs de evaluación)
- **Storage**: Google Cloud Storage (`Google.Cloud.Storage.V1`) o sistema de archivos local
- **Docs**: Swagger/OpenAPI via `Swashbuckle.AspNetCore`
- **Infraestructura**: Docker Compose (API → :5001, Web → :8181, DB → :5433)

### Frontend
- **Framework**: React 18 + TypeScript 5, bundler Vite 5
- **UI**: Ant Design 5 (`antd`) + `@ant-design/icons`
- **Data fetching**: TanStack Query v5 (`@tanstack/react-query`)
- **Routing**: React Router 6
- **Real-time**: `@microsoft/signalr`
- **Fechas**: `dayjs`
- **Export**: `html2canvas` + `jspdf`
- **Markdown**: `react-markdown`
- **Proxy dev**: Vite proxia `/api` y `/hubs` → `http://localhost:5000`

## Convenciones de Desarrollo

### Nombrado
- Stored procedures: `usp_<Entidad>_<Verbo>` (PascalCase, singular para entidad)
- Scripts de migración: `VXXX__Descripcion_breve.sql` (número de 3 dígitos con ceros)
- Módulos backend: `MPM.Modules.<Nombre>` (un concepto de dominio por módulo)
- Hooks de React: `use<Entidad>` en `src/hooks/` (e.g. `useLicitaciones`, `useAnalisis`)
- Constantes de stored procedure names deben vivir en `Data/` del módulo correspondiente

### Límites de módulos
- `MPM.Shared`: modelos compartidos entre módulos (`TenantContext`, `IStorageService`); no contiene lógica de negocio
- `MPM.Core`: infraestructura transversal (`DbConnectionFactory`, `ErrorHandlingMiddleware`, `TenantMiddleware`)
- Los módulos solo pueden referenciar `MPM.Shared` y `MPM.Core`; **nunca a otro módulo**
- `MPM.Api` es el único proyecto que referencia todos los módulos (punto de composición)

### Variables de entorno requeridas
`DB_USER`, `DB_PASSWORD`, `DB_NAME`, `REDIS_PASSWORD`, `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`, `MP_TICKET` (API Mercado Público), `GEMINI_API_KEY`, `Storage__Provider`, `Storage__Bucket`

## Governance

Esta constitución tiene precedencia sobre cualquier otra convención o preferencia individual. Toda nueva funcionalidad debe validar que no viola los principios I–VII antes de ser integrada. Los cambios a la arquitectura de módulos, al mecanismo de migraciones o al patrón de acceso a datos requieren actualización de este documento y registro en `CHANGELOG.md`.

**Version**: 1.0.0 | **Ratified**: 2026-06-23 | **Last Amended**: 2026-06-23
