# Research: Extracción de Documentos vía API Directa

**Feature**: 016-extraccion-documentos-api | **Date**: 2026-07-01

Decisiones técnicas para reemplazar la descarga por navegador (`tools/scraper-mp/modulos/adjuntos.js`) por HTTP directo. Contexto del flujo actual (verificado en código):

- **Login** (`modulos/login.js`): navega a `mercadopublico.cl/Home` → "Iniciar Sesión" → redirige a Keycloak/Heimdall (`/auth`) → tab "Extranjero" → RUT/clave (`#username-re`, `#password-re`, submit `#kc-login-re`) → selección de organización TIVIT → vuelve a mercadopublico.cl con cookies de sesión.
- **Adjuntos** (`modulos/adjuntos.js`): en la ficha, click en `#imgAdjuntos` abre un **popup ASP.NET WebForms** con tabla `#DWNL_grdId`; cada fila tiene un botón `input[type=image]` cuyo click dispara un **postback** (`__EVENTTARGET`/`__VIEWSTATE`) que devuelve el archivo como descarga.
- **Integración**: el scraper llama a la API MPM (`crearWorkspaceAnalisis` → `subirDocumento` → `iniciarAnalisis`) con un service JWT.

## R1 — Descubrimiento de endpoints del portal (SPIKE, bloqueante)

**Decision**: Antes de implementar, ejecutar un spike que capture el tráfico real de red del portal (con Playwright tracing / HAR o DevTools) sobre 2-3 licitaciones adjudicadas conocidas, para documentar exactamente:
1. URL de la ventana/listado de adjuntos (la que hoy abre `#imgAdjuntos`) y sus parámetros (código de licitación / `enc`/query).
2. Campos ocultos del formulario WebForms del listado: `__VIEWSTATE`, `__VIEWSTATEGENERATOR`, `__EVENTVALIDATION`.
3. El request exacto de descarga: método (POST postback), `__EVENTTARGET` del botón por fila, headers (Referer, cookies) y forma de la respuesta (Content-Disposition, Content-Type).
4. Qué cookies de sesión son imprescindibles (nombres) y su vigencia.

El resultado del spike se documenta en `contracts/internal-api.md` y **condiciona** el resto de tareas. Si el spike revela que el listado/descarga requiere ejecución de JavaScript no reproducible por HTTP, se re-evalúa el enfoque (el fallback a navegador de US2 cubre ese riesgo).

**Rationale**: el portal no tiene API documentada de adjuntos (confirmado); la única fuente de verdad es su propio tráfico. Hacer el spike primero evita construir sobre supuestos.

**Alternatives considered**: implementar a ciegas replicando lo que "debería" ser — descartado por riesgo. Pedir API oficial a ChileCompra — fuera de nuestro control y plazo.

## R2 — Autenticación: sesión cacheada reutilizando el login Node

**Decision**: `MpSessionProvider` obtiene las cookies de sesión invocando el login ya existente (`tools/scraper-mp/modulos/login.js`) mediante un pequeño comando Node que, tras autenticar, exporta el `storageState`/cookies como JSON a stdout. El proveedor cachea esas cookies en **Redis** con TTL configurable (`Extraccion:SesionTtlHoras`, default 6h) y las renueva cuando expiran o cuando una descarga responde 401/403. Toda la descarga de adjuntos usa esas cookies vía `HttpClient` — **sin navegador por licitación**.

**Rationale**: el login Keycloak (auth code + CSRF + selección de organización) es la parte más frágil y costosa de reimplementar en C#. Reusar el login Node —que ya funciona en producción— y cachear el resultado da el 100% de descarga directa con el mínimo riesgo, y reduce el uso de navegador de "1 por licitación" a "1 cada 6h". Encaja con que el scraper Node se conserva de todos modos como fallback.

**Alternatives considered**:
- **Keycloak en C# puro** (opción "HTTP puro extremo a extremo"): elimina Node del login, pero multiplica el riesgo y esfuerzo ante cambios del IdP; se puede abordar en una fase posterior si se decide retirar Node por completo.
- **Playwright .NET** para el login: agrega una dependencia pesada (~navegadores) al backend y duplica lógica de login ya resuelta en Node.

## R3 — Descarga WebForms por HTTP directo

**Decision**: `AdjuntosHttpExtractor` con un `HttpClient` que comparte `CookieContainer` (cookies de R2):
1. **GET** de la página de listado de adjuntos de la licitación → `WebFormsParser` (AngleSharp) extrae filas de `#DWNL_grdId` (nombre, tipo, tamaño, id del botón) y los campos ocultos `__VIEWSTATE`/`__VIEWSTATEGENERATOR`/`__EVENTVALIDATION`.
2. Por cada documento objetivo (Acta + bases/anexos/resoluciones, FR-007), **POST** del postback (`__EVENTTARGET` = id del botón de la fila, más los campos de ViewState) al endpoint del listado.
3. La respuesta (stream PDF con `Content-Disposition`) se guarda vía `IStorageService` y se registra en `licitaciones_adjuntos` con `metodo_extraccion = 'directo'`.

**Rationale**: replica exactamente lo que el navegador hacía (postback + cookies) pero sin renderizar. AngleSharp maneja el HTML real de WebForms de forma robusta.

**Alternatives considered**: `HttpClient` + regex para ViewState — frágil; AngleSharp es estándar para esto. Descargar todo en paralelo agresivo — se limita la concurrencia para no gatillar anti-automatización (R6).

## R4 — Orquestación y fallback automático (US2)

**Decision**: `DocumentExtractionService.ExtraerAsync(licitacion)`:
1. Intenta `AdjuntosHttpExtractor` (directo).
2. Si lanza excepción o no obtiene el Acta esperada, invoca el flujo Node/Playwright actual para esa licitación (proceso externo, como hoy).
3. Registra el resultado en `extraccion_documentos_log` (`metodo`, `estado`, `documentos`, `error`, `duracion_ms`). Solo se marca fallo real si **ambos** fallan (FR-006).
`SyncEngineService` llama a este servicio en su ciclo, en lugar de disparar directamente el scraper.

**Rationale**: cumple US2 sin perder cobertura durante la maduración del flujo directo. El registro por método habilita la comparación de US3/FR-008.

**Alternatives considered**: sin fallback (reemplazo duro) — descartado por el riesgo de huecos silenciosos que el propio spec prohíbe.

## R5 — Validación en paralelo y adopción (US3/FR-008)

**Decision**: un flag `Extraccion:Modo` con tres valores:
- `solo_navegador` (comportamiento actual, default inicial),
- `paralelo` (ejecuta ambos y registra ambos resultados para comparar, sin cambiar qué se persiste — el navegador sigue siendo la fuente),
- `directo_con_fallback` (el directo es primario, navegador es respaldo).
La comparación se hace consultando `extraccion_documentos_log` (cobertura y coincidencia de documentos por licitación). Se pasa a `directo_con_fallback` cuando el directo iguale/supare la cobertura del navegador (SC-003/SC-005).

**Rationale**: transición gradual y medible, exactamente como pide FR-008; el flag permite volver atrás sin desplegar.

**Alternatives considered**: switch binario — no permite el período de validación exigido.

## R6 — Anti-automatización y robustez (edge cases del spec)

**Decision**: el `HttpClient` usa un `User-Agent` realista y los headers `Referer` observados en el spike; concurrencia limitada (semáforo, `Extraccion:MaxConcurrencia` default 2) y un pequeño delay entre licitaciones; ante 401/403 se renueva la sesión (R2) una vez y se reintenta; ante 429/bloqueo se hace backoff y, si persiste, se cae al fallback navegador. Licitación sin adjuntos publicados = éxito con `documentos = 0` (no es fallo). Cambios de estructura del portal = el parser falla → fallback → registro para alertar.

**Rationale**: cubre explícitamente los edge cases enumerados en el spec (token que expira, sin adjuntos, anti-bot, cambio de estructura).

## R7 — Idempotencia y reprocesamiento

**Decision**: antes de descargar, `DocumentExtractionService` verifica en `licitaciones_adjuntos` si la licitación ya tiene sus documentos (por `record_status=1`); si ya están, no reprocesa salvo modo `paralelo` (que compara sin sobrescribir). Las licitaciones ya procesadas por el navegador NO se reprocesan automáticamente (respuesta al edge case correspondiente).

**Rationale**: evita descargas y tráfico redundante; respeta lo ya obtenido.

**Alternatives considered**: reprocesar todo con el flujo nuevo — tráfico innecesario y riesgo anti-bot sin beneficio.
