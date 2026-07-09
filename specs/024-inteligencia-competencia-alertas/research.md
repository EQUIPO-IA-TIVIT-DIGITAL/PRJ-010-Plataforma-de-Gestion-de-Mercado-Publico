# Research: Inteligencia de competencia, alertas interactivas y canal de correo

## R1 — ¿Módulo nuevo `MPM.Modules.Competidores` o extensión de módulos existentes?

**Decision**: Extender `MPM.Modules.Licitaciones` con la nueva entidad "ofertas" (es una extensión natural de "licitaciones" — cada oferta pertenece a una licitación) y crear un módulo nuevo **`MPM.Modules.Competidores`** solo para la capa de búsqueda/agregación/análisis-cacheado, que consume `MPM.Modules.Licitaciones` vía `MPM.Shared` (nunca referencia directa entre módulos, Principio I).

**Rationale**: "Ofertas por licitación" es dato de dominio de Licitaciones (mismo ciclo de vida, mismo sync/scraper). "Buscar competidor + análisis cacheado por periodo" es un concepto de negocio distinto (compara across licitaciones, con su propio ciclo de análisis IA) que no encaja limpio ni en Licitaciones ni en Alertas — mejor un módulo chico y enfocado, siguiendo el patrón ya usado para Alertas (que también nació como una capa que consume Licitaciones).

**Alternatives considered**: Meter todo en `MPM.Modules.Analisis` (ya tiene el cliente de Gemini) — descartado porque Análisis hoy es 1:1 con "un documento de una licitación", y esto es N:1 (muchas licitaciones agregadas por competidor), un modelo de datos distinto que ensuciaría el módulo existente.

## R2 — Cómo obtener el cliente de Gemini/Vertex AI para el análisis de competidor

**Decision**: Reusar la misma configuración/cliente de Vertex AI ya usado por `MPM.Modules.Analisis` (`GoogleAdcTokenProvider`, ADC vía `roles/aiplatform.user` ya otorgado a `mpm-api-sa`) — exponerlo como un servicio inyectable desde `MPM.Shared` si no lo está ya, para que `MPM.Modules.Competidores` lo consuma sin depender directamente de `MPM.Modules.Analisis`.

**Rationale**: Ya está probado en producción (ver `020-migracion-gemini-adc`), no requiere una nueva credencial ni un nuevo rol IAM.

**Alternatives considered**: Duplicar la configuración de Gemini dentro de `MPM.Modules.Competidores` — descartado, viola DRY y el principio de un solo punto de configuración de IA.

## R3 — Extracción del "Cuadro de Ofertas" — mecanismo del scraper

**Decision**: Nuevo módulo `tools/scraper-mp/modulos/cuadroOfertas.js`, que para una licitación adjudicada: navega a la ficha pública (`DetailsAcquisition.aspx?qs=<hash>` o vía búsqueda por código externo, confirmado hoy que NO requiere login), hace clic en el ícono "Cuadro de ofertas", y extrae de la tabla resultante: RUT proveedor, nombre proveedor, monto total oferta, estado (Aceptada/Rechazada). Se ejecuta como un paso adicional del ciclo del scraper existente (`agente-mp.js`), acotado a licitaciones en estado "Adjudicada"/"Cerrada" (donde ya existe cuadro de ofertas) que aún no tengan ofertas recolectadas.

**Rationale**: Confirmado en vivo el 2026-07-09 contra una licitación real (`622-12-LP26`) que esta información es pública, no requiere sesión iniciada, y trae exactamente los campos necesarios (proveedor, monto, estado) para las 4 ofertas de ese caso (ENTEL Chile, Growth Partner Network, Noventiq, Tivit Chile).

**Alternatives considered**: Usar un endpoint de API directo en vez de scraping — no se encontró ninguno documentado ni en uso hoy en el proyecto; queda como optimización futura si se descubre uno, no bloquea esta implementación.

**Riesgo abierto (no bloqueante, documentar en quickstart)**: No se validó todavía si el "Cuadro de Ofertas" está disponible para el 100% de las licitaciones adjudicadas o si varía según el tipo/monto de licitación (ej. compras ágiles muy pequeñas). Se recomienda un spike corto de muestreo (ej. 20-30 licitaciones adjudicadas de distintos tipos) antes de comprometerse a recolectar las 126k completas.

## R4 — Búsqueda de competidor por nombre con variaciones de formato

**Decision**: Búsqueda case-insensitive con `ILIKE`/ `pg_trgm` (ya usado en el proyecto para búsqueda de licitaciones, ver `V066`/`V093`) sobre el nombre del proveedor tal como viene en el Cuadro de Ofertas, sin intentar normalizar/deduplicar razones sociales distintas del mismo proveedor en esta primera versión.

**Rationale**: Consistente con el mecanismo de búsqueda ya validado y en producción para licitaciones; evita construir un sistema de resolución de entidades (deduplicación de proveedores) que no fue pedido explícitamente y agregaría complejidad no solicitada.

**Alternatives considered**: Tabla de "alias de proveedor" para unificar variantes de nombre — descartado por ahora (sobre-ingeniería sin evidencia de que sea un problema real todavía); se puede agregar después si en la práctica se nota que un mismo competidor aparece fragmentado.

## R5 — Caché de análisis de competidor: clave de cacheo

**Decision**: La clave de caché es `(nombre_competidor_normalizado, fecha_desde, fecha_hasta)` — una consulta con el mismo competidor pero un rango de fechas distinto (aunque se superponga) es un análisis distinto y no reutiliza caché.

**Rationale**: Es la interpretación más simple y predecible de FR-005 ("mismo competidor y rango exactos"); evita la complejidad de "¿qué hago si el rango se solapa parcialmente?" que el spec no pidió resolver.

**Alternatives considered**: Caché por sub-rangos combinables (ej. cachear por mes y componer cualquier rango) — descartado por complejidad innecesaria para el alcance actual (FR-005 solo exige reutilizar ante consulta idéntica).

## R6 — Telegram `callback_query` para el botón "Me interesa"

**Decision**: Extender `TelegramWebhookController.Webhook` para inspeccionar si el update trae `callback_query` (en vez de `message`) — si el `data` del callback tiene el formato `interesa:<licitacionId>`, llamar a `ApiMpService.GetDetalleAsync` para esa licitación y responder vía `sendMessage` (no vía "answerCallbackQuery" solamente, ya que se pidió un mensaje nuevo con el resumen). El botón se agrega al mensaje original vía `reply_markup.inline_keyboard` en el `sendMessage` que ya dispara la alerta (`TelegramNotificationService.EnviarAsync`).

**Rationale**: Reutiliza el mismo endpoint de webhook ya registrado en producción (mismo secret, mismo fail-closed de BUG-009) — no requiere una URL ni configuración de Telegram nueva.

**Alternatives considered**: Webhook nuevo separado para callbacks — descartado, Telegram manda todos los tipos de update al mismo webhook configurado, no hay forma de separar por tipo de update a nivel de Telegram.

## R7 — Canal de correo: reutilización de `IEmailService`

**Decision**: Nuevo `EmailNotificationService` en `MPM.Modules.Alertas` que arma el HTML del correo (mismo contenido informativo que `TelegramNotificationService.FormatearMensaje`, pero en HTML) y llama a `IEmailService.SendEmailAsync(toEmail, subject, htmlBody)` (ya inyectable, definido en `MPM.Shared`, implementado en `SmtpEmailService`).

**Rationale**: Cero trabajo de infraestructura nueva — el servicio ya está probado en producción (Auth lo usa para reset de contraseña).

**Alternatives considered**: Ninguna — la reutilización es directa y no amerita comparar alternativas.
