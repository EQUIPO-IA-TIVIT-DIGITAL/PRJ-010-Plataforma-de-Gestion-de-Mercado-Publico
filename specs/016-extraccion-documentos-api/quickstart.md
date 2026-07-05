# Quickstart: Validación — Extracción de Documentos vía API Directa

**Feature**: 016-extraccion-documentos-api | **Date**: 2026-07-01

Guía de validación end-to-end. Diseño en [plan.md](plan.md) / [research.md](research.md); contratos en [contracts/internal-api.md](contracts/internal-api.md).

## Prerequisitos

```bash
docker compose up --build   # API :5001, DB :5433, Redis
```

- `.env` con `MP_RUT` / `MP_PASSWORD` válidos (sesión Keycloak) y `GEMINI_API_KEY`.
- Migración `V077` aplicada automáticamente al arrancar la API.
- Un conjunto de 3-5 licitaciones **adjudicadas conocidas** que hoy descargan Acta por el flujo navegador (baseline de comparación).

## Fase 0 — Spike de descubrimiento (bloqueante, antes de implementar)

1. Con Playwright (o DevTools) capturar el tráfico (HAR/trace) al abrir adjuntos y descargar el Acta de una licitación conocida.
2. Completar la sección 1 de `contracts/internal-api.md` con: URL de listado, cookies imprescindibles, campos `__VIEWSTATE`/`__EVENTVALIDATION`, y el request de descarga (postback).
3. **Criterio de avance**: se pudo reproducir manualmente (curl/Postman) la descarga del Acta usando solo HTTP + cookies. Si NO se logra, documentar el bloqueo y mantener `Extraccion:Modo=solo_navegador`.

## Escenarios de validación

### 1. Descarga directa sin navegador (US1)

1. Configurar `Extraccion:Modo=directo_con_fallback`.
2. Ejecutar la extracción sobre una licitación conocida (vía ciclo de sync o disparo manual).
3. **Esperado**: el Acta y los adjuntos quedan en storage con `licitaciones_adjuntos.metodo_extraccion='directo'`; `extraccion_documentos_log` registra `metodo='directo', estado='exito'`; durante la descarga NO se abrió ningún proceso de navegador (verificar ausencia de proceso Node/Chromium y en logs).
4. Comparar el PDF obtenido con el baseline del navegador → mismo documento.

### 2. Fallback automático (US2)

1. Forzar un fallo del directo (p.ej. invalidar temporalmente las cookies en Redis o apuntar a una licitación cuyo listado el parser no reconozca).
2. **Esperado**: el sistema cae automáticamente al scraper navegador para esa licitación; `extraccion_documentos_log` muestra `directo/fallo` **y** `navegador/exito (es_fallback=true)`; el documento queda igualmente descargado.
3. Forzar fallo de ambos → un único registro de **fallo real** consultable (`directo/fallo` + `navegador/fallo`), sin excepción silenciosa (SC-004).

### 3. Validación en paralelo y cobertura (US3)

1. Configurar `Extraccion:Modo=paralelo` y correr un lote.
2. Consultar `usp_ExtraccionLog_ResumenPeriodo(desde, hasta)`.
3. **Esperado**: por licitación existen ambos registros (directo y navegador) y se puede comparar cobertura y si obtuvieron los mismos documentos; el directo iguala o supera al navegador (SC-003, SC-005) antes de promover a `directo_con_fallback`.

### 4. Rendimiento y recursos (SC-001/SC-002)

1. Medir `duracion_ms` promedio de `metodo='directo'` vs. `metodo='navegador'` en `extraccion_documentos_log`.
2. **Esperado**: el directo reduce ≥70% el tiempo por licitación y no levanta un navegador por licitación (solo 1 login cada `SesionTtlHoras`).

### 5. Edge cases

- Licitación sin adjuntos → `estado='sin_adjuntos'` (no cuenta como fallo).
- Cookies expiradas a mitad de lote → se renueva la sesión una vez y continúa (verificar en logs 1 solo re-login).
- Licitación ya procesada → no se vuelve a descargar (idempotencia, salvo modo `paralelo`).

## Tests automatizados

```bash
dotnet test tests/MPM.Modules.Licitaciones.Tests   # parser WebForms, fallback, TTL de sesión (HttpClient mockeado)
dotnet test MPM.sln
```

El acceso real al portal (spike y escenarios 1-4) se valida manualmente con el stack levantado y credenciales — no corre en CI.

## Criterios de cierre (mapa a Success Criteria)

| Escenario | SC |
|-----------|----|
| 1 | SC-001 (parcial), SC-002 |
| 2 | SC-004 |
| 3 | SC-003, SC-005 |
| 4 | SC-001, SC-002 |
