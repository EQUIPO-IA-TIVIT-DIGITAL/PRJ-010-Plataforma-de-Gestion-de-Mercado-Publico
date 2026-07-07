# Research: Fase 6 — Alertas Inteligentes por Palabras Clave

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)
**Fecha**: 2026-07-06

---

## 1. Motor de matching: ILIKE vs. full-text search vs. tsvector

**Decisión**: `ILIKE`/`unaccent` sobre nombre+descripción de la licitación, evaluado en un stored procedure ejecutado una vez por licitación nueva del ciclo de sync (no en cada request de usuario).

**Rationale**: El volumen es bajo — un ciclo de sync trae decenas de licitaciones nuevas, no miles, y las reglas de alerta por usuario son pocas (no cientos). `tsvector`/full-text ya existe para `usp_Licitaciones_BuscarNatural` (búsqueda interactiva de usuario), pero ese caso es distinto: ahí importa el ranking y la latencia de una request HTTP; acá es un job batch donde una query `ILIKE ANY(@keywords)` por licitación es más que suficiente y evita mantener un índice adicional solo para este propósito.

**Alternatives considered**: reutilizar el mismo `tsvector` de búsqueda — descartado por ahora (complejidad extra sin beneficio de latencia percibible en un proceso batch); se puede migrar después si el volumen de licitaciones o de reglas crece mucho.

---

## 2. Expansión de sinónimos vía IA: en cada ciclo vs. precalculada

**Decisión**: los sinónimos se calculan **una vez, al crear o editar la regla** (no en cada ciclo de matching), y se guardan en la propia fila de `alertas_reglas` (columna `sinonimos_ia JSONB`).

**Rationale**: Si se recalculan en cada ciclo de sync, cada regla activa dispara una llamada a Gemini por ciclo — costo y latencia innecesarios, dado que los sinónimos de una keyword no cambian entre ciclos. Precalcular al guardar la regla es más barato, más rápido en el matching (es solo una comparación contra un array ya resuelto), y permite mostrarle al usuario los sinónimos generados en el formulario antes de guardar (transparencia, pedido implícito de Francisco: *"la IA puede ir consultando por sinónimos... que la persona humana no puede encontrar"* — el usuario debe poder ver qué encontró la IA).

**Prompt**: reutiliza el mismo proveedor Gemini ya integrado (`MPM.Modules.Analisis.Services.GeminiService`, patrón `HttpClient` directo a `generativelanguage.googleapis.com`, sin SDK) — un prompt simple: *"Dado el término de búsqueda de licitaciones públicas '{keyword}', devuelve entre 5 y 10 sinónimos o términos relacionados que un comprador público podría usar en el nombre o descripción de una licitación. Responde solo JSON: {\"sinonimos\": [...]}"*.

**Alternatives considered**: tabla de sinónimos compartida/precalculada globalmente (no por regla) — se descarta para esta fase porque el Buscador Inteligente (`018-buscador-inteligente-nl`) todavía no está implementado; si en el futuro ambas features comparten un servicio de expansión de conceptos, se puede extraer a un servicio común en `MPM.Shared` sin romper el contrato de esta feature (la tabla de destino no cambia, solo quién la llena).

---

## 3. Resumen enriquecido: reusar análisis existente vs. generar ad-hoc

**Decisión**: al dispararse una alerta, `AlertaEnriquecimientoService` primero busca si la licitación ya tiene un análisis Gemini completado en `MPM.Modules.Analisis` (vía HTTP interno a su API existente, no referencia directa de proyecto — Principio I); si no existe (caso más común para una licitación recién publicada, que normalmente aún no tiene bases descargadas/analizadas), genera un resumen liviano con un prompt Gemini propio usando solo los metadatos ya sincronizados (nombre, descripción, organismo, monto, fechas) — sin esperar a que el pipeline de análisis de documentos complete (eso puede tardar).

**Rationale**: FR de la spec exige que campos no disponibles se marquen como "no determinado" en vez de inventar — el resumen liviano debe ser honesto sobre qué puede inferir de metadatos vs. qué requeriría los documentos (p. ej. "forma de pago" casi nunca está en el título/descripción, se marcará "no determinado" en la mayoría de los casos hasta que el análisis de documentos esté disponible).

**Alternatives considered**: esperar siempre al análisis de documentos antes de notificar — descartado porque el valor de una alerta es la inmediatez; el cliente prefiere una notificación rápida con lo que se sabe, no una notificación tardía perfecta.

---

## 4. Notificación por Telegram — nuevo 2026-07-06

**Decisión**: Bot de Telegram vía HTTP directo a la Bot API (`https://api.telegram.org/bot{token}/sendMessage`), mismo patrón `HttpClient` que `GeminiService` — sin SDK de terceros.

**Configuración**:
- `Telegram:BotToken` en config/Secret Manager (nunca en el repo).
- Nueva tabla `alertas_destinatarios` (`usuario_id`, `telegram_chat_id` nullable, `es_account_manager_gobierno BOOLEAN`) — no se reutiliza el sistema de roles de Auth porque hoy no existe el concepto de "account manager de gobierno" como rol; se modela como una tabla de configuración simple, editable sin necesidad de tocar JWT/roles.
- El `chat_id` de Telegram se obtiene manualmente (el usuario le escribe al bot una vez y Telegram expone su `chat_id` en el update — proceso estándar, documentado en el runbook, no automatizable sin que el usuario inicie la conversación primero).

**Rationale**: Manuel pidió explícitamente Telegram por precedente interno de TIVIT (otro proyecto ya lo usa) y por inmediatez — no requiere que el usuario tenga la app abierta. Falla de forma aislada (try/catch alrededor del envío) para no bloquear la notificación in-app, que sigue siendo la fuente de verdad (User Story 5, escenario 2 del spec).

**Alternatives considered**: email vía SMTP (ya configurado en `appsettings.json`, sección `Smtp`, no usado aún) — se descarta para esta ronda porque Manuel pidió Telegram específicamente, no email; queda como candidato natural para Fase 10 (Notificaciones Multicanal, pausada).

---

## 5. Función de "disparar alerta de prueba" (demo)

**Decisión**: endpoint `POST /api/v1/alertas/{id}/probar` que ejecuta el mismo pipeline de matching→enriquecimiento→notificación (in-app + Telegram) contra una licitación real ya existente en la base (elegida por el usuario desde un selector, no una licitación falsa/inventada), marcada internamente como `es_prueba=true` en `alertas_disparadas` para no contaminar las métricas de alertas reales.

**Rationale**: usar una licitación real ya sincronizada (no datos ficticios) mantiene la demo honesta — se está probando el pipeline completo (matching, IA, notificación), no mockeando el resultado.

---

## Resumen de decisiones

| Decisión | Elegido |
|---|---|
| Motor de matching | `ILIKE`/`unaccent`, evaluado por licitación nueva en el ciclo de sync |
| Expansión de sinónimos | Precalculada al crear/editar la regla, cacheada en `alertas_reglas.sinonimos_ia` |
| Resumen enriquecido | Reusa análisis de `MPM.Modules.Analisis` si existe; si no, genera resumen liviano con metadatos ya sincronizados |
| Notificación Telegram | HTTP directo a Bot API, tabla `alertas_destinatarios` para mapear `usuario_id` ↔ `telegram_chat_id` |
| Demo | Endpoint "disparar alerta de prueba" sobre una licitación real existente, marcada `es_prueba` |
| Migración | `V079__Create_Alertas.sql` (V078 ya usada por `016-extraccion-documentos-api`) |
