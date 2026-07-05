# Data Model: Fase 2 — Automatización del Scraping

**Feature**: MPM CU010 — Fase 2
**Date**: 2026-06-23

---

## Entidades existentes relevantes (sin cambios)

### `licitaciones`
- Ya existe desde V002
- El scraper hace upsert via `usp_Licitacion_UpsertFromScraper`
- Estado `adjudicada` = `codigo_estado = 5`

### `licitaciones_adjuntos` (V062)
```sql
id              BIGSERIAL PK
licitacion_id   BIGINT FK → licitaciones(id)
tipo            VARCHAR(50)   -- 'acta_evaluacion' | 'anexo'
nombre_archivo  VARCHAR(500)
nombre_elemento VARCHAR(500)
ruta_storage    TEXT          -- path local o gs://
ruta_local      TEXT
tamanio_bytes   BIGINT
mime_type       VARCHAR(100)
grid_origen     VARCHAR(30)
acta_descargada BOOLEAN DEFAULT false
analisis_estado VARCHAR(20)   -- 'pendiente' | 'procesando' | 'completado' | 'error'
analisis_workspace_id BIGINT  -- FK a analisis_workspaces cuando el pipeline crea workspace
created_at      TIMESTAMP
updated_at      TIMESTAMP
record_status   SMALLINT DEFAULT 1
```

### `scraper_sync_log` (V062)
```sql
id                   BIGSERIAL PK
tipo                 VARCHAR(20)   -- 'SCRAPER'
fecha_desde          TIMESTAMP
fecha_hasta          TIMESTAMP
registros_procesados INT
nuevos               INT
actualizados         INT
errores              INT
detalle_errores      JSONB
total_licitaciones   INT
total_con_acta       INT
total_sin_acta       INT
total_analizados     INT
ejecutado_en         TIMESTAMP DEFAULT NOW()
duracion_ms          BIGINT
estado               VARCHAR(10)   -- 'iniciado' | 'completado' | 'error'
```

### `analisis_workspaces` + `analisis_documentos` + `analisis_resultados`
- Ya existen desde V051–V055
- El pipeline IA crea un workspace por licitación, sube el PDF, y llama al endpoint de análisis
- El `AnalisisBackgroundService` procesa el PDF con Gemini y guarda el resultado

---

## Flujo de datos (Fase 2 end-to-end)

```
Mercado Público Web
    ↓ (Playwright: login + búsqueda)
agente-mp.js
    ↓ upsertLicitacion()
licitaciones (DB)
    ↓ descargarActaEvaluacion()
archivo PDF local (tools/scraper-mp/descargas/)
    ↓ registrarAdjunto()
licitaciones_adjuntos (DB) [tipo='acta_evaluacion', acta_descargada=true]
    ↓ pipelineAnalisisCompleto() → HTTP → MPM API
analisis_workspaces (DB) [estado='pendiente']
    ↓ subirDocumento() → IStorageService
analisis_documentos (DB) + archivo en /app/uploads o GCS
    ↓ iniciarAnalisis() → AnalisisBackgroundService.EnqueueAnalisis()
analisis_workspaces (DB) [estado='analizando']
    ↓ GeminiService.AnalyzePdfAsync()
analisis_resultados (DB) [contenido_json = JSON estructurado]
analisis_workspaces (DB) [estado='completado']
    ↓
licitaciones_adjuntos (DB) [analisis_estado='completado', analisis_workspace_id=N]
```

---

## Sin cambios de esquema en Fase 2

No se requieren nuevas migraciones SQL. Todos los objetos de DB necesarios están en las migraciones V001–V069.
