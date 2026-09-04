# API Specification: Competidores

> Módulo `MPM.Modules.Competidores` — inteligencia de competidores: scraping del
> Cuadro de Ofertas público por competidor, análisis on-demand con IA (cacheado)
> y actividad de mercado (V124). Scraper externo: `tools/scraper-mp-v2`.

## 1. Scope

### Included
- Listado de competidores con resumen de historial
- Detalle de un competidor (licitaciones ganadas/perdidas contra TIVIT)
- Scraping del Cuadro de Ofertas público para un competidor en un rango de fechas
- Análisis con IA sobre el historial de ofertas (resultado cacheado)
- Actividad de mercado: métricas de participación del competidor

### Excluded
- Seguimiento automático periódico por competidor (se dispara on-demand)
- Análisis de ofertas de TIVIT propias (vive en Análisis)

## 2. Endpoints — `[Authorize]`

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/v1/competidores/lista` | Lista de competidores con resumen (montos, victorias) |
| GET | `/api/v1/competidores` | Detalle de competidor por nombre (historial contra TIVIT) |
| POST | `/api/v1/competidores/ofertas` | Scraping del Cuadro de Ofertas del competidor (rango de fechas) |
| POST | `/api/v1/competidores/analisis` | Análisis IA del historial (usa proveedor IA activo; cacheado) |
| GET | `/api/v1/competidores/{nombre}/actividad-mercado` | Actividad de mercado del competidor (V124) |

## 3. Reglas de negocio

- El scraping usa el scraper externo (`tools/scraper-mp-v2`) y persiste en
  tablas de licitaciones/ofertas; los resultados se cachean por rango de fechas.
- El análisis IA es on-demand: si el rango ya fue analizado, devuelve el cache.
- `POST /ofertas` y `POST /analisis` son operaciones largas (timeout cliente 5 min).

## 4. Stored procedures principales

| SP | Descripción |
|----|-------------|
| `usp_ListarCompetidores` (V100) | Ranking de competidores con métricas agregadas |
| SPs de `actividad_mercado` (V124) | Participación, montos y frecuencia por competidor |
