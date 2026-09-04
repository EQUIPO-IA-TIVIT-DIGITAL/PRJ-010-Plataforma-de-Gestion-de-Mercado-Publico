# Tasks: Track 1 Quick Wins — YoY Ejecutivo, Preferencia Monto Mínimo, Go/No-Go por Tipo

**Specs:**
- `docs/api-first/analisis-ejecutivo-v2.md` (Feature A)
- `docs/api-first/preferencias-usuario.md` (Feature B)
- `docs/specs/go-nogo-por-tipo.feature-spec.md` (Feature C)

**Sprint:** Track 1 Quick Wins (v1.1)
**Total estimado:** ~27h ≈ 3.5 días (1 dev) / ~2 días (2 devs en paralelo por track)
**Gates HITL antes de iniciar:** validar reglas GO-T001..T007 y decisión D2 de `preferencias-usuario.md` con el cliente (no bloquean T1–T6).

> Convenciones: migraciones en `src\MPM.Api\Database\Scripts` con naming `V{N}__Descripcion.sql` — usar el siguiente número libre (≥ V155; placeholders V15X aquí). Contrato de respuesta `{success:true,data}` / `{success:false,error:{code,message,details},meta}`. Test Gate: toda tarea de lógica incluye sus tests.

---

## Feature A — Crecimiento Interanual (YoY) Dashboard Ejecutivo

### T1 — Backend: `comparacionAnual` en `AnalisisService` + tests
**Capa:** backend
**Depende de:** none
**Estimación:** 3h
**Work:**
- `MPM.Modules.Analisis/Models/AnalisisDtos.cs`: agregar `ComparacionAnualDto` (ANA §2) y campo `ComparacionAnual` nullable en `DashboardEjecutivoDto`.
- `AnalisisService.cs`: extraer método privado `CalcularTotalesGanadas(int anio)` reutilizando dedup + extracción de año real existentes; en `GetDashboardEjecutivoAsync`, cuando `anio.HasValue`, invocar segunda pasada con `anio - 1` y armar `ComparacionAnualDto` según ANA-R020..R025 (`variacionPorcentaje` null si `montoAnterior = 0`; `-100.0` si actual=0 y anterior>0; `tieneDatosAnioAnterior` false solo sin licitaciones).
- Cuando `anio` es null → `ComparacionAnual = null`.
- Tests (`tests/MPM.Modules.Analisis.Tests/AnalisisServiceTests.cs` o equivalente): año con datos ambos años (% correcto, 1 decimal), año anterior sin filas (`tieneDatosAnioAnterior=false`, var null), anterior con monto 0 (var null), actual 0 con anterior >0 (-100), request sin anio (campo null), consistencia `montoActual == montoTotalGanado` del mismo año.
**Verify:**
```bash
dotnet test tests/MPM.Modules.Analisis.Tests --filter "FullyQualifiedName~Ejecutivo"
# ≥ 6 tests nuevos pass; suite completa verde
```
- [ ] Response de `GET /api/v1/analisis/ejecutivo?anio=2026` incluye `data.comparacionAnual` con los 6 campos.

---

### T2 — Frontend: bloque YoY en `EjecutivoDashboardPage` + test
**Capa:** frontend
**Depende de:** T1
**Estimación:** 3h
**Work:**
- `src/mpm-web/src/types/analisis.ts`: agregar `ComparacionAnual` y campo opcional en `DashboardEjecutivo`.
- `EjecutivoDashboardPage.tsx`: tarjeta/bloque bajo el KPI "Monto ganado" con: monto actual, monto año anterior, % con flecha ↑/↓ y color semántico (verde/rojo); estados diferenciados: "Sin datos del año anterior" (`tieneDatosAnioAnterior=false`) y "Sin base de comparación" (anterior=0 con datos). Sin filtro de año → bloque oculto.
- Formato CLP con helper existente (`Intl.NumberFormat('es-CL')`).
- Test Vitest/RTL: render con variación positiva/negativa/null, estados vacíos, oculto sin `anio`.
**Verify:**
```bash
cd src/mpm-web && npx vitest run src/pages/__tests__/EjecutivoDashboardPage
# tests pass
```
- [ ] Con datos 2026=1.250M / 2025=980M se muestra "+27,6%" con flecha arriba.
- [ ] Año anterior sin datos muestra texto explicativo, nunca "∞%" ni "NaN".

---

## Feature B — Preferencia de Usuario: Monto Mínimo

### T3 — DB: migración `V15X__Preferencias_Usuario_Monto_Minimo.sql`
**Capa:** db
**Depende de:** none
**Estimación:** 2h
**Work:**
- Nueva migración en `src\MPM.Api\Database\Scripts` (número real ≥ V155):
  - `CREATE TABLE IF NOT EXISTS preferencias_usuario (user_id VARCHAR(200) PRIMARY KEY, monto_minimo NUMERIC(18,2) CHECK (monto_minimo IS NULL OR monto_minimo >= 0), updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW())`
  - `usp_PreferenciasUsuario_Obtener(p_user_id)` → fila o NULL (patrón `usp_CensoPreferencias_Obtener`, V143).
  - `usp_PreferenciasUsuario_Upsert(p_user_id, p_monto_minimo, OUT p_error_msg)` → INSERT ... ON CONFLICT (user_id) DO UPDATE SET monto_minimo, updated_at = NOW().
- Validar monto negativo en SP (SET p_error_msg) además del CHECK.
**Verify:**
```sql
SELECT * FROM usp_PreferenciasUsuario_Obtener('user-test');            -- NULL sin fila
CALL usp_PreferenciasUsuario_Upsert('user-test', 50000000, NULL);
SELECT * FROM usp_PreferenciasUsuario_Obtener('user-test');            -- 50000000
CALL usp_PreferenciasUsuario_Upsert('user-test', NULL, NULL);          -- borra
SELECT * FROM usp_PreferenciasUsuario_Obtener('user-test');            -- NULL
-- upsert repetido no duplica filas; CHECK rechaza -1
```
- [ ] Migración corre limpia sobre BD desde cero (docker-compose up --build).

---

### T4 — Backend: endpoints GET/PUT `/usuarios/me/preferencias-licitaciones` + tests
**Capa:** backend
**Depende de:** T3
**Estimación:** 3h
**Work:**
- `MPM.Modules.Licitaciones/Data`: constantes SP + métodos `PreferenciasObtenerAsync`/`PreferenciasUpsertAsync` (patrón `CensoHandler`).
- `Models`: `PreferenciasLicitacionesDto { decimal? MontoMinimo }` + UpdateDto.
- `Controllers/PreferenciasLicitacionesController.cs`: `[Route("api/v1/usuarios/me/preferencias-licitaciones")]`, GET (sin fila → `Ok(dto con null)`, patrón censo) y PUT (validar VAL_001 si < 0; user_id SIEMPRE del JWT `tenant.UserId`; upsert idempotente).
- Tests unitarios: GET con/sin fila, PUT valor válido/null/negativo (400 VAL_001), user_id ignorado si viniera en body.
**Verify:**
```bash
dotnet test tests/MPM.Modules.Licitaciones.Tests --filter "FullyQualifiedName~Preferencias"
# ≥ 5 tests pass
curl https://localhost:5001/api/v1/usuarios/me/preferencias-licitaciones          # {"success":true,"data":{"montoMinimo":null}}
curl -X PUT ... -d '{"montoMinimo":50000000}'                                     # echo del valor
curl -X PUT ... -d '{"montoMinimo":-1}'                                           # 400 VAL_001
```

---

### T5 — Frontend: default del filtro desde preferencia + indicador + test
**Capa:** frontend
**Depende de:** T4
**Estimación:** 3h
**Work:**
- Hook `usePreferenciasLicitaciones` (React Query) en `src/mpm-web/src/hooks`.
- `useLicitaciones.ts`: al montar SIN `montoDesde` en URL, sembrar el filtro con la preferencia antes del primer fetch (decisión D2: aplicación en front). Si la URL trae `montoDesde`, override explícito gana (PREF-R001).
- `LicitacionFilterBar.tsx`: tag discreto "según tu preferencia" junto al campo cuando el valor activo proviene de la preferencia y no fue editado; limpiar filtro = ver todo en sesión (PREF-R002).
- Test Vitest/RTL: sin preferencia → sin cambio; con preferencia → primer request incluye `montoDesde`; override manual pisa el default; URL manda sobre preferencia.
**Verify:**
```bash
cd src/mpm-web && npx vitest run src/hooks/__tests__ src/components/__tests__/LicitacionFilterBar
```
- [ ] Usuario con $50M configurado recarga `/licitaciones` (URL limpia) → lista filtrada + tag visible.
- [ ] Limpia el filtro → ve todas; refresca → preferencia reaplica.

---

### T6 — E2E Playwright: preferencia + override temporal
**Capa:** tests
**Depende de:** T5
**Estimación:** 2h
**Work:**
- `src/mpm-web/e2e/specs/preferencias-monto-minimo.spec.ts`: configurar preferencia vía API (setup), recargar listado y verificar filtrado + tag; limpiar filtro y verificar listado completo; persistencia tras reload; usuario sin preferencia sin cambios.
**Verify:**
```bash
cd src/mpm-web && npx playwright test e2e/specs/preferencias-monto-minimo.spec.ts
# 4 tests pass
```

---

## Feature C — Go/No-Go condicionado al Tipo de Licitación

> ⛔ **Gate HITL**: T7 requiere reglas GO-T001..T007 validadas por el cliente (desajuste "450 UTM" vs catálogo real LE/LP/LQ/LR). T8 puede iniciarse en paralelo.

### T7 — Backend: resolver tipo + prompt parametrizado + `modulacion_tipo` + tests
**Capa:** backend
**Depende de:** validación HITL de reglas (gate)
**Estimación:** 4h
**Work:**
- `AnalisisComercialService.cs`: al iniciar (`IniciarAsync` ya tiene `licitacionId`), consultar `licitaciones.tipo` + nombre desde `tipos_licitacion` (handler nuevo o reutilizado); mapear código → grupo de regla (GO §6, incl. fallback `generico_sin_clasificar` para NULL/desconocido — GO-R013: fallo no aborta).
- `PromptAnalisisComercial(documentosCount, contextoTipo)`: inyectar bloque de instrucciones por grupo (GO-T001..T008) y agregar `"modulacion_tipo": {tipo_codigo, tipo_nombre, grupo_regla, regla_aplicada, notas}` al schema JSON del prompt (GO-R011). Caso CO: instrucciones de evaluación de catálogo (GO-T002).
- Verificar que `SanearYExtraer` (devuelve `GetRawText()`) persiste `modulacion_tipo` dentro de `resultado_json` sin tocar columnas — prueba con test.
- Tests unitarios builder: cada grupo de regla, fallback genérico, tipo NULL, CO especial; test de parseo con y sin `modulacion_tipo`.
**Verify:**
```bash
dotnet test tests/MPM.Modules.Licitaciones.Tests --filter "FullyQualifiedName~AnalisisComercial"
# tests nuevos pass; suite completa verde (regresión GO AC)
```
- [ ] Análisis real de una licitación CO (staging) persiste `resultado_json` con `regla_aplicada="convenio_marco_evaluacion_catalogo"`.

---

### T8 — Frontend: chip de tipo + regla aplicada en `AnalisisComercialPanel` + test
**Capa:** frontend
**Depende de:** none (contrato aditivo definido en spec; puede paralelizar con T7 usando fixture)
**Estimación:** 3h
**Work:**
- Types TS del estado de análisis comercial: campo opcional `modulacionTipo`.
- `AnalisisComercialPanel.tsx`: junto al badge Go/No-Go, chip con nombre del tipo + línea con regla aplicada ("Evaluación de catálogo" para CO); análisis legacy (sin campo) → "Tipo: no disponible (análisis previo)" (GO §5).
- Test Vitest/RTL: con `modulacion_tipo` completo, con `grupo_regla` genérico, ausente (legacy).
**Verify:**
```bash
cd src/mpm-web && npx vitest run src/components/__tests__/AnalisisComercialPanel
```
- [ ] Fixture CO muestra chip "Convenio Marco · Evaluación de catálogo"; fixture legacy muestra texto degradado sin romper badge.

---

### T9 — Integración: análisis modulado + snapshot de decisión intacto
**Capa:** tests
**Depende de:** T7, T8
**Estimación:** 2h
**Work:**
- Test de integración backend con LLM mockeado: licitación tipo CO → recomendación completada con `modulacion_tipo` correcto; luego `DecisionService.RegistrarAsync(no_go, motivo)` → snapshot `recomendacion_ia`/`score_confianza` idéntico al mostrado (GO-R014, DEC-R005/R006).
- Test regresión: licitación sin tipo → decisión humana fluye igual que hoy.
**Verify:**
```bash
dotnet test tests/MPM.Modules.Colaboracion.Tests --filter "FullyQualifiedName~Decision"
dotnet test tests/MPM.Modules.Licitaciones.Tests --filter "FullyQualifiedName~Integracion"
# todos pass
```

---

## Cierre

### T10 — Documentación y catálogo
**Capa:** docs
**Depende de:** T1–T9
**Estimación:** 1h
**Work:**
- `docs/api-first/analisis.md`: referenciar delta v2 (`analisis-ejecutivo-v2.md`) en el endpoint ejecutivo.
- `docs/API_CATALOG.md`: alta de `GET|PUT /api/v1/usuarios/me/preferencias-licitaciones`.
- `CHANGELOG.md`: entrada Unreleased con las 3 features.
- Marcar specs como implementadas (estado) y cerrar marcadores `[HITL]` resueltos.
**Verify:**
- [ ] `grep -n "preferencias-licitaciones" docs/API_CATALOG.md` encuentra entrada.
- [ ] CHANGELOG Unreleased lista las 3 features con refs a specs.

---

## Resumen de Dependencias

```
FEATURE A:   T1 ──→ T2
FEATURE B:   T3 ──→ T4 ──→ T5 ──→ T6
FEATURE C:   (gate HITL) T7 ──→ T9 ←── T8 (paralelizable desde inicio)
CIERRE:      T1..T9 ──→ T10
```

Los tres tracks son independientes entre sí (módulos Analisis / Licitaciones+Auth-route / Licitaciones+Colaboracion).

## Orden de Ejecución Sugerido (1 dev)

| Día | Tasks | Notas |
|-----|-------|-------|
| Día 1 | T1 → T2 → T3 | Feature A completa + migración B |
| Día 2 | T4 → T5 → T6 | Feature B completa |
| Día 3 | T7 → T8 | Feature C backend + UI (previo gate HITL) |
| Día 4 | T9 → T10 | Integración + cierre |

## Orden de Ejecución Paralelo (2 devs)

| Dev A (backend-heavy) | Dev B (frontend-heavy) |
|------------------------|------------------------|
| T1 → T3 → T4 → T7 | T2 (tras T1) → T5 (tras T4) → T8 |
| T9 (con Dev B) → T10 | T6 |

## Definition of Done (Track 1)

- [ ] Features A, B, C implementadas según specs y criterios de aceptación de cada documento
- [ ] Unit tests backend: ≥ 16 nuevos pass (A:6, B:5, C:builder+integración)
- [ ] Tests frontend Vitest/RTL: ≥ 8 nuevos pass
- [ ] E2E Playwright: 4 nuevos pass (B)
- [ ] Migración V15X aplicada limpia en BD desde cero
- [ ] Sin breaking changes: respuestas existentes de `/analisis/ejecutivo` y `/licitaciones` compatibles
- [ ] Marcadores `[HITL]` resueltos y documentados en las specs
- [ ] `docs/api-first/*`, `API_CATALOG.md`, `CHANGELOG.md` actualizados
