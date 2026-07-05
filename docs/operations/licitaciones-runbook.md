# Runbook Operativo: Módulo de Licitaciones

## Información General

| Campo | Valor |
|-------|-------|
| Módulo | Licitaciones |
| Servicio | MPM.Api |
| Prefijo API | `/api/v1/licitaciones` |
| Health Check | `/health/licitaciones` |
| Endpoints | 4 (Listar, Detalle, Buscar, Sync) |
| Sincronización | Cada 24 horas (BackgroundService) |
| Fuente externa | API Mercado Público (ChileCompra) |

## Endpoints

| Método | URL | Descripción |
|--------|-----|-------------|
| `GET` | `/api/v1/licitaciones` | Listado paginado con filtros |
| `GET` | `/api/v1/licitaciones/{codigoExterno}` | Detalle de licitación |
| `GET` | `/api/v1/licitaciones/buscar?q=...` | Búsqueda rápida (autocomplete) |
| `POST` | `/api/v1/licitaciones/sync` | Forzar sincronización manual |
| `GET` | `/health/licitaciones` | Health check del módulo |

## Sincronización

### Automática
- **Frecuencia**: Cada 24 horas
- **Rango**: Últimos 7 días desde la ejecución
- **Retry**: Hasta 3 reintentos con backoff (3s, 6s, 9s) ante HTTP 429
- **Delay entre días**: 2 segundos

### Manual
- `POST /api/v1/licitaciones/sync`
- Retorna `{ syncId, status, startedAt }`
- Status posibles: `COMPLETADO`, `FALLO`

### Monitoreo de Sync
- Tabla `sync_log` registra cada ejecución
- Campos: `tipo`, `creados`, `actualizados`, `errores`, `detalle_errores`, `estado`

## Base de Datos

### Tablas Principales
| Tabla | Propósito |
|-------|-----------|
| `licitaciones` | Datos principales de licitaciones |
| `licitaciones_items` | Items/partidas de cada licitación |
| `sync_log` | Historial de sincronizaciones |

### Funciones/Procedures
| Objeto | Tipo | Uso |
|--------|------|-----|
| `usp_Licitaciones_Listar(...)` | Function | Listado paginado con filtros |
| `usp_Licitaciones_ObtenerPorCodigo(...)` | Function | Detalle con items |
| `usp_Licitaciones_Buscar(...)` | Function | Búsqueda full-text |
| `usp_SyncLog_Iniciar(...)` | Procedure | Iniciar registro de sync |
| `usp_SyncLog_Finalizar(...)` | Procedure | Cerrar registro de sync |
| `usp_SyncEngine_MergeLicitaciones(...)` | Procedure | Merge masivo desde API |

### Índices Relevantes
- `idx_licitaciones_estado` — Filtrado por estado
- `idx_licitaciones_tipo` — Filtrado por tipo
- `idx_licitaciones_fecha_publicacion` — Ordenamiento por fecha
- `idx_licitaciones_organismo` — Filtrado por organismo
- `idx_licitaciones_busqueda` — Búsqueda full-text (GIN)

## Configuración

### Variables de Entorno Requeridas

| Variable | Descripción | Ejemplo |
|----------|-------------|---------|
| `ConnectionStrings__PostgreSQL` | Cadena de conexión PostgreSQL | `Host=db;Port=5432;Database=mpm;Username=mpm;Password=***` |
| `ConnectionStrings__Redis` | Cadena de conexión Redis | `redis:6379,password=***` |
| `MP_TICKET` | API Key de Mercado Público | (obtener en mercado publica.cl) |
| `JWT__Secret` | Clave secreta JWT (mín. 32 chars) | (generar con `openssl rand -base64 48`) |
| `JWT__Issuer` | Emisor del token | `TIVIT.MPM` |

## Troubleshooting

### Síntoma: Sincronización no ejecuta

1. Verificar que `SyncEngineService` esté corriendo (logs: `SyncEngineService starting`)
2. Verificar `MP_TICKET` en configuración
3. Verificar conectividad a `api.mercadopublico.cl`
4. Verificar tabla `sync_log` para estado de última ejecución

```sql
SELECT * FROM sync_log ORDER BY ejecutado_en DESC LIMIT 5;
```

### Síntoma: Error 429 Too Many Requests

1. La API de Mercado Público tiene límite de requests
2. El sistema reintenta automáticamente hasta 3 veces con backoff
3. Si persiste, revisar frecuencia de sync o contactar a Mercado Público

### Síntoma: Licitaciones vacías

1. Verificar health endpoint: `GET /health/licitaciones`
2. Verificar registros: `SELECT COUNT(*) FROM licitaciones WHERE deleted_at IS NULL`
3. Verificar última sincronización exitosa
4. Forzar sync manual: `POST /api/v1/licitaciones/sync`

### Síntoma: Detalle no carga enriquecimiento

1. El detalle se enriquece desde la API de Mercado Público bajo demanda
2. Si `MP_TICKET` es inválido, el detalle se mostrará sin datos adicionales
3. Verificar logs: `No se pudo obtener detalle de {Codigo}`

## SLOs

| Métrica | Objetivo |
|---------|----------|
| Disponibilidad API | 99.5% |
| Latencia GET /licitaciones | < 500ms (P95) |
| Latencia GET /licitaciones/{id} | < 1s (P95, sin enriquecimiento) |
| Latencia GET /licitaciones/{id} con enriquecimiento | < 3s (P95) |
| Sync manual | Completa en < 10 min |
| Datos actualizados | Máximo 24 horas de atraso |

## Alarmas

| Condición | Severidad | Acción |
|-----------|-----------|--------|
| `/health/licitaciones` devuelve 503 | Critical | Verificar PostgreSQL y datos |
| Sync fallida 3 veces seguidas | Warning | Verificar conectividad a API MP |
| Latencia > 2s en listado | Warning | Verificar índices y queries |
| Rate limit (429) sostenido | Warning | Reducir frecuencia de sync |