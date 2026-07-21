# MPM — Mercado Público Management

> Plataforma de gestión e inteligencia de licitaciones públicas chilenas. Sincroniza, analiza y da seguimiento a licitaciones de [mercadopublico.cl](https://www.mercadopublico.cl), con análisis asistido por IA sobre documentos de evaluación y alertas automáticas de nuevas oportunidades.
>
> **v1.0.0 en producción** (GCP Cloud Run).

> **Nota**: la raíz de este repositorio también contiene un framework agéntico interno de TIVIT para OpenCode (`AGENTS.md`, `.opencode/`, `opencode.json`, `README.opencode.md`), no relacionado con la aplicación MPM. El código de MPM vive en `src/`, `tests/` y `tools/`, descritos abajo.

## Problema que resuelve

El seguimiento de licitaciones en Mercado Público es hoy un proceso manual: revisar el portal a diario, descargar y leer actas de evaluación en PDF para entender por qué se ganó o perdió una oferta, y monitorear a mano lo que hacen los competidores. MPM automatiza ese ciclo completo — sincroniza licitaciones desde la API oficial, extrae y analiza documentos con IA, y notifica proactivamente sobre oportunidades relevantes y actividad de la competencia.

## Funcionalidades

### Licitaciones
- Sincronización diaria automática desde la API oficial de Mercado Público
- Búsqueda por palabra clave y búsqueda semántica en lenguaje natural (Gemini)
- Filtros por estado, tipo, organismo y fecha
- Seguimiento de licitaciones puntuales con detección automática de aclaraciones

### Análisis con IA
- Workspace de análisis por licitación con carga de documentos (actas de evaluación en PDF)
- Extracción automática de criterios, puntajes, competidores y conclusión vía Gemini
- Chat contextual para preguntar sobre los documentos analizados
- Dashboard ejecutivo: licitaciones ganadas/perdidas, ranking de competidores, diferencial económico

### Alertas
- Reglas configurables por palabra clave, monto, tipo y organismo
- Expansión de keywords a sinónimos y conceptos relacionados vía IA
- Notificaciones enriquecidas (requisitos, presupuesto, competidores, señales de renovación) por canal in-app, Telegram y email

### Inteligencia de competidores
- Scraping del Cuadro de Ofertas público por competidor
- Análisis con IA on-demand sobre el historial de ofertas de un competidor en un rango de fechas (cacheado)

### Mensajería y notificaciones
- Chat interno en tiempo real (SignalR)
- Centro de notificaciones in-app

## Tecnologías

**Backend**
- .NET 8 (monolito modular), C#
- PostgreSQL 16 + Dapper, acceso exclusivamente vía stored procedures (`usp_*`)
- Autenticación JWT
- SignalR con backplane Redis (chat en tiempo real)
- Swagger / Swashbuckle

**Frontend**
- React 18 + TypeScript, Vite
- Ant Design 5 + Ant Design X (componentes de chat con IA)
- TanStack Query
- React Router 6
- Playwright (tests E2E)

**Inteligencia artificial**
- Google Gemini vía Vertex AI + Application Default Credentials (ADC)

**Infraestructura**
- Docker Compose (Postgres, Redis, API, Web) para desarrollo local
- Despliegue en GCP (Cloud Run + Cloud Run Jobs), almacenamiento de adjuntos en Google Cloud Storage

## Arquitectura

Monolito modular: cada dominio de negocio es una librería de clases independiente con su propio `Controllers/`, `Services/`, `Data/` y `Models/`, registrada vía `AddXxxModule()` en `Program.cs`.

```
MPM.Shared          Modelos compartidos, IStorageService
MPM.Core            DbConnectionFactory, middlewares (error handling, multi-tenancy)
MPM.Modules.Auth            Autenticación JWT, recuperación de contraseña
MPM.Modules.Licitaciones    Sync con API de Mercado Público, scraper, búsqueda
MPM.Modules.Catalogo        Datos de referencia (estados, tipos, monedas)
MPM.Modules.Mensajeria      Chat en tiempo real (SignalR)
MPM.Modules.Analisis        Workspace de análisis de documentos + IA
MPM.Modules.Notificaciones  Notificaciones in-app
MPM.Modules.Alertas         Alertas por keyword, entrega multicanal
MPM.Modules.Competidores    Inteligencia de competidores
```

Detalle técnico completo (flujos de datos, background services, convenciones) en [CLAUDE.md](CLAUDE.md).

## Inicio rápido

Requiere Docker Desktop y un archivo `.env` en la raíz (ver `docker-compose.yml` para las variables).

```bash
cp .env.example .env    # completar credenciales
docker compose up --build
```

| Servicio   | URL                            |
|------------|---------------------------------|
| Frontend   | http://localhost:8181          |
| API        | http://localhost:5001          |
| Swagger    | http://localhost:5001/swagger  |
| PostgreSQL | localhost:5433                 |
| Redis      | localhost:6379                 |

Guía completa de desarrollo local (sin Docker), estructura del proyecto y variables de entorno en [QUICKSTART.md](QUICKSTART.md).

## Testing

```bash
# Backend
dotnet test MPM.sln

# Frontend E2E
cd src/mpm-web && npm run test:e2e
```

## Estructura del repositorio

```
src/            Módulos backend (.NET 8) y frontend (mpm-web)
tests/          Tests unitarios/integración (xUnit) y E2E (Playwright)
tools/          Scraper de licitaciones (scraper-mp-v2) y procesador de documentos
specs/          Especificaciones de features (Spec Kit) y roadmap
docs/           Documentación técnica
scripts/        Scripts de despliegue y operación
QA/             Reportes de control de calidad
```

## Roadmap / Sprints

Historial completo de sprints y su estado en [docs/cu010_sprints.txt](docs/cu010_sprints.txt). Roadmap técnico vivo por fases en [specs/ROADMAP.md](specs/ROADMAP.md).

## Licencia

Uso interno — proyecto privado, no distribuir fuera del equipo del cliente.
