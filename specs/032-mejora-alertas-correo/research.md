# Research: Mejora de Alertas por Correo

## R1 — Matching de keywords sin límites de palabra

**Decision**: Reemplazar `texto.Contains(keyword)` por una comparación por límites de palabra usando `Regex.IsMatch` con `\b` alrededor del keyword escapado (`Regex.Escape`), insensible a mayúsculas (`RegexOptions.IgnoreCase`). Aplica tanto al keyword principal como a cada sinónimo IA en `EvaluarMatch` (`AlertasMatchingService.cs` líneas 261 y 269).

**Rationale**:
- El bug real (`AlertasMatchingService.cs:261`, `269`) es `string.Contains`, sin ningún límite de palabra — "TI" matchea el fragmento "ti" dentro de "par**ti**cipantes".
- `\b` en .NET Regex ya funciona correctamente para separar palabras en textos en español con acentos y sin ellos (los límites de palabra se basan en clases de caracteres `\w`/no-`\w`, y las licitaciones ya se comparan en minúsculas vía `ToLowerInvariant()`, que preserva vocales acentuadas).
- Para keywords compuestas por varias palabras ("mesa de ayuda"), `\b` al inicio y al final de la frase completa sigue funcionando igual que hoy (coincide si la frase completa aparece como bloque, límites de palabra en los extremos) — no rompe FR-002.
- Alternativa considerada — migrar a `tsvector`/`plainto_tsquery` de PostgreSQL (mismo mecanismo que se corrigió esta sesión para `areas_negocio`): **descartada** para este caso. El stemming de PostgreSQL en español convierte palabras a su raíz (ej. "comunicaciones"→"comun"), lo cual es *demasiado* permissive para keywords cortas/siglas como "TI" — el stemmer probablemente no tiene una raíz reconocible para una sigla de 2 letras y su comportamiento sería impredecible. Un regex de límite de palabra es más simple, más predecible, y no requiere tocar la capa de datos (el matching ya ocurre en C#, en memoria, contra `licitacion.Nombre`/`Descripcion` ya cargados).

**Alternatives considered**:
- `tsvector`/`plainto_tsquery` (PostgreSQL full-text): descartado — más complejo, comportamiento de stemming impredecible para siglas cortas, requeriría mover el matching de C# a SQL.
- Separar el texto en tokens (`Split` por espacios/puntuación) y comparar token por token: funcionalmente equivalente a un regex con `\b`, pero más código y no soporta frases multi-palabra de forma directa sin lógica adicional de ventana deslizante. El regex resuelve ambos casos (palabra suelta y frase) con una sola expresión.

## R2 — Contenido enriquecido del correo: qué campos ya están disponibles

**Decision**: Extender `LicitacionParaMatching` (record en `AlertasDtos.cs`) con dos campos nuevos: `FechaCierre` (`DateTime?`) y `Link` (`string?`). Ambos ya existen como columnas reales en la tabla `licitaciones` (`fecha_cierre`, `link` — este último poblado desde el campo `link` de la API de Mercado Público durante el sync, `V002__Create_licitaciones.sql` / `usp_SyncEngine_MergeLicitaciones`). `Organismo` ya está presente en `LicitacionParaMatching` hoy (se usa para el filtro de reglas) y no requiere cambios.

**Rationale**:
- No se necesita ninguna consulta nueva ni llamada externa — solo ampliar el `SELECT` de `usp_Licitaciones_ListarParaMatching` (la stored procedure que alimenta `ListarParaMatchingAsync`) para traer `fecha_cierre` y `link` junto con las columnas que ya trae, y propagar esos dos campos a través de `MatchingRow` → `LicitacionParaMatching` → `EmailNotificationService.EnviarAsync`.
- Esto respeta la restricción del usuario de priorizar campos ya disponibles antes que agregar consultas nuevas.

**Alternatives considered**:
- Hacer una consulta adicional a la ficha completa de la licitación al momento de enviar el correo: descartado — ya existe un mecanismo de "enriquecimiento en caliente" en `AlertasMatchingService.cs` (líneas 89-160) para cuando el organismo viene vacío desde el sync masivo; ese mecanismo ya deja `licitacion.Organismo` poblado antes de llegar al envío de correo, así que no hace falta duplicar esa lógica para `fecha_cierre`/`link` — esos dos campos ya vienen poblados desde el sync normal (no dependen del organismo).

## R3 — Horario del disparador (Cloud Scheduler)

**Decision**: Cambiar el schedule del job `sync-job-scheduler` en Cloud Scheduler de `0 3,15 * * *` a `0 8,15 * * *` (hora de Santiago, zona ya configurada en el scheduler existente). Comando: `gcloud scheduler jobs update ... --schedule="0 8,15 * * *" --location=us-central1 --project=tivit-cu010` (ajustar el tipo de job HTTP/Pub-Sub existente, sin cambiar el resto de su configuración).

**Rationale**:
- Confirmado en el código (`Program.cs`, `WORKER_MODE=sync` → `SyncEngineService.EjecutarCicloUnaVezAsync()`) que el ciclo de sync corre una sola vez por invocación del Cloud Run Job, sin timer interno — el horario lo controla 100% el cron externo de Cloud Scheduler, sin restricción de separación simétrica entre disparos.
- Las alertas se evalúan al final de ese mismo ciclo (`SyncEngineService` líneas ~92-104, invoca `AlertasMatchingService.EvaluarLicitacionesAsync`), así que mover el horario del sync mueve también el horario de las alertas — es el único punto de control existente, no hay un disparador independiente para alertas.

**Alternatives considered**:
- Crear un Cloud Scheduler separado solo para alertas, desacoplado del sync: descartado por alcance — las alertas dependen de que el sync ya haya corrido (evalúan licitaciones nuevas del ciclo), así que un disparador independiente necesitaría de todas formas esperar/depender del sync, agregando complejidad de orquestación sin beneficio real para el problema reportado.
