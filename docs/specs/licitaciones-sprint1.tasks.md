# Tasks: Licitaciones Sprint 1 — Filtro Monto, Presupuesto, Institución

**Spec:** `docs/api-first/licitaciones-sprint1-hus.md`  
**HUs:** MPM-LIC-001, MPM-LIC-002, MPM-LIC-003  
**Sprint:** 1  
**Total estimado:** 1.5 días (1 dev) / 1 día (2 devs)

---

## Backend — Base de Datos y API

### T1 — SP `usp_Licitaciones_Listar`: agregar parámetros `p_monto_desde`, `p_monto_hasta` + sort `monto_estimado` ✅ **COMPLETADO**
**Depende de:** none  
**Estimación:** 1h  
**Work:**
- Editar migración SQL (nueva versión Vxxx) para `CREATE OR REPLACE FUNCTION usp_Licitaciones_Listar`
- Agregar parámetros `p_monto_desde DECIMAL(18,2) DEFAULT NULL`, `p_monto_hasta DECIMAL(18,2) DEFAULT NULL`
- En el WHERE dinámico: `AND (p_monto_desde IS NULL OR monto_estimado >= p_monto_desde)` y `AND (p_monto_hasta IS NULL OR monto_estimado <= p_monto_hasta)`
- Agregar `monto_estimado` a la lista blanca de columnas ordenables (`p_sort_by = 'monto_estimado'`)
- `ORDER BY CASE WHEN p_sort_by = 'monto_estimado' THEN monto_estimado END DESC NULLS LAST`
- Incluir `DROP FUNCTION` de firma anterior si existe (evitar overload como en CHANGELOG 031 V125)
**Verify:**
```sql
-- 1. Llamar sin filtro → retorna todas
SELECT * FROM usp_Licitaciones_Listar(1, 20, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'fecha_publicacion', 'desc');
-- 2. Llamar con montoDesde=50000000 → solo >= 50M
SELECT * FROM usp_Licitaciones_Listar(1, 20, NULL, NULL, NULL, NULL, NULL, NULL, 50000000, NULL, 'fecha_publicacion', 'desc');
-- 3. Llamar con montoHasta=100000000 → solo <= 100M
SELECT * FROM usp_Licitaciones_Listar(1, 20, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 100000000, 'fecha_publicacion', 'desc');
-- 4. Llamar con rango montoDesde=50000000 montoHasta=100000000
SELECT * FROM usp_Licitaciones_Listar(1, 20, NULL, NULL, NULL, NULL, NULL, NULL, 50000000, 100000000, 'fecha_publicacion', 'desc');
-- 5. Ordenar por monto_estimado desc
SELECT * FROM usp_Licitaciones_Listar(1, 20, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'monto_estimado', 'desc');
-- 6. Verificar que NULLs van al final en orden desc
```

---

### T2 — Endpoint `GET /api/v1/licitaciones`: query params `montoDesde`, `montoHasta` + sort `monto_estimado` ✅ **COMPLETADO**
**Depende de:** T1  
**Estimación:** 1h  
**Work:**
- `MPM.Modules.Licitaciones/Controllers/LicitacionesController.cs`: agregar `[FromQuery] decimal? montoDesde`, `[FromQuery] decimal? montoHasta` en `ListarAsync`
- Pasar a `LicitacionService.ListarAsync(..., montoDesde, montoHasta)`
- `LicitacionService.cs`: pasar a handler
- `LicitacionHandler.cs`: agregar parámetros a llamada SP
- Validación: `if (montoDesde <= 0) throw ValidationException("VAL_001", "Monto desde debe ser positivo")`, `if (montoHasta <= 0) throw ValidationException("VAL_001", "Monto hasta debe ser positivo")`, `if (montoDesde > montoHasta) throw ValidationException("VAL_001", "Monto desde no puede ser mayor a monto hasta")`
- Agregar `monto_estimado` a `allowedSortColumns` en handler/servicio
**Verify:**
```bash
# 1. Sin filtro
curl "https://localhost:5001/api/v1/licitaciones?page=1&pageSize=5"
# 2. Con filtro montoDesde
curl "https://localhost:5001/api/v1/licitaciones?page=1&pageSize=5&montoDesde=50000000"
# 3. Con filtro montoHasta
curl "https://localhost:5001/api/v1/licitaciones?page=1&pageSize=5&montoHasta=100000000"
# 4. Con rango completo
curl "https://localhost:5001/api/v1/licitaciones?page=1&pageSize=5&montoDesde=50000000&montoHasta=100000000"
# 5. Ordenar por monto desc
curl "https://localhost:5001/api/v1/licitaciones?page=1&pageSize=5&sortBy=monto_estimado&sortDir=desc"
# 6. Validaciones negativas
curl "https://localhost:5001/api/v1/licitaciones?montoDesde=-100"  # → 400 VAL_001
curl "https://localhost:5001/api/v1/licitaciones?montoHasta=-100"  # → 400 VAL_001
curl "https://localhost:5001/api/v1/licitaciones?montoDesde=100000000&montoHasta=50000000"  # → 400 VAL_001
```

---

### T3 — Tests unitarios backend: filtro monto (desde/hasta) + orden monto ✅ **COMPLETADO**
**Depende de:** T2  
**Estimación:** 1h  
**Work:**
- `tests/MPM.Modules.Licitaciones.Tests/LicitacionServiceTests.cs`: nuevos tests
  - `ListarAsync_ConMontoDesde_RetornaSoloMayoresIguales`
  - `ListarAsync_ConMontoHasta_RetornaSoloMenoresIguales`
  - `ListarAsync_ConRangoMonto_RetornaEnRango`
  - `ListarAsync_OrdenMontoEstimadoDesc_NullsLast`
  - `ListarAsync_MontoDesdeNegativo_LanzaValidacion`
  - `ListarAsync_MontoHastaNegativo_LanzaValidacion`
  - `ListarAsync_MontoDesdeMayorQueHasta_LanzaValidacion`
- Mock `LicitacionHandler` → verificar parámetros pasados al SP
**Verify:**
```bash
dotnet test tests/MPM.Modules.Licitaciones.Tests --filter "FullyQualifiedName~LicitacionServiceTests"
# 7 tests pass
```

---

## Frontend — Filtros y Lista

### T4 — `LicitacionFilterBar.tsx`: campos "Monto desde" / "Monto hasta" + URL sync ✅ **COMPLETADO**
**Depende de:** T2 (endpoint listo)  
**Estimación:** 2h  
**Work:**
- Agregar `montoDesde`, `montoHasta` a `LicitacionFilters` interface en `useLicitaciones.ts`
- En `LicitacionFilterBar.tsx`:
  - Dos `InputNumber` (AntD) con placeholders "Ej: 50000000", `min={0}`, `step={1000000}`
  - Formateo visual: mostrar con separador de miles mientras se edita (opcional)
  - Incluir en `onSearch` / `onFilterChange` → push a URL (`useSearchParams`)
- En `useLicitaciones.ts`:
  - Leer `montoDesde`, `montoHasta` de `searchParams` en inicialización
  - Incluir en `fetchLicitaciones` query params
- Botón "Reiniciar filtros" limpia `montoDesde` y `montoHasta` (setear `undefined`)
**Verify:**
- [ ] Cargar `/licitaciones` → campos visibles en barra de filtros
- [ ] Ingresar `50000000` en "Monto desde" + Enter → URL incluye `?montoDesde=50000000`, tabla filtra
- [ ] Ingresar `100000000` en "Monto hasta" + Enter → URL incluye `?montoHasta=100000000`, tabla filtra
- [ ] Combinar ambos → URL incluye ambos params, tabla filtra rango
- [ ] Combinar con filtro Estado "Publicada" → todos aplican
- [ ] Click "Reiniciar filtros" → campos limpios, URL sin params, tabla completa
- [ ] Refrescar página (F5) → filtros persisten desde URL

---

### T5 — `LicitacionesPage.tsx`: render "Presupuesto" en tarjetas + sort header ✅ **COMPLETADO**
**Depende de:** T2 (endpoint ordena por monto)  
**Estimación:** 1.5h  
**Work:**
- En tarjeta de licitación (`LicitacionCard.tsx` o inline en `LicitacionesPage`):
  - Nueva fila: `<Typography.Text>Presupuesto: {formatCLP(licitacion.montoEstimado)}</Typography.Text>`
  - `formatCLP`: helper `Intl.NumberFormat('es-CL', {style: 'currency', currency: 'CLP', minimumFractionDigits: 0})`
  - Si `montoEstimado === null/undefined` → render "—"
- En header de tabla/grilla (si usa `Table` de AntD) o toolbar:
  - Agregar opción "Presupuesto" al selector de ordenamiento (`sortBy='monto_estimado'`)
  - Default sortDir = 'desc' para monto
- Responsive: en `< 640px` ocultar label "Presupuesto:", solo monto
**Verify:**
- [ ] Tarjeta muestra "$45.000.000 CLP" para monto 45000000
- [ ] Tarjeta muestra "—" para monto NULL
- [ ] Click "Presupuesto" en sort → request incluye `sortBy=monto_estimado&sortDir=desc`
- [ ] Segundo click → `sortDir=asc` (NULLs last)
- [ ] Móvil (DevTools device toolbar): solo monto visible, sin label

---

### T6 — `LicitacionesPage.tsx`: render "Institución" en tarjetas ✅ **COMPLETADO**
**Depende de:** none (dato ya viene en DTO)  
**Estimación:** 1h  
**Work:**
- En tarjeta de licitación, bajo nombre:
  - `<Typography.Text ellipsis={{rows: 1, tooltip: licitacion.organismo}}>Institución: {licitacion.organismo || '—'}</Typography.Text>`
  - `ellipsis` de AntD maneja truncado + tooltip nativo
- Estilo: `color: #8c8c8c`, `fontSize: 12px`, `marginTop: 4px`
**Verify:**
- [ ] Tarjeta muestra "Institución: Municipalidad de Santiago"
- [ ] Nombre largo truncado con ellipsis, hover → tooltip completo
- [ ] NULL/empty → "Institución: —"

---

### T7 — Tests E2E Playwright: filtros + visualización
**Depende de:** T4, T5, T6  
**Estimación:** 1.5h  
**Work:**
- `src/mpm-web/e2e/specs/licitaciones-filtros-monto.spec.ts`:
  - Test: filtro monto mínimo filtra correctamente (comparar count antes/después)
  - Test: filtro monto + estado combinados
  - Test: sort por presupuesto desc/asc
  - Test: render presupuesto e institución en tarjetas
  - Test: reset filtros limpia monto mínimo
- Usar `LicitacionesPage.ts` Page Object existente
**Verify:**
```bash
cd src/mpm-web && npx playwright test e2e/specs/licitaciones-filtros-monto.spec.ts
# 5 tests pass
```

---

## Integración y Documentación

### T8 — Actualizar `docs/api-first/licitaciones.md` con cambios
**Depende de:** T1, T2  
**Estimación:** 0.5h  
**Work:**
- Agregar `montoMinimo` a tabla de parámetros `GET /api/v1/licitaciones`
- Agregar `monto_estimado` a valores permitidos de `sortBy`
- Documentar nuevo parámetro SP `p_monto_minimo`
- Agregar Business Rules `BUS_LIC_009`, `BUS_LIC_010`, `BUS_LIC_011`
**Verify:**
- [ ] `docs/api-first/licitaciones.md` refleja API actual
- [ ] `grep -n "montoMinimo" docs/api-first/licitaciones.md` encuentra entradas

---

### T9 — Migración SQL versionada (archivo .sql en docs/migrations/)
**Depende de:** T1  
**Estimación:** 0.5h  
**Work:**
- Crear `docs/migrations/Vxxx_FILTRO_MONTO_MINIMO_LICITACIONES.sql`
- Contenido: `DROP FUNCTION IF EXISTS usp_Licitaciones_Listar(...firma vieja...); CREATE OR REPLACE FUNCTION...`
- Seguir patrón V125 del CHANGELOG (drop explícito para evitar overload)
**Verify:**
- [ ] Archivo existe en `docs/migrations/`
- [ ] Ejecuta limpio en BD limpia (docker-compose up --build)
- [ ] `psql -f Vxxx_...sql` sin errores

---

## Resumen de Dependencias

```
T1 (SP) 
  → T2 (Endpoint) 
    → T3 (Unit Tests Backend)
    → T4 (FilterBar Frontend)
    → T5 (Presupuesto en tarjetas)
T6 (Institución) ← independiente (dato ya existe)
T3, T4, T5, T6 → T7 (E2E)
T1, T2 → T8 (Docs API)
T1 → T9 (Migración SQL)
```

---

## Orden de Ejecución Sugerido (1 dev)

| Día | Tasks | Notas |
|-----|-------|-------|
| Mañana | T1 → T2 → T3 | Backend completo + tests |
| Tarde | T4 → T5 → T6 | Frontend filters + render |
| Día 2 Mañana | T7 → T8 → T9 | E2E + docs + migración |

---

## Orden de Ejecución Paralelo (2 devs)

| Dev A (Backend) | Dev B (Frontend) |
|-----------------|------------------|
| T1 → T2 → T3 | Espera T2 listo → T4 → T5 → T6 |
| T9 (migración) | T7 (E2E, tras T4-6) |
| T8 (docs) | — |

---

## Definition of Done (Sprint 1)

- [ ] 3 HUs implementadas y verificadas
- [ ] Unit tests backend: 3 nuevos pass
- [ ] E2E tests: 5 nuevos pass
- [ ] `docs/api-first/licitaciones.md` actualizado
- [ ] Migración SQL versionada en `docs/migrations/`
- [ ] CHANGELOG.md actualizado (sección Unreleased)
- [ ] Deploy a staging/docker-compose funcional