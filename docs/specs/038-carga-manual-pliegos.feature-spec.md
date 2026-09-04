# Feature Spec: Carga manual de pliegos (038)

**Feature:** 038-carga-manual-pliegos  
**Rama:** `038-carga-manual-pliegos`  
**Generado por:** orchestrator (design) — feature-spec  
**Fecha:** 2026-08-25  
**ADR:** `docs/adr/ADR-015-carga-manual-pliegos.md`  
**Spec origen:** `docs/api-first/licitaciones-documentos.md` (V141) — se extiende con endpoint `upload-manual`  
**Superficie REST:** Nueva (1 endpoint) + 1 flag de config — por eso usa `api-first-spec` para el endpoint y `feature-spec` para el comportamiento UI.

---

## 1. Scope

### Included

*   Nuevo flujo **manual** para pliegos de una licitación: el usuario abre la ficha oficial en Mercado Público (link externo) y sube los PDFs/DOCs descargados desde su PC.
*   Nuevo endpoint `POST /api/v1/licitaciones/{codigoExterno}/documentos/upload-manual` (multipart/form-data, `file-upload` skill). Valida, sube a GCS/local via `IStorageService`, calcula SHA-256, persiste en `licitaciones_adjuntos` con `metodo_extraccion='manual'`, `sha256_hash`, `version`, `descarga_estado='completado'`.
*   Preservación del scraper automático como **referencia deprecada**: `tools/scraper-mp-v2/descargar-documentos.js` marcado `@DEPRECATED`, `AdjuntoDescargaService` marcado `[Obsolete]`, flag `Extraccion:ModoDescarga = manual|auto` (default `manual`). En `manual`, el botón/flujo automático se oculta y `POST /documentos/descargar` responde `DOC_007` (feature deshabilitada).
*   UI `DocumentosLicitacionPanel.tsx` v2: link "Ver en Mercado Público" + zona drag & drop + lista de archivos + hash conjunto + botón "Analizar con IA" (habilitado cuando hay >=1 archivo analizable). Reutiliza `AnalisisComercialService` sin cambios (lee desde storage).
*   Compatibilidad: `GET /documentos` muestra tanto origen `navegador` (auto) como `manual` en la misma tabla, sin duplicar fuente.

### Excluded

*   Clasificación fina de documentos (administrativo/técnico) — lo hace la IA en `AnalisisComercialService` (Fase 1.3).
*   Chunked upload / presigned URL — innecesario para <20MB; descartado por simplicidad (`file-upload` skill: multipart <10MB default, aquí 20MB).
*   Borrado del scraper — prohibido por ADR-015 (se conserva).
*   Cambios en `licitaciones_adjuntos` schema — se reutilizan columnas V141 (`sha256_hash`, `version`, etc.), solo se usa nuevo valor `metodo_extraccion='manual'`.
*   Virus scanning síncrono — diferido; se registra `virus_scan_status='pending'` si aplica.

---

## 2. Actors & Triggers

| Actor | Rol |
|---|---|
| Usuario comercial (Account Manager) | Abre ficha de licitación, descarga pliegos en Mercado Público, los arrastra al panel y dispara "Analizar con IA". |
| Sistema (DocumentosLicitacionController) | Valida archivos (MIME magic bytes, tamaño), sube a GCS, calcula hash, persiste. |
| Sistema (AnalisisComercialService) | Lee archivos desde storage (sin saber si vinieron de scraper o manual) y produce Go/No-Go. |
| Admin/Sistema | Cambia flag `Extraccion:ModoDescarga` a `auto` para re-habilitar descarga auto (solo si desbloqueo futuro). |

**Triggers:**
*   `onDrop` / `onFileSelect` en `DocumentosLicitacionPanel` -> `POST /upload-manual`
*   Click "Ver en Mercado Público" -> `window.open(fichaUrl, _blank)` (no trigger backend)
*   Click "Analizar con IA" -> flujo existente `POST /analisis-comercial` (sin cambios)

---

## 3. Data Touched

| Entidad | Lectura/Escritura | Detalle |
|---|---|---|
| `licitaciones` | Lectura | Valida `codigoExterno` existe (LIC_001). Obtiene `licitacion_id`. |
| `licitaciones_adjuntos` | Escritura | `usp_Adjuntos_UpsertConHash` por cada archivo subido: `tipo`, `nombre_archivo`, `ruta_storage` (GCS), `tamanio_bytes`, `mime_type`, `sha256_hash`, `metodo_extraccion='manual'`, `descarga_estado='completado'`, `version` (incrementa si hash distinto). |
| `extraccion_documentos_log` | Escritura | `usp_ExtraccionLog_Registrar(..., 'manual', 'exito', N, ...)` por lote (trazabilidad). |
| `analisis_licitacion_comercial` | Lectura (indirecta) | Sin cambios; el análisis posterior lee el `conjuntoHash` de adjuntos manuales. |

Sin migraciones. Columnas V141 ya existen.

---

## 4. Behavior Spec

*   Dado una licitación existente, cuando el usuario sube 1-10 archivos válidos (PDF/DOC/DOCX/XLS/XLSX/ZIP/TXT, <=20MB c/u) via `POST /upload-manual`, entonces el sistema persiste cada archivo con `sha256_hash` y responde `200 { descargados: N, conjuntoHash }`, y `GET /documentos` muestra los N archivos con `metodo_extraccion='manual'` y `estadoConjunto='completado'`.
*   Dado un archivo con MIME no permitido (ej. `.exe`) o >20MB, cuando se sube, entonces el sistema rechaza **ese archivo** con `DOC_008` (422) y persiste los demás válidos del mismo lote (partial success).
*   Dado un archivo con extensión `.pdf` pero magic bytes no PDF (`%PDF`), cuando se sube, entonces se rechaza con `DOC_009` (422) (validación magic bytes, `file-upload` skill).
*   Dado un usuario no autenticado, cuando intenta `POST /upload-manual`, entonces recibe `401 AUTH_001` (consistent con `GET /documentos`).
*   Dado `Extraccion:ModoDescarga=manual` (default), cuando el frontend carga `DocumentosLicitacionPanel`, entonces **no** muestra el botón "Descargar documentos" automático; muestra link "Ver en Mercado Público" + dropzone.
*   Dado `Extraccion:ModoDescarga=auto`, cuando el frontend carga el panel, entonces muestra el flujo antiguo (botón Descargar + polling) **además** del dropzone manual (híbrido). `POST /documentos/descargar` funciona.
*   Dado `Extraccion:ModoDescarga=manual`, cuando se llama `POST /documentos/descargar`, entonces responde `501 DOC_007 "Descarga automática deshabilitada (ADR-015). Use carga manual."`
*   Dado que el usuario sube un archivo con el mismo contenido (mismo SHA-256) que uno ya existente, cuando se persiste, entonces `version` no incrementa, `tamanio_bytes` se actualiza y se retorna `reutilizados++` (mismo que `usp_Adjuntos_UpsertConHash` hace para scraper).
*   Dado que el usuario sube archivos y luego dispara `POST /analisis-comercial`, cuando `AnalisisComercialService` procesa, entonces analiza los archivos manuales igual que los del scraper (bytes via `IStorageService`, `DocumentContentExtractor`, mismo prompt), sin distinguir origen.
*   Dado que el scraper es invocado manualmente con flag `manual` (error de config), cuando `AdjuntoDescargaService.IniciarDescargaAsync` se llama, entonces loggea `Warning` y retorna `DOC_007` sin disparar `node`.

---

## 5. UI States

**Pantalla:** `DocumentosLicitacionPanel.tsx` (única afectada; `AnalisisComercialPanel` sin cambios salvo habilitación del botón).

| Estado | Comportamiento |
|---|---|
| `pendiente` (sin docs, sin upload previo) | Card informativo + Link "Ver en Mercado Público" (button `type=link` con `ExternalLink`) + Dropzone grande (`Instrucción: Arrastra los pliegos aquí o haz click para seleccionar` + hint `PDF, DOC, DOCX, XLS hasta 20MB`) + Botón "Analizar" deshabilitado. |
| `uploading` (durante `POST /upload-manual`) | Dropzone disabled + `Progress` por archivo (0-100%) + `Spin tip="Subiendo y validando..."` |
| `completado` con docs (manual o mixto) | Alert `success` con `N archivos (manual)` + Lista `List` con icono por extensión + Tag `manual`/`navegador` + `formatTamanio` + `version` + botón `Descargar` (GET /archivo) + `conjuntoHash` corto + Botón `Analizar con IA` habilitado + Link persistente a Mercado Público. |
| `completado` sin docs (portal sin adjuntos o usuario no subió) | Card dashed con `InfoCircle` + texto "No hay documentos cargados" + Dropzone visible + Link a Mercado Público. |
| `error` parcial (algún archivo rechazado) | Alert `warning` con lista de `DOC_008/DOC_009` por archivo + lista de exitosos. |
| Flag `auto` habilitado | Además de lo anterior, muestra botón secundario "Descargar automático (deprecado)" con `Tooltip: ADR-015 - puede fallar por reCAPTCHA` |

**Accesibilidad:** Dropzone con `role="button"` + `aria-label="Zona de carga de pliegos"` + keyboard `Enter` abre file picker (WCAG 2.2 AA).

---

## 6. Business Rules

*   **UPLOAD-R001:** Máximo 20MB por archivo, máximo 10 archivos por request. `VAL_001` si excede.
*   **UPLOAD-R002:** MIME permitidos (validación por magic bytes + extensión): `application/pdf`, `application/msword`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`, `application/vnd.ms-excel`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `application/zip`, `text/plain`. `DOC_008` si no permitido.
*   **UPLOAD-R003:** Magic bytes: PDF debe empezar con `%PDF` (0x25 0x50 0x44 0x46). Si no coincide, `DOC_009`. Otros tipos: validación extensión vs MIME (mapa `file-upload` skill).
*   **UPLOAD-R004:** Nombre en storage = `licitaciones/{codigo}/manual/{uuid}_{sanitizedOriginal}` via `IStorageService.UploadAsync`. Nunca nombre original directo (seguridad).
*   **UPLOAD-R005:** `metodo_extraccion='manual'` obligatorio para uploads manuales; `navegador` reservado para scraper (trazabilidad).
*   **UPLOAD-R006:** `descarga_estado='completado'` para manual; `version` incrementa solo si `sha256` cambia (misma lógica que `UpsertConHash`).
*   **UPLOAD-R007:** `forzar` no aplica a manual (siempre upsert). Si archivo idéntico (hash igual) -> `reutilizado`.
*   **FLAG-R001:** `Extraccion:ModoDescarga` default `manual`. Solo `auto` habilita `POST /descargar`. `DOC_007` si se llama en `manual`.
*   **SEC-R001:** `[Authorize]` en nuevo endpoint, cualquier rol autenticado (consistente con `DocumentosLicitacionController`).
*   **SEC-R002:** `ruta_storage` no se expone en DTO (ya existente `DOC-R022`).

---

## 7. Non-Goals

*   No se elimina código del scraper ni `AdjuntoDescargaService` — se depreca (ADR-015).
*   No se añade virus scanning síncrono ni OCR.
*   No se cambia `AnalisisComercialService` ni prompt de IA.
*   No se añade chunked upload ni presigned URL (innecesario para 20MB).
*   No se migra data existente (adjuntos previos quedan con `metodo_extraccion='navegador'`).

---

## 8. Acceptance Criteria

*   [ ] `POST /upload-manual` con 1 PDF válido persiste en `licitaciones_adjuntos` con `sha256_hash` y `metodo_extraccion='manual'`, y `GET /documentos` lo lista con `estadoConjunto='completado'`.
*   [ ] Subir `.exe` o PDF con magic bytes inválidos es rechazado con `DOC_008`/`DOC_009` y no persiste.
*   [ ] Archivo >20MB o lote >10 archivos es rechazado con `VAL_001`.
*   [ ] `AnalisisComercialService` analiza archivos manuales y produce `go_no_go`/`resumen_ejecutivo` igual que con scraper.
*   [ ] Con flag `manual` (default), UI no muestra "Descargar automático"; muestra link a Mercado Público + dropzone. Con flag `auto`, muestra ambos.
*   [ ] `POST /documentos/descargar` con flag `manual` responde `DOC_007` (feature deshabilitada).
*   [ ] `AdjuntoDescargaService` y `descargar-documentos.js` tienen marca de deprecación y no se invocan en `manual`.
*   [ ] Tests: `unit` para validación magic bytes + `integration` para upload happy path + `playwright` para dropzone (si aplica).
