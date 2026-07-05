# Research: Fase 2 — Automatización del Scraping

**Feature**: MPM CU010 — Fase 2: Automatización del Pipeline de Análisis
**Date**: 2026-06-23
**Spec**: [spec.md](./spec.md)

---

## 1. Estado actual del pipeline (análisis del código)

### ✅ Ya implementado

| Componente | Archivo | Estado |
|---|---|---|
| Playwright scraper (login, búsqueda, descarga) | `tools/scraper-mp/agente-mp.js` | Funcional |
| Búsqueda por "adjudicada" + "en las que ofertamos" | `tools/scraper-mp/modulos/buscar.js` | Funcional |
| Descarga de "Acta de Evaluación" (por tipo) | `tools/scraper-mp/modulos/adjuntos.js` | Funcional |
| Identificación del acta por columna tipo | `adjuntos.js` línea 70: `esActa: tipo === 'Acta de Evaluación'` | Funcional |
| Pipeline IA: workspace → PDF upload → análisis | `tools/scraper-mp/modulos/api-client.js` | Funcional (condicionado a `MP_ANALISIS_IA=true`) |
| Registro de adjuntos en DB | `usp_Licitaciones_Adjuntos_Upsert` → V063 | Funcional |
| Control de sync log | `usp_ScraperSync_Start/End/GetLastCompleted` → V063 | Funcional |
| BackgroundService .NET que invoca el scraper | `ScraperBackgroundService.cs` | Implementado, deshabilitado |
| Notificaciones al completar scraping | `ScraperBackgroundService.cs` → `NotificacionesService` | Funcional |
| Análisis background con Gemini | `AnalisisBackgroundService.cs` | Funcional |
| Modo incremental (desde última sync) | `agente-mp.js` + `usp_ScraperSync_GetLastCompleted` | Funcional |
| Paginación de resultados | `buscar.js` líneas 253–303 (hasta 50 páginas) | Funcional |

### ❌ Bloqueante crítico

**El Dockerfile del API (`src/MPM.Api/Dockerfile`) no instala Node.js ni copia el directorio `tools/`.**

```dockerfile
# Imagen base: solo runtime .NET, sin Node.js
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# El ScraperBackgroundService intenta ejecutar `node agente-mp.js`
# → Falla con "node: not found"
```

### ❌ Variables de entorno faltantes

Las siguientes variables son necesarias para el pipeline pero **no están en `docker-compose.yml`**:

| Variable | Uso | Default actual |
|---|---|---|
| `MP_RUT` | RUT de TIVIT para login en Mercado Público | No configurado → scraper no puede hacer login |
| `MP_PASSWORD` | Contraseña de Mercado Público | No configurado |
| `SCRAPER_ENABLED` | Habilitar el ScraperBackgroundService | `false` (no activo) |
| `MP_ANALISIS_IA` | Activar pipeline IA en el scraper | `false` (no crea workspaces) |
| `API_BASE_URL` | URL de la MPM API para el scraper JS | `http://localhost:5001` → en Docker debe ser `http://api:80` |
| `MP_FECHA_DESDE` | Fecha inicial de búsqueda | `01-01-2026` en `buscar.js` |
| `MP_HEADLESS` | Modo headless del browser | `false` (falla en contenedor) |
| `SCRAPER_INTERVAL_HOURS` | Intervalo de ejecución | 12 horas |

---

## 2. Decisiones de diseño

### Decisión 1: ¿Dónde corre el scraper en Docker?

**Problema**: El `ScraperBackgroundService.cs` ejecuta `node agente-mp.js` como proceso hijo. Para eso, el contenedor del API necesita Node.js instalado.

**Alternativas evaluadas:**

| Opción | Ventaja | Desventaja |
|---|---|---|
| **A: Contenedor separado `mpm-scraper`** | Imagen API más pequeña; separación de responsabilidades | Requiere duplicar env vars; lógica de notificación queda en API igual |
| **B: Añadir Node.js al Dockerfile del API** | Un solo contenedor; mantiene ScraperBackgroundService intacto | Imagen más pesada (~500MB extra con Playwright browsers); acoplamiento de runtime |
| **C: Deshabilitar ScraperBackgroundService; correr scraper manualmente** | Cero cambios en Docker | No es automático; no cumple el objetivo |

**Decisión: Opción B — Añadir Node.js al Dockerfile del API.**

**Rationale:**
- La Fase 2 es urgente (demo del jueves); la opción B requiere menos cambios.
- El `ScraperBackgroundService.cs` ya está integrado con el sistema de notificaciones y el sistema de configuración de .NET.
- El scraper no tiene estado compartido con el API; solo los llama via HTTP.
- La imagen más pesada es aceptable para el ambiente de desarrollo; la Fase 5 (GCP) optimizará el despliegue.

**Rationale alternativa evaluada para Opción A**: Aunque arquitecturalmente más limpia, implica también modificar docker-compose para que el scraper tenga acceso a las vars de entorno JWT, configurar comunicación interna, y complicar el debug para la demo del jueves.

---

### Decisión 2: ¿Qué versión de Node.js usar?

`tools/scraper-mp/` usa ES modules (`import/export`). Playwright `@playwright/test` v1.60 requiere Node.js >= 18.

**Decisión**: Node.js 20 LTS (imagen `node:20-slim`).

---

### Decisión 3: ¿Cómo maneja el scraper la primera corrida histórica (2025)?

El `agente-mp.js` en modo incremental lee `usp_ScraperSync_GetLastCompleted`. Si no hay sync previo, retorna `'2000-01-01'` y empieza desde esa fecha.

**Para la primera corrida real**, configurar `MP_FECHA_DESDE=01-01-2025` para capturar licitaciones desde 2025 como pide Francisco.

**Riesgo**: Con 18 meses de licitaciones adjudicadas, la primera corrida puede tomar varias horas. Playwright es síncrono por licitación.

**Mitigación**: Correr la primera vez manualmente con `--incremental false` y controlar el rango. Para la demo del jueves, usar un subconjunto de datos ya conocidos.

---

### Decisión 4: ¿Cómo se pasan las credenciales de Mercado Público?

Las credenciales (`MP_RUT`, `MP_PASSWORD`) son secretos operativos que **no deben estar en el repo**.

**Decisión**: Agregar al `.env` local (ya en `.gitignore`) y documentar en `.env.example`.

---

## 3. Stored Procedures verificados

Los siguientes SPs están confirmados en las migraciones:

| SP | Migración |
|---|---|
| `usp_Licitacion_UpsertFromScraper` | V063 |
| `usp_Licitaciones_Adjuntos_Upsert` | V063 |
| `usp_ScraperSync_Start` | V063 |
| `usp_ScraperSync_End` | V069 (fix de tipos) |
| `usp_ScraperSync_GetLastCompleted` | V063 |
| `usp_Licitacion_YaExistePorCodigo` | V063 |

Todos los SPs requeridos están presentes. No se necesitan nuevas migraciones para Fase 2.

---

## 4. Dependencias de Node.js del scraper

```json
// tools/scraper-mp/package.json (inferido del código)
{
  "dependencies": {
    "playwright": "^1.x",
    "dotenv": "^16.x",
    "pg": "^8.x"
  }
}
```

Playwright requiere browsers instalados. En el Dockerfile se usará `npx playwright install chromium --with-deps`.

---

## 5. Ruta del script en Docker

El `ScraperBackgroundService.cs` busca el script en:
```csharp
private const string ScraperScriptPath = "tools/scraper-mp/agente-mp.js";
// Resuelve: AppContext.BaseDirectory + "../../../../.." + script
```

En producción Docker (`/app`), la ruta resuelta sería `/agente-mp.js` (incorrecto). El Dockerfile debe copiar `tools/` a una ruta predecible y la var `SCRAPER_SCRIPT_PATH` (o una nueva var) debe sobrescribir la constante.

**Solución**: Agregar variable de entorno `Scraper__ScriptPath` y leerla en `ScraperBackgroundService.cs` para sobrescribir el path hardcodeado.
