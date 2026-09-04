# Tasks: Sprint 2 — ChileCompra Histórico + YoY + Competidores

**Spec:** `docs/api-first/sprint2-plan.md`  
**HUs:** MPM-S2-001 a MPM-S2-006  
**Sprint:** 2  
**Total estimado:** 10 días (2 devs + 1 DevOps parcial)

---

## Backend — Base de Datos y Servicios

### T1 — Migración SQL: Tablas ChileCompra histórico + competidores
**Depende de:** none  
**Estimación:** 2h  
**Work:**
- Crear `docs/migrations/V160__ChileCompra_Historico_Tablas.sql` (basado en spec)
- Tablas: `chilecompra_historico`, `chilecompra_convenios_marco`, `chilecompra_competidores`
- Índices + constraints + seed competidores (5 RUTs)
- Ejecutar en Docker compose local: `docker compose exec db psql -U $DB_USER -d $DB_NAME -f /migrations/V160...sql`
**Verify:**
```sql
\d chilecompra_historico
\d chilecompra_convenios_marco
\d chilecompra_competidores
SELECT * FROM chilecompra_competidores; -- 5 filas
```

---

### T2 — Config + HTTP Client ChileCompra (Shared)
**Depende de:** T1  
**Estimación:** 2h  
**Work:**
- `src/MPM.Shared/Services/ChileCompraApiClient.cs`: HttpClient tipado con:
  - BaseAddress desde env `CHILECOMPRA_BASE_URL`
  - Timeout 30s, User-Agent `TIVIT-MPM/1.0`
  - Polly: retry 3x, backoff exponencial, circuit breaker
  - Métodos: `GetKpiAsync(rut, anio)`, `GetModalityAsync(rut, anio)`, `GetTradedAsync(rut, anio, modalidadId)`, `GetDetailAsync(rut, anio, modalidadId, sectorId)`, `GetFrameworksAsync(anio)`, `GetConceptsAsync()`
- Registro en `Program.cs` DI: `services.AddHttpClient<IChileCompraApiClient, ChileCompraApiClient>()`
- Tests unitarios: mock HttpMessageHandler, verificar retry + URL construction
**Verify:**
```bash
dotnet test tests/MPM.Shared.Tests --filter "ChileCompraApiClient"
# Tests: GetKpiAsync_BuildsCorrectUrl, RetryOnTransientError, CircuitBreakerOpens
```

---

### T3 — Servicio Ingesta TIVIT (MPM-S2-001)
**Depende de:** T2  
**Estimación:** 4h  
**Work:**
- `src/MPM.Modules.Analisis/Services/ChileCompraIngestionService.cs`:
  - `IngestTivitAsync(anio?, ct)` → itera años 2020..actual, llama 4 endpoints, upsert en `chilecompra_historico`
  - `IngestFrameworksAsync(anio)` → `chilecompra_convenios_marco` + matching TI (ILIKE cloud/seguridad/datacenter/ciber)
  - Upsert: `ON CONFLICT (rut_empresa, anio, mes, modalidad_id, sector_id) DO UPDATE SET monto_total=EXCLUDED.monto_total, raw_json=EXCLUDED.raw_json, ingested_at=NOW()`
  - Logging estructurado: `LogInformation("Ingesta TIVIT {Anio} completada: {Count} filas")`
- BackgroundService `ChileCompraIngestionWorker`: cron desde env `CHILECOMPRA_JOB_CRON` (default día 20 06:00)
- Endpoint admin: `POST /api/v1/admin/chilecompra/ingest?anio=2025` (solo Admin/SuperAdmin)
**Verify:**
```bash
# Manual trigger
curl -X POST "http://localhost:5001/api/v1/admin/chilecompra/ingest?anio=2025" -H "Authorization: Bearer <admin_token>"
# Verificar BD
SELECT anio, modalidad_id, COUNT(*), SUM(monto_total) FROM chilecompra_historico WHERE rut_empresa='76.130.712-6' GROUP BY anio, modalidad_id;
# Debe mostrar 2025: modalidad 1 (Licitación) + 2 (CM) con montos ~4.4B + 2.8B
```

---

### T4 — Endpoints Dashboard TIVIT (MPM-S2-001 + MPM-S2-005)
**Depende de:** T3  
**Estimación:** 3h  
**Work:**
- `src/MPM.Modules.Analisis/Controllers/DashboardChileCompraController.cs`:
  - `GET /api/v1/dashboard/chilecompra/tivit?anio=2025` → KPIs + desglose modalidad + % CM
  - `GET /api/v1/dashboard/chilecompra/tivit/serie-temporal` → 84 meses (2020-2026) con monto/mes
  - Cache Redis 1h: `IDistributedCache` key `dashboard:chilecompra:tivit:{anio}`
- DTOs: `ChileCompraKpiDto`, `ChileCompraSerieTemporalDto`, `ChileCompraModalityDto`
- Tests unitarios: mock service, verificar cache hit/miss, response shape
**Verify:**
```bash
curl "http://localhost:5001/api/v1/dashboard/chilecompra/tivit?anio=2025"
# 200: { totalCLP: 7210000000, convenioMarcoCLP: 2800000000, porcentajeCM: 38.8, anio: 2025, modalidades: [...] }
curl "http://localhost:5001/api/v1/dashboard/chilecompra/tivit/serie-temporal"
# 200: 84 items [{ anio:2020, mes:1, monto:50000000 }, ...]
```

---

### T5 — Competidores: Tabla + Ingesta Multi-RUT (MPM-S2-003)
**Depende de:** T3  
**Estimación:** 4h  
**Work:**
- Extender `ChileCompraIngestionService`:
  - `IngestCompetidoresAsync(anio?, ct)` → lee `chilecompra_competidores WHERE activo=true ORDER BY prioridad`
  - Para cada RUT: llama mismos 4 endpoints + `GetKpiAsync`, rate limit 1req/s (`await Task.Delay(1000)`)
  - Fallo individual: log error + continuar; métricas `competidor_fallido` + `competidor_exitoso`
- Endpoints:
  - `GET /api/v1/dashboard/chilecompra/competidores?anio=2025` → lista ranking (rut, nombre, total, cm, yoy, ranking)
  - `GET /api/v1/dashboard/chilecompra/competidor/{rut}?anio=2025` → detalle empresa
- Job programado: mismo worker, después de TIVIT
**Verify:**
```bash
curl "http://localhost:5001/api/v1/dashboard/chilecompra/competidores?anio=2025"
# 200: [{ rut: "90.123.456-7", nombre: "SONDA", totalCLP: 12000000000, cmCLP: 3000000000, yoy: 15.2, ranking: 1 }, ...]
curl "http://localhost:5001/api/v1/dashboard/chilecompra/competidor/90.123.456-7?anio=2025"
# 200: { rut: "...", nombre: "SONDA", kpis: {...}, serieTemporal: [...], modalidades: [...] }
```

---

### T6 — Endpoint Consolidado Dashboard Ejecutivo (MPM-S2-005)
**Depende de:** T4, T5  
**Estimación:** 2h  
**Work:**
- `GET /api/v1/dashboard/ejecutivo/chilecompra?anio=2025` → respuesta única:
  ```json
  {
    "kpis": { "totalActual": 7210000000, "totalAnterior": 7210000000, "yoyPorcentaje": 0, "cmActual": 2800000000, "cmPorcentaje": 38.8 },
    "conveniosMarco": [{ codigo: "CM-2025-123", nombre: "Cloud Services", monto: 626000000, esTi: true }, ...],
    "competidores": [{ rut: "...", nombre: "SONDA", total: 12000000000, yoy: 15.2, ranking: 1 }, ...],
    "serieTemporal": [{ anio: 2020, mes: 1, monto: 50000000 }, ...]
  }
  ```
- Cache Redis 1h, invalidación en ingesta exitosa
- Tests: integration test con BD real, verificar response completo
**Verify:**
```bash
curl "http://localhost:5001/api/v1/dashboard/ejecutivo/chilecompra?anio=2025"
# 200: JSON completo con 4 secciones, < 500ms cached
```

---

## Frontend — Dashboard Ejecutivo

### T7 — Types + Hook `useDashboardChileCompra`
**Depende de:** T4 (endpoints listos)  
**Estimación:** 2h  
**Work:**
- `src/mpm-web/src/types/chilecompra.ts`: DTOs `ChileCompraKpi`, `ChileCompraCompetidor`, `ChileCompraSerieTemporal`, `ChileCompraConvenioMarco`
- `src/mpm-web/src/hooks/useDashboardChileCompra.ts`:
  - `fetchKpis(anio)`, `fetchSerieTemporal()`, `fetchCompetidores(anio)`, `fetchConveniosMarco(anio)`
  - `fetchConsolidado(anio)` → usa endpoint consolidado T6
  - React Query: `staleTime: 5min`, `cacheTime: 30min`, `refetchOnWindowFocus: false`
  - Error handling: toast + retry button
**Verify:**
```bash
npm run build # sin errores TS
# En browser console: hook retorna datos tipados
```

---

### T8 — Componentes KPI Cards (YoY + Total + CM%)
**Depende de:** T7  
**Estimación:** 3h  
**Work:**
- `src/mpm-web/src/components/dashboard/ChileCompraKpiCards.tsx`:
  - 4 cards: Total Actual, Total Anterior, **YoY% (verde/rojo/gris)**, CM Actual + CM%
  - Tooltip YoY: "((Actual - Anterior) / Anterior) × 100"
  - Sparkline mini (últimos 12 meses) en card Total
  - Loading: Skeleton; Error: Alert + retry
- Responsive: grid 2x2 desktop, 1x4 móvil
**Verify:**
- Visual: cards render con datos mock → luego reales
- YoY 0% → gris "N/A" si anterior=0
- CM% = (cm/actual)×100

---

### T9 — Componente Convenios Marco (Lista + Sparkline)
**Depende de:** T7  
**Estimación:** 2h  
**Work:**
- `src/mpm-web/src/components/dashboard/ChileCompraConveniosMarco.tsx`:
  - Tabla: Código, Nombre, Monto, Es TI (badge), Año
  - Filtro: "Solo TI" (checkbox)
  - Sparkline evolución 2020-2025 (monto CM por año)
  - Click fila → modal detalle (fechas, descripción)
**Verify:**
- Tabla muestra 10+ convenios 2025
- Filtro TI reduce a 3-4 filas
- Sparkline visible

---

### T10 — Tabla Competidores + Ranking + Drill-down
**Depende de:** T5, T7  
**Estimación:** 4h  
**Work:**
- `src/mpm-web/src/components/dashboard/ChileCompraCompetidoresTable.tsx`:
  - AntD Table: columnas RUT, Nombre, Total 2025, CM 2025, YoY%, Ranking
  - Sortable por Total, YoY, Ranking
  - Row click → Modal `CompetidorDetailModal`:
    - KPIs empresa (total, cm, ranking, yoy)
    - Serie temporal (gráfico línea 12 meses)
    - Modalidad breakdown (bar chart: Licitación vs CM)
- Paginación servidor (pageSize=10)
**Verify:**
- Tabla ordenada por Total desc
- Modal abre con datos correctos
- Gráficos renderizan (Recharts o similar)

---

### T11 — Filtro Año + Integración Página Dashboard
**Depende de:** T8, T9, T10  
**Estimación:** 2h  
**Work:**
- `src/mpm-web/src/pages/EjecutivoDashboardPage.tsx`:
  - Select año (2020-2026) en header → actualiza todos los componentes
  - Layout: KPI cards (top) → Convenios Marco (left 50%) + Competidores (right 50%)
  - Móvil: stack vertical
  - Loading global + error boundary
- Reemplazar datos mock anteriores por `useDashboardChileCompra`
**Verify:**
- Cambio año → todos los componentes refrescan
- Móvil: layout correcto
- No console errors

---

## Calidad + Docs + CI

### T12 — Tests Backend (Unit + Integration)
**Depende de:** T3, T4, T5, T6  
**Estimación:** 4h  
**Work:**
- `tests/MPM.Modules.Analisis.Tests/Services/ChileCompraIngestionServiceTests.cs`:
  - `IngestTivitAsync_Upsert_Idempotent` (mock HttpClient)
  - `IngestCompetidoresAsync_RateLimits_ContinuesOnError`
  - `GetKpiAsync_CalculatesYoY_Correctly`
- `tests/MPM.Modules.Analisis.Tests/Integration/DashboardChileCompraApiTests.cs`:
  - `KpiEndpoint_ReturnsCorrectShape_WithCache`
  - `CompetidoresEndpoint_RankingDesc`
  - `ConsolidadoEndpoint_AllSectionsPresent`
**Verify:**
```bash
dotnet test tests/MPM.Modules.Analisis.Tests --filter "ChileCompra"
# 8+ tests pass
```

---

### T13 — Tests E2E Playwright (Dashboard)
**Depende de:** T11  
**Estimación:** 3h  
**Work:**
- `src/mpm-web/e2e/specs/dashboard-chilecompra.spec.ts`:
  - `dashboard carga kpis yoy cm%`
  - `filtro año actualiza todos los componentes`
  - `tabla competidores sortable + drill-down modal`
  - `convenios marco filtro TI + sparkline`
  - `responsive móvil layout stack`
- Page Object: `DashboardChileCompraPage.ts`
**Verify:**
```bash
cd src/mpm-web && npx playwright test e2e/specs/dashboard-chilecompra.spec.ts
# 5 tests pass
```

---

### T14 — Documentación API + CHANGELOG + Migración
**Depende de:** T1, T6  
**Estimación:** 2h  
**Work:**
- `docs/api-first/analisis.md`: agregar endpoints ChileCompra (KPI, serie, competidores, consolidado)
- `docs/api-first/analisis-comercial.md`: sección "Datos ChileCompra Datos Abiertos"
- `CHANGELOG.md`: entrada `[Unreleased]` Sprint 2
- Verificar migración `V160` aplicable en staging
**Verify:**
```bash
grep -n "chilecompra" docs/api-first/analisis.md
grep "Sprint 2" CHANGELOG.md
```

---

## Resumen Dependencias

```
T1 (Migración)
  → T2 (HTTP Client)
    → T3 (Ingesta TIVIT)
      → T4 (Endpoints TIVIT)
      → T5 (Competidores)
        → T6 (Consolidado)
          → T7 (Types + Hook)
            → T8 (KPI Cards)
            → T9 (Convenios Marco)
            → T10 (Competidores Table)
              → T11 (Página Dashboard + Filtro Año)
T12 (Tests Backend) ← T3,T4,T5,T6
T13 (E2E) ← T11
T14 (Docs) ← T1,T6
```

---

## Ejecución Paralela (2 Devs)

| Dev A (Backend) | Dev B (Frontend) |
|-----------------|------------------|
| T1 → T2 → T3 → T4 → T5 → T6 | Espera T4 → T7 → T8 → T9 → T10 → T11 |
| T12 (tests) | T13 (E2E) |
| T14 (docs) | — |

---

## Definition of Done Sprint 2

- [ ] Migración `V160` aplicada en staging + prod
- [ ] Ingesta TIVIT 2020-2026 completa (manual + job)
- [ ] Competidores 5 RUTs ingesta + ranking
- [ ] Dashboard: KPIs YoY, CM%, tabla competidores, drill-down, filtro año
- [ ] Tests: Unit 8+, Integration 3+, E2E 5+ passing
- [ ] Docs API + CHANGELOG actualizados
- [ ] Deploy staging + UAT Francisco/Carlos ✅
- [ ] Retro + Demo