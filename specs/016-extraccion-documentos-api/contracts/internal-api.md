# Contracts: Extracción de Documentos vía API Directa

**Feature**: 016-extraccion-documentos-api | **Date**: 2026-07-01

Este feature no expone endpoints HTTP nuevos al usuario final: es un proceso interno del ciclo de sincronización. Se documentan (a) los contratos internos de servicios C# y (b) el contrato del portal de Mercado Público a **descubrir en el spike** (R1).

## 1. Contrato del portal MP (spike ejecutado 2026-07-06 contra el portal real)

> Ejecutado con `tools/scraper-mp/spike-adjuntos.js`, `spike-ficha-directa.js` y
> `spike-validacion-http-pura.js`, credenciales reales, licitación de prueba `2153-41-LP26`.

### 1.1 Sesión (cookies)
- Origen: login Keycloak/Heimdall (reutilizado desde Node, R2) — **confirmado funcional**, 27 cookies del dominio `mercadopublico.cl` exportadas correctamente.
- Vigencia observada: no medida en este spike (una sola ejecución); se mantiene el supuesto de 6h de `research.md` R2 hasta tener datos de expiración real.

### 1.2 Ficha por código (nuevo paso, no contemplado en el diseño original)
- URL: `https://www.mercadopublico.cl/Procurement/Modules/RFB/DetailsAcquisition.aspx?idlicitacion={codigo}` — **confirmado**: funciona con solo el código externo (no requiere el token `qs` opaco que usa la búsqueda del portal), redirige automáticamente.
- El HTML de la ficha contiene el botón `#imgAdjuntos` con `onclick="open('../Attachment/ViewAttachment.aspx?enc={token}', 'MercadoPublico', ...)"` — el token `enc` es único por licitación/render y se extrae de ahí (no es derivable de otra forma).

### 1.3 GET listado de adjuntos — ⚠️ BLOQUEADO POR reCAPTCHA ENTERPRISE
- URL: `https://www.mercadopublico.cl/Procurement/Modules/Attachment/ViewAttachment.aspx?enc={token}` — **confirmada**, pero:
- **Hallazgo crítico**: la respuesta HTTP no es el listado — es una página que ejecuta `grecaptcha.enterprise.execute(...)` (reCAPTCHA Enterprise, invisible/v3-style) client-side, hace un POST a `ViewAttachment.aspx?ajax=1` con el token de reCAPTCHA + el `enc`, y **recién si el score pasa**, redirige vía `window.location.href` (JavaScript, no HTTP redirect) a `ViewAttachmentLC.aspx?enc=...` con el listado real.
- Un `HttpClient`/`fetch()` sin motor JS **no puede completar `grecaptcha.enterprise.execute()`** — no hay forma de obtener un token de reCAPTCHA válido sin ejecutar el JS real de Google en un contexto de navegador. Esto es una barrera de la plataforma, no un detalle de implementación pendiente.
- Cuando SÍ se navega con un navegador real (Playwright, como hace `adjuntos.js` hoy), el challenge se resuelve de forma transparente/invisible y se llega directo a `ViewAttachmentLC.aspx` con el listado — confirmado en `spike-adjuntos.js`.
- Estructura del listado real (cuando se accede vía navegador): tabla `#DWNL_grdId`, filas con 7 `<td>` (índice, nombre, tipo, descripción, tamaño, fecha, botón), campos ocultos observados: `__EVENTTARGET`, `__EVENTARGUMENT`, `__VIEWSTATE`, `__VIEWSTATEGENERATOR` (sin `__EVENTVALIDATION` en esta instancia — el parser lo trata como opcional).

### 1.4 POST descarga (postback WebForms) — mecanismo confirmado, alcance bloqueado por 1.3
- Los botones de la grilla son `<input type="image">` — al hacer click, el navegador envía `{name}.x`/`{name}.y` (coordenadas), **no** el mecanismo clásico `__EVENTTARGET` de LinkButton. Confirmado con un botón real: `DWNL$grdId$ctl02$search.x` / `.y`.
- Body observado: todos los campos ocultos de la página + `{botonName}.x` + `{botonName}.y` (+ un campo adicional `DWNL$ctl10` visto en el spike, cuyo origen no se determinó — probablemente otro hidden field de la página, capturado igual por la estrategia de "capturar todos los hidden inputs" de `WebFormsParser`).
- **No se pudo probar el POST real** porque para llegar aquí hay que pasar el reCAPTCHA de 1.3, y eso solo lo logra un navegador.

## Conclusión del spike (2026-07-06)

**016 NO logra su objetivo original de eliminar el uso de navegador para la descarga de adjuntos.** El paso del listado de adjuntos está protegido por reCAPTCHA Enterprise, resoluble únicamente por un navegador real ejecutando JavaScript. `AdjuntosHttpExtractor` (implementado en `MPM.Modules.Licitaciones`) sigue siendo código correcto y útil para todo lo que SÍ es HTTP puro (ficha por código, extracción del token `enc`), pero falla de forma controlada y documentada en el paso del reCAPTCHA, cayendo al fallback de navegador vía `DocumentExtractionService`.

**Implicación para `002-fase5-deploy-gcp` (migración a Cloud Run)**: el argumento de "016 reduce el uso de Chromium a una renovación de sesión cada 6h" ya no es válido — Chromium sigue siendo necesario **por cada licitación** para pasar el reCAPTCHA, igual que hoy. `scraper-job` como ejecución corta sin navegador persistente **no es viable con el diseño actual**. Ver la actualización correspondiente en `specs/002-fase5-deploy-gcp/research.md`.

## 2. Contratos internos de servicios (C#)

### `MpSessionProvider`
```csharp
Task<MpSession> ObtenerSesionAsync(bool forzarRenovacion = false, CancellationToken ct = default);
// MpSession { CookieContainer Cookies; DateTime ObtenidaEn; }
// - Lee cookies de Redis; si faltan/expiraron o forzarRenovacion, invoca el login Node y recachea.
// - Renovación protegida por lock para evitar logins concurrentes.
```

### `WebFormsParser`
```csharp
AdjuntosListado Parse(string html);
// AdjuntosListado { List<AdjuntoFila> Filas; WebFormsState State; }
// AdjuntoFila { string Nombre; string Tipo; string Tamanio; string BotonId; bool EsActa; }
// WebFormsState { string ViewState; string ViewStateGenerator; string EventValidation; }
```

### `AdjuntosHttpExtractor`
```csharp
Task<ResultadoExtraccion> ExtraerAsync(LicitacionRef lic, CancellationToken ct = default);
// 1) GET listado con cookies de MpSessionProvider  2) Parse  3) POST postback por documento objetivo
// 4) guarda vía IStorageService  5) devuelve ResultadoExtraccion
// Lanza/ް señala 401/403 para que el orquestador renueve sesión.
```

### `DocumentExtractionService` (orquestador)
```csharp
Task<ResultadoExtraccion> ExtraerAsync(LicitacionRef lic, CancellationToken ct = default);
// Según Extraccion:Modo:
//   solo_navegador       → solo scraper Node (comportamiento actual)
//   paralelo             → ambos, registra ambos, persiste el del navegador
//   directo_con_fallback → directo; si falla, scraper Node; registra ambos intentos
// Idempotencia: si la licitación ya tiene adjuntos (usp_Adjuntos_ExistePorLicitacion) no reprocesa (salvo paralelo).
```

### `ResultadoExtraccion` (DTO)
```csharp
record ResultadoExtraccion(
    string Metodo,            // "directo" | "navegador"
    string Estado,            // "exito" | "fallo" | "sin_adjuntos"
    int DocumentosObtenidos,
    bool ActaObtenida,
    string? Error,
    long DuracionMs,
    bool EsFallback);
```

## 3. Persistencia (SPs — ver data-model.md)

- `usp_ExtraccionLog_Registrar(...)` — un registro por intento.
- `usp_ExtraccionLog_ResumenPeriodo(desde, hasta)` — comparación directo vs. navegador (US3).
- `usp_Adjuntos_ExistePorLicitacion(licitacion_id)` — idempotencia.

## 4. Sin cambios de contrato HTTP público

- `POST /api/v1/licitaciones/sync` y `GET /api/v1/licitaciones` no cambian de firma.
- El pipeline de análisis (crear workspace → subir documento → analizar) sigue igual: solo cambia **cómo** llega el PDF al storage, no qué se hace después.
