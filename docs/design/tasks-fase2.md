# Tasks — Fase 2 (match Census + GO/NO GO formal)

**Origen**: specs `docs/api-first/censo.md` + `docs/api-first/decisiones.md` (aprobadas 2026-08-16)
**Rama**: `036-flujo-comercial-ofertas` · **Modo**: bundle (delivery-agent)

## Bundle A — Backend módulo Censo (CEN)

- [ ] A1. Ajustar borrador técnico a la spec: revisar `src/MPM.Modules.Censo/` (csproj, ModuleRegistration, DTOs, CensoHandler, V143) — corregir lo que la spec mande.
- [ ] A2. `CensusTokenManager` (singleton): cache JWT, `exp` con margen 2 min, `SemaphoreSlim`, renovación.
- [ ] A3. `CensusClient` (HttpClient): auth `{Username,Password}` → tokens; `GetUsersByTechnology`, `GetUsersByCertification` (substring, país opcional), `GetCatalogoKnowledge`, `DownloadCertificationFile`; **retry 401** (renovar + 1 reintento); timeout largo.
- [ ] A4. `CensoCatalogoService`: refresco desde `census/knowledge` → `censo_catalogo` (limpiar + upsert); listar.
- [ ] A5. `CensoExpansionService`: Capa 1 (fuzzy types ≥80) → Capa 2 (fuzzy tecnología) → Capa 3 (IA fallback vía `LlmClientResolver`, cacheada en `censo_expansiones`); normalización sin acentos; token_set_ratio.
- [ ] A6. `CensoMatchService`: requisitos (body o análisis V142) → expansión → consultas paralelas (semáforo 8) con cache 24 h (`censo_cache_personas`) → dedup por email → scoring cobertura + levelSkill + bonus país → guardar `censo_match`.
- [ ] A7. Controllers: `MatchCapacidadesController` (POST/GET), `CensoCatalogoController` (GET + POST refrescar), `CensoPreferenciasController` (GET/PUT `/usuarios/me/preferencias-censo`) — con `ApiResponse`, `[Authorize]`, TenantContext, errores CEN_001..004.
- [ ] A8. Registros: `dotnet sln add`, `MPM.Api.csproj` (ProjectReference), `Program.cs` (`AddCensoModule`), `Dockerfile` (COPY csproj), `appsettings.json` (Censo:Url/Username vacíos, password por env).
- [ ] A9. Build solución OK.

## Bundle B — Decisiones GO/NO GO (DEC, en Colaboracion)

- [ ] B1. Fix **V144**: `estado_licitacion_al_marcar` = estado REAL de la licitación (fallback 1) — no hardcode.
- [ ] B2. `DecisionHandler` (Colaboracion): `usp_LicitacionesDecision_Registrar/Obtener` (Dapper).
- [ ] B3. `DecisionService`: registrar (snapshot recomendación IA desde `analisis_licitacion_comercial` si existe; motivo obligatorio en NO GO) + obtener.
- [ ] B4. `DecisionController` (POST/GET `/licitaciones/{codigo}/decision`) + `ModuleRegistration`.
- [ ] B5. Build OK.

## Bundle C — Frontend

- [ ] C1. Tipos + hooks: `useMatchCapacidades`, `useDecision`, `usePreferenciasCenso`.
- [ ] C2. Sección "Capacidades TIVIT" en la ficha: toggle país, match por rol con cobertura x/10, certificaciones.
- [ ] C3. Panel GO/NO GO: recomendación IA + botones + motivo (obligatorio en NO GO).
- [ ] C4. `tsc` limpio.

## Bundle D — Tests + validación (control-agent)

- [ ] D1. Unit: `CensusTokenManager` (renovación/expiración), `CensoExpansionService` (capas), `CensoMatchService` (dedup/cobertura/bonus país), `DecisionService` (motivo NO GO, snapshot).
- [ ] D2. E2E match real contra Census (licitación ciberseguridad `1425525-3-LE26`): 200 con personas + cobertura.
- [ ] D3. E2E decisión: GO/NO GO persiste y se lee en la ficha.
- [ ] D4. Validación control-agent (qa-validation) + commit final de la fase.
