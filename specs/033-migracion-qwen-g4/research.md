# Research: Migración Gemini 2.5 Pro → Qwen 3.7 (G4)

**Spec**: `specs/033-migracion-qwen-g4/spec.md` | **Date**: 2026-08-11

## Estado actual verificado (código, 2026-08-11)

Cuatro usos de IA, todos acoplados a Google Vertex AI:

| # | Módulo | Servicio | Modelo (constante) | Estilo de llamada |
|---|--------|----------|--------------------|-------------------|
| 1 | `MPM.Modules.Analisis` | `GeminiService` | `gemini-2.5-pro` | `VertexGeminiClient` (MPM.Shared) |
| 2 | `MPM.Modules.Competidores` | `CompetidorGeminiService` | `gemini-2.5-pro` | `VertexGeminiClient` (MPM.Shared) |
| 3 | `MPM.Modules.Licitaciones` | `ConsultaSemanticaService` | `gemini-2.5-flash-lite` | HTTP crudo a Vertex (copia local) |
| 4 | `MPM.Modules.Alertas` | `SinonimosIaService` | `gemini-2.5-flash` | HTTP crudo a Vertex (copia local) |

Hallazgos clave:

- `VertexGeminiClient.GenerarContenidoAsync(model, requestBody, ct)` ya centraliza auth (ADC, bearer), envío, manejo de errores y parseo. Vive en `MPM.Shared.Services` (compartido por Análisis y Competidores desde spec 029).
- El body del request es **formato Gemini**: `contents[].parts[]` con `fileData` (PDF en GCS) o `inlineData` (base64), `generationConfig.responseMimeType = "application/json"`, `maxOutputTokens = 65536` (constante compartida, subida por bug real de truncamiento).
- Los módulos 3 y 4 duplican el patrón de HTTP + parseo (deuda ya conocida; spec 029 centralizó solo 1 y 2).
- `modelo_usado` se persiste en BD desde `GeminiService.ModelName` / `AnalisisService.cs:151` / `AnalisisBackgroundService.cs:180`. Columna `varchar` — soporta cualquier identificador nuevo sin migración.
- `docs/infraestructura-cu010-v4.md` y `v5.md` ya documentaron el plan de migración de Fase 5: interfaz `IAnalisisIAService`, switch `AI:Provider` (`gemini | kimi | pangu`), reemplazo de `Gemini__ApiKey` → `AI__ApiKey`. **El diseño ya contemplaba esto; el código nunca lo implementó.**
- Spec `031-feedback-chilecompra` registra la evaluación en curso de Qwen/Gemma cuantizado para el data center TIVIT (on-premise), con benchmark + estimación de costos por separado.

## Decisiones

### D1. Abstracción del proveedor: interfaz propia liviana (NO Microsoft.Extensions.AI)

- **Decision**: Definir `ILlmClient` en `MPM.Shared.Services` con un método único tipo `GenerarContenidoAsync(string model, object requestBody, CancellationToken)` y respuesta tipada (`LlmResult`: text, raw, usage), más un factory registrado por `AI:Provider`.
- **Rationale**: El código actual ya usa HttpClient crudo sin SDKs (estilo consistente, sin deuda nueva). `VertexGeminiClient` ya es un "cliente" compartido; se convierte en la implementación `gemini` de `ILlmClient` (cambio mínimo). Microsoft.Extensions.AI (`IChatClient`) fue evaluado: provee middleware útil (caching, telemetría, function calling) pero **no trae routing/failover entre proveedores** y agrega una dependencia para un problema que hoy es un switch de configuración. La constitución favorece simplicidad; los docs v4/v5 ya definieron el switch.
- **Alternatives considered**: `Microsoft.Extensions.AI` `IChatClient` + `AddChatClient` (descartado por dependencia extra sin routing nativo); abstracción por módulo (descartada: duplica el problema, el cliente ya está compartido).

### D2. Contrato de request/response neutral al proveedor

- **Decision**: `ILlmClient` recibe el request en un formato neutral mínimo (lista de partes de contenido: texto, PDF inline base64, referencia de archivo opcional) y el proveedor lo traduce a su formato nativo (Gemini `contents[]` vs OpenAI `messages[]`). El parseo de salida queda en el cliente (como hoy), devolviendo `LlmResult` con text/raw/usage.
- **Rationale**: Hoy los prompts y parsers viven en los servicios de módulo (`GeminiService.GetAnalisisPrompt`, `ParseGeminiResponse`); mover el armado del body al cliente evita duplicar prompts. La traducción de formato queda aislada en la implementación de cada proveedor.
- **Alternatives considered**: Enviar el body Gemini nativo a ambos (descartado: Qwen OpenAI-compatible no entiende `contents[]`/`fileData`); exponer prompts por proveedor (descartado: duplica prompts y parsers).

### D3. Qwen G4: API compatible con OpenAI, JSON mode

- **Decision**: Qwen 3.7 G4 se sirve vía endpoint OpenAI-compatible (`/v1/chat/completions`). El cliente nuevo (`OpenAiCompatClient`) usa `Authorization: Bearer <AI:ApiKey>` y `response_format: { "type": "json_object" }` (o guided JSON según el servidor). Se recomienda **vLLM** como servidor (mejor soporte de JSON mode y contexto largo); Ollama/llama.cpp aceptables para pruebas.
- **Rationale**: Es el estándar de facto para servir modelos cuantizados; el mismo endpoint sirve para vLLM, Ollama, llama.cpp y MaaS (el camino Huawei de v5 también expone OpenAI-compatible en `/v1/infers/...`), lo que mantiene una sola implementación para los dos destinos candidatos.
- **Alternatives considered**: OpenAI SDK oficial (dependencia extra, mismo formato por debajo); API nativa de Vertex para Qwen (no aplica: Vertex no sirve Qwen cuantizado).

### D4. PDFs: base64 inline como equivalencia de `fileData` GCS

- **Decision**: En el camino Qwen, los PDFs se envían como `data:application/pdf;base64,...` en el contenido del mensaje (equivalente directo de `inlineData`). El caso `fileData` de GCS (referencia directa a `gs://`) **no existe** en OpenAI-compatible: se valida en el benchmark si el servidor de Qwen acepta URL de archivo; si no, todos los documentos van inline.
- **Rationale**: Preserva el flujo multi-documento y el análisis con PDFs escaneados sin cambiar el pipeline. Costo conocido: payload más grande y posible límite de tamaño → se mide en el benchmark (D6) y, si falla, la alternativa es extraer la capa de texto (OCR/PDf text) y enviar texto plano (optimización posterior, fuera del MVP de la migración).
- **Alternatives considered**: Extraer texto y mandar solo texto (más liviano, pero cambia el pipeline y puede perder fidelidad en documentos escaneados); OCR previo como paso obligatorio (descartado: no existe hoy y agrega infraestructura).

### D5. Configuración y secretos (dos niveles: persistida + entorno)

- **Decision**: Dos niveles de configuración con precedencia `BD > entorno > default`:
  - **Persistida (BD)**: tabla nueva `system_ai_provider` (ver `data-model.md`) con proveedor activo, endpoint, modelo, auditoría. Es la fuente de verdad en runtime; sobrevive reinicios (FR-015).
  - **Entorno (fallback/bootstrapping)**: `AI:Provider` (`gemini` | `openai`), `AI:Endpoint` (base URL del servidor Qwen), `AI:Model` (id del modelo, ej. `qwen3.7-g4`), `AI:ApiKey` (opcional en on-premise; si vacía, sin header). Se usa cuando la tabla está vacía o ante error de BD al arrancar (FR-017, edge case "config no disponible").
  - `Gemini__ApiKey`/ADC siguen siendo la ruta `gemini` (rollback intacto). Los secretos van a CSMS, nunca al repo.
- **Rationale**: El switch del super admin (US4) exige que el proveedor activo no sea una env var estática (requeriría reinicio); la precedencia BD>env garantiza que el arranque con tabla vacía (migración inicial, nuevos ambientes) siga funcionando con el env como hoy. Consistente con v4/v5 (`AI__ApiKey` reemplaza `Gemini__ApiKey`).
- **Alternatives considered**: Solo env vars (descartado: el switch requeriría reinicio/redeploy); solo BD sin env (descartado: rompe el bootstrapping de ambientes nuevos y el runbook de contingencia sin UI).

### D6. Benchmark de calidad (harness US2)

- **Decision**: Script/consola en el repo (`tools/` o `tests/` de integración, ejecutable por comando) que: (1) toma un conjunto de documentos reales desde GCS o una ruta externa (NUNCA en el repo — lección de `benchmark/` removido), (2) ejecuta el mismo prompt de análisis contra ambos proveedores, (3) compara campo a campo el JSON (fechas, montos, criterios, puntuaciones) con normalización de formatos, (4) reporta: paridad por campo, tasa de JSON válido, tasa de truncamiento, latencia p50/p95, y recomendación go/no-go.
- **Umbral acordado (decisión de negocio, Q1)**: **≥ 90% de campos críticos idénticos** + revisión manual de discrepancias, con montos y criterios con prioridad de revisión. El informe ordena las discrepancias por criticidad.
- **Rationale**: La extracción estructurada es el riesgo central de una cuantización G4; medirla con documentos reales es el gate de US5. El harness reutiliza `GetAnalisisPrompt` y los parsers existentes para que la comparación sea apples-to-apples.
- **Alternatives considered**: Benchmark manual con capturas (no reproducible); comparar solo tasa de éxito de parseo (insuficiente: el JSON puede ser válido y los campos mal); umbral del 95% (exigente para G4, podía bloquear sin necesidad).

### D7. Alcance de la migración

- **Decision**: **Todo migra a Qwen — sin uso de Google a partir de la migración** (decisión de negocio, Q2). Los 4 usos (análisis, competidores, búsqueda semántica, sinónimos) usan el mismo mecanismo de selección con un único switch global `AI:Provider`. Gemini queda implementado únicamente como proveedor de rollback/contingencia y como opción "gcloud" del switch (la mudanza a infra privada puede requerir alternar mientras ambas infraestructuras coexisten).
- **Rationale**: Un solo switch global es lo más simple de operar y auditar; el costo marginal de mantener el camino Gemini es bajo (ya existe y no se elimina). La decisión elimina la dependencia de Google del flujo normal.
- **Alternatives considered**: Migrar solo análisis de PDFs (descartado por decisión de negocio: "todo se migrará"); per-provider por uso (más configuración de la necesaria).

## Decisiones complementarias (ampliación de alcance)

### D8. Switch del super admin: resolución dinámica del proveedor

- **Decision**: El proveedor activo se resuelve **por request** (no en arranque): un `LlmClientResolver` (en MPM.Shared, registrado singleton) lee la configuración persistida (tabla `system_ai_provider` vía SPs, cache en memoria con TTL corto ~30s e invalidación explícita al cambiar el switch) y devuelve el `ILlmClient` del proveedor activo. El cambio del switch aplica al análisis siguiente sin reinicio (FR-013).
- **Dónde vive**: Tabla + SPs + servicio + handler en **MPM.Core/Data** (infraestructura transversal, como `TenantMiddleware`/`DbConnectionFactory`); controller `SystemConfigController` en **MPM.Api/Controllers** (mismo patrón que `HealthController`); página de administración en frontend (`/admin/ia`) visible solo para SuperAdmin.
- **Autorización**: rol `SuperAdmin` del JWT (policy de rol en el controller; el frontend oculta el menú/página sin ese rol). `admin@tivit.cl` es el seed del rol; V117 usó chequeo exacto de email para notificaciones por un caso legacy, pero para el switch el rol es la fuente correcta (V070/V102 siembran otros SuperAdmins reales).
- **Auditoría**: `system_ai_provider` guarda `updated_by_user_id`, `updated_by_username`, `updated_at` (FR-014). La UI muestra el último cambio.
- **Rationale**: La mudanza a infraestructura privada exige alternar gcloud/qwen sin despliegues; un resolver con cache corta es simple (sin Redis para este caso: baja frecuencia, consistencia eventual de 30s aceptable) y robusto (fallback a env ante BD caída).
- **Alternatives considered**: Reinicio del servicio tras cambiar env (descartado: requiere despliegue); Redis como store (descartado: sobre-ingeniería para una config de baja frecuencia); resolución sin cache (descartado: una consulta SP por cada request de IA es innecesaria).

### D9. Endpoint del proveedor Qwen (URL entregada)

- **Decision**: El equipo proveedor entrega una **URL base** del servidor Qwen antes de implementar US3. Se configura en `AI:Endpoint` (entorno) y se persiste en `system_ai_provider.endpoint` al activar Qwen desde el switch. Hasta que la entreguen, se usa un placeholder de desarrollo (`http://localhost:8000/v1`) y el smoke test del contrato (`contracts/openai-compat-api.md`) valida la URL real.
- **Rationale**: La decisión de hosting ya está tomada por el equipo (Q3: "nos darán una url"); el plan queda agnóstico al hosting y solo valida compatibilidad OpenAI (vLLM/Ollama/MaaS).
- **Alternatives considered**: Elegir infraestructura propia (descartado: ya hay una decisión de equipo; el plan no debe inventar hosting).

## Riesgos

| Riesgo | Mitigación |
|--------|-----------|
| Degradación de calidad de extracción por cuantización G4 | Harness US2 con umbral ≥ 90% + revisión manual de discrepancias (D6) |
| Payload PDF inline excede límites del servidor Qwen | Benchmark con los PDFs más pesados del historial; alternativa texto plano (D4) |
| Truncamiento de respuestas largas (65536 tokens) | Mantener presupuesto de tokens; medir tasa de truncamiento en benchmark; logging dedicado |
| Latencia mayor en infraestructura privada (hardware desconocido) | Medir p50/p95 en benchmark; runbook de cutover contempla impacto |
| Rollback difícil si hay análisis a medio camino | `modelo_usado` persiste el modelo real; rollback por switch (US4) o env (US5), probado en < 30 min |
| Los 3 estilos de llamada duplicados rompen en la unificación | US1 refactoriza con regresión completa (suite de tests existente + validación manual) |
| Dos admins cambian el switch casi a la vez | Escritura por SP con UPSERT atómico: el último cambio gana; auditoría registra ambos valores |
| BD de configuración caída al resolver proveedor | Fallback a env vars (D5); el resolver loguea el fallback para diagnóstico |
| Cambio de switch no visible por cache TTL | TTL de 30s máximo + invalidación explícita de cache al escribir; el análisis siguiente usa el nuevo proveedor |
| URL entregada tarde o con formato distinto al esperado | US3 arranca con placeholder de dev; smoke test del contrato valida la URL real antes de activarla |
| Dependencia residual de Google | FR-011: los 4 usos migran; Gemini queda solo como opción "gcloud" del switch y rollback (D7) |
