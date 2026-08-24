# Constitution — CU010 Mercado Público

**Proyecto:** PRJ-010 Plataforma de Gestión de Mercado Público  
**Organización:** TIVIT Digital — Equipo IA  
**Owner confirmado por HITL:** Matías Méndez / Equipo Digital TIVIT (no es Manuel Aliaga, autor del framework)  
**Versión:** 1.0.0 — 24.08.2026  
**Framework:** TIVIT Foundry 4.3.0 — 9 artículos

---

## Article 1: Core Principles

1. **Contrato primero:** Toda feature nueva parte de `api-first-spec` o `feature-spec`. Sin spec no hay código.
2. **Fuente única de verdad:** `src/MPM.Api/Database/Scripts` manda sobre `docs/migrations`. `specs/` manda sobre `docs/cu010_*.txt`.
3. **Brownfield seguro:** Nunca romper datos de prod. Migraciones idempotentes, `CONCURRENTLY` en índices grandes, rollback documentado.
4. **No romper lo que funciona:** Optimizar N+1, bundle o seguridad sin cambiar comportamiento visible salvo que el spec lo pida.

**Phase -1 Gate:** Antes de cualquier código, verificar que el diseño respeta los 4 principios. Si viola uno, rediseñar.

## Article 2: Stack & Dependencies

- **Backend:** .NET 8 (C#) + Dapper + Npgsql + PostgreSQL 16 + pgvector — target `net8.0`, SDK 8.0.x obligatorio local y en CI.
- **Frontend:** React 18 + Vite 5 + Ant Design 5 + TanStack Query 5 + SignalR 8.
- **Infra:** Docker Compose local, GCP Cloud Run + Cloud SQL + GCS + Memorystore en prod, Terraform futuro.
- **AI:** Vertex AI Gemini 2.5 Pro via ADC + switch a Qwen 3.7 G4 via OpenAI-compatible (tabla `system_ai_provider`).
- **Library-First:** Nueva capacidad transversal (formateo CLP, validación UTM, parsing UTM) va a `MPM.Shared` antes de usarse en un módulo.
- **Dependency Gate:** Nueva dependencia requiere justificación + 2 alternativas + licencia. AngleSharp solo para parsing HTML de actas.

## Article 3: CLI Interface

- Toda feature batch expone CLI: `dotnet run -- --help --json --dry-run`.
- CLI valida inputs y falla con `exit 1` + `ApiResponse.Fail` JSON.

## Article 4: Test-First (NON-NEGOTIABLE)

1. Escribir test que define el comportamiento esperado.
2. Ejecutarlo — debe fallar (red).
3. Escribir mínimo código para pasar (green).
4. Refactorizar manteniendo verde.
- Todo PR requiere tests de happy path, edge y error. Corren en CI (backend `dotnet test`, frontend `vitest`, e2e `playwright`). En local pueden correr via Docker si falta SDK.

## Article 5: Integration Testing

- Operaciones contra Postgres/Redis via `TestContainers` o stack Docker real (`mpm-db`, `mpm-redis`).
- Todo `usp_*` tiene test de contrato: parámetros, paginación, orden, soft-delete.

## Article 6: Observability

- Todo servicio expone `/health/*` con `status`, `timestamp`, trazabilidad.
- Logs estructurados con `correlationId`. Métricas via OpenTelemetry futuro.
- Llamadas LLM trazadas con `modelo_usado` persistido. Costos via Langfuse cuando se habilite.

## Article 7: Versioning & Breaking Changes

- SemVer, CHANGELOG.md por release, API versionada `/api/v1`.
- Breaking change avisa una versión antes con deprecación.

## Article 8: Simplicity

- Preferir simple a clever. Función ≤30 líneas, archivo ≤500 líneas.
- DRY con moderación. Duplicación clara mejor que abstracción prematura.

## Article 9: Anti-Abstraction

- No abstraer para futuro hipotético. Una implementación concreta antes de una interfaz.
- Interfaces solo con 2+ implementaciones productivas.

---

## Forbidden Patterns

1. **God Classes** >10 métodos públicos o >500 líneas.
2. **Magic Numbers** sin constante nombrada (UTM, montos, timeouts).
3. **Silent Failures** `catch {}` sin log.
4. **Hardcoded Secrets** en código o `.env` commiteado.
5. **SELECT *** en prod — columnas explícitas.
6. **N+1 Queries** — batch o `IN` siempre.
7. **Lógica de negocio en Controllers** — va en Services.

## Phase -1 Gates (pre-código)

| Gate | Pregunta | Condición de paso |
|------|----------|-------------------|
| Simplicity | ¿Es lo más simple? | Sin abstracción innecesaria |
| Anti-Abstraction | ¿Cada abstracción se justifica hoy? | 2+ usos reales |
| Integration-First | ¿Cómo se testea e2e? | Plan de test existe |
| Library-First | ¿Puede ser librería reutilizable? | Extraído a Shared si aplica |
| Test-First | ¿Tests definidos antes? | Esqueleto rojo existe |

## Constitución — validación

Esta constitución es inmutable salvo ADR con aprobación del owner. Cualquier violación detiene el PR.
