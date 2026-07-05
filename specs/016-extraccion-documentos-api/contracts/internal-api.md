# Contracts: Extracción de Documentos vía API Directa

**Feature**: 016-extraccion-documentos-api | **Date**: 2026-07-01

Este feature no expone endpoints HTTP nuevos al usuario final: es un proceso interno del ciclo de sincronización. Se documentan (a) los contratos internos de servicios C# y (b) el contrato del portal de Mercado Público a **descubrir en el spike** (R1).

## 1. Contrato del portal MP (a completar tras el spike R1)

> ⚠️ Los valores exactos se llenan al ejecutar el spike de descubrimiento (tarea T004). Estructura esperada según el flujo actual de `adjuntos.js`.

### 1.1 Sesión (cookies)
- Origen: login Keycloak/Heimdall (reutilizado desde Node, R2).
- Cookies imprescindibles: `[por descubrir — p.ej. .AspNet.ApplicationCookie / cookies de sesión de mercadopublico.cl]`.
- Vigencia observada: `[por descubrir]`.

### 1.2 GET listado de adjuntos
- URL: `[por descubrir — la que abre #imgAdjuntos]` (probable `https://www.mercadopublico.cl/...Adjuntos...?enc=<token licitación>`).
- Headers requeridos: `Cookie`, `Referer` (ficha de la licitación), `User-Agent` realista.
- Respuesta: HTML WebForms con tabla `#DWNL_grdId` (filas: nombre, tipo, descripción, tamaño, fecha, botón `input[type=image]`) y campos ocultos `__VIEWSTATE`, `__VIEWSTATEGENERATOR`, `__EVENTVALIDATION`.

### 1.3 POST descarga (postback WebForms)
- URL: la misma del listado (postback).
- Body (form-urlencoded): `__EVENTTARGET=<id del botón de la fila>`, `__EVENTARGUMENT=`, `__VIEWSTATE=...`, `__VIEWSTATEGENERATOR=...`, `__EVENTVALIDATION=...` (+ cualquier campo adicional que revele el spike).
- Respuesta: stream binario con `Content-Disposition: attachment; filename=...` y `Content-Type: application/pdf` (u otro).

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
