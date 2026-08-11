# Infraestructura CU010 — v6 (borrador, 033-migracion-qwen-g4)

> **Estado**: BORRADOR — se completa en US5 (T041–T044). Este documento parte como evolución de
> `infraestructura-cu010-v5.md` y registra el cambio de proveedor de IA: **Gemini/Google → Qwen
> (infraestructura privada)** con switch operativo de super admin.

## Política de proveedor de IA (cambio principal vs. v5)

| Aspecto | v5 (anterior) | v6 (objetivo) |
|---------|---------------|---------------|
| Proveedor principal | Google Gemini 2.5 Pro (transitorio, "estado transitorio") | **Qwen 3.7 (cuantización G4)** vía API OpenAI-compatible en infraestructura privada |
| Uso de Google | Todo el análisis IA | **Ninguno a partir de la migración**; Gemini queda solo como opción "gcloud" del switch y rollback |
| Selección de proveedor | Env var estática (`AI:Provider`), requería reinicio | **Switch del super admin en la UI** (tabla `system_ai_provider`, efecto < 1 min sin reinicio) |
| Hosting del modelo | Google Vertex AI | URL entregada por el equipo proveedor (`AI:Endpoint`) — infraestructura privada (TIVIT) |
| Contrato de integración | Vertex AI (`:generateContent`) | OpenAI-compatible (`/v1/chat/completions`), ver `specs/033-migracion-qwen-g4/contracts/` |

## Configuración del proveedor de IA

Precedencia de resolución en runtime: **BD (tabla `system_ai_provider`) > entorno > default (gemini)**.

| Variable | Default | Descripción |
|----------|---------|-------------|
| `AI:Provider` | `gemini` | `gemini` (Vertex AI) o `openai` (Qwen) — fallback/bootstrapping; el switch en BD la sobreescribe |
| `AI:Endpoint` | — | Base URL del servidor Qwen (la entrega el equipo proveedor), ej. `https://qwen.tivit.internal/v1` |
| `AI:Model` | `gemini-2.5-pro` | Id del modelo activo; se persiste en `analisis.modelo_usado` |
| `AI:ApiKey` | vacío | Token bearer para el camino `openai` (opcional en on-premise); **CSMS, nunca repo** |
| `Gemini__ApiKey` / ADC | — | Se mantiene para el camino `gemini` (rollback) mientras aplique |

## Nueva tabla (aplicada por migración V130)

- `system_ai_provider`: fila activa con `provider`, `endpoint`, `model`, auditoría
  (`updated_by_user_id`, `updated_by_username`, `updated_at`) e historial (`record_status`).
- SPs: `usp_SystemConfig_ObtenerAiProvider` / `usp_SystemConfig_ActualizarAiProvider`.

## Runbook: cutover a Qwen y rollback (US5)

### Cutover (Google → Qwen)

1. **Precondiciones**: benchmark go/no-go archivado (paridad ≥ 90%), URL real de Qwen + modelo confirmado, `AI:ApiKey` en CSMS si aplica.
2. Login como **super admin** (`admin@tivit.cl` o rol SuperAdmin) → `/admin/ia`.
3. Cambiar el switch a **qwen** → confirmar → ingresar URL del servidor (la entregada por el equipo) y modelo (ej. `qwen3.7-g4`).
4. **Verificar post-cambio**:
   - `GET /api/system/ai-provider` → `provider=openai`, `resolvedFrom=database`, `updatedBy=admin@tivit.cl`.
   - Análisis de una licitación nueva → `modelo_usado=qwen3.7-g4` y JSON completo en BD.
   - Búsqueda semántica y sinónimos de alertas operativos (mismo proveedor).
5. Tiempo objetivo del cutover: **< 15 minutos** (solo switch + verificación, sin despliegue).

### Rollback (Qwen → Google)

1. Login como super admin → `/admin/ia` → switch a **gcloud** → confirmar (modelo `gemini-2.5-pro`).
2. Verificar: `GET /api/system/ai-provider` → `provider=gemini`; análisis siguiente con `modelo_usado=gemini-2.5-pro`.
3. **Contingencia sin UI** (interfaz no disponible): restaurar la fila activa de BD:
   ```sql
   UPDATE system_ai_provider SET record_status = 'I' WHERE record_status = 'A';
   -- con la tabla vacía, el runtime cae a env: AI:Provider=gemini (o default)
   ```
   y si hace falta setear `AI:Provider=gemini` en el entorno + reiniciar el servicio.
4. Tiempo objetivo del rollback: **< 30 minutos**, sin pérdida de análisis pendientes (los análisis en curso terminan con su modelo; `modelo_usado` registra el real).

### Notas operativas

- El switch **no reinicia** el servicio: la cache del proveedor se invalida al guardar y el análisis siguiente usa el nuevo proveedor (efecto < 1 min).
- El env var `AI:Provider` es solo fallback/bootstrapping; con fila en BD, la BD manda (FR-017).
- Gemini queda como opción "gcloud" del switch y rollback — la política es **sin uso de Google a partir de la migración** (US5), salvo contingencia.

## Pendiente (US5 — validación real en staging)

- [x] Runbook de cutover/rollback documentado (arriba) con tiempos objetivo.
- [ ] Ejecutar el drill en staging: cutover a Qwen → validación 1 día → rollback a gcloud → medir tiempos y ajustar.
- [ ] Confirmación de la URL real de Qwen y del identificador del modelo en el servidor proveedor.
- [ ] Actualización completa del diagrama de infraestructura y del resto de secciones heredadas de v5.
