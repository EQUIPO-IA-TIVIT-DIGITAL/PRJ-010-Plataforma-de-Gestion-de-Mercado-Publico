# Governance — CU010 Mercado Público

**Proyecto:** PRJ-010 — Plataforma de Gestión de Mercado Público  
**Framework:** TIVIT Foundry 4.3.0 — Hybrid mode  
**Owner (HITL confirmado):** Matías Méndez / Equipo Digital TIVIT  
**Última actualización:** 24.08.2026 — rama `036-flujo-comercial-ofertas`

## 1. Reglas del framework

- **Obligatorias (mandatory):** `framework-governance`, `framework-architecture`, `framework-security`, `framework-data-memory-compliance`, `api-first-spec`, `database-modeling`, `api-contracts`, `error-handling`, `authentication`, `authorization`, `unit-testing`, `integration-testing`, `playwright`, `ci-cd`, `observabilidad`, `governance-constitution`.
- **Ninguna excepción sin registro aquí.** Si se omite una mandatory, documentar `Bloqueo` + `Owner` + `Fecha revisión`.

## 2. Stack certificado y enforcement

| Capa | Stack | Regla |
|------|-------|-------|
| Backend | .NET 8 + Dapper | `TargetFramework net8.0` — SDK 8.0.x obligatorio. Docker es fallback válido si falta runtime local (E01). |
| Frontend | React 18 Vite | `vitest` para unit, `playwright` para e2e. Bundle budget <600KB gz por chunk. |
| DB | PostgreSQL 16 | Migraciones en `src/MPM.Api/Database/Scripts` son fuente. `docs/migrations` es solo guía prod. |
| AI | Gemini ADC + Qwen switch | `system_ai_provider` tabla manda sobre env var. |

## 3. Excepciones temporales registradas

| ID | Regla violada | Justificación | Owner | Revisión |
|----|---------------|---------------|-------|----------|
| EXC-001 | Secretos en `.env` plano hasta Sprint R2 | CSMS/Terraform pendiente — Sprint 4 original bloqueado por infra GCP. `.env` no se commitea, solo `.env.example` con placeholders. | Matías Méndez | 30.09.2026 |
| EXC-002 | Sin Terraform/K8s manifests | Infra GCP Huawei/GCP en borrador v6 — se implementa en R2 | Equipo DevOps | 30.09.2026 |
| EXC-003 | Sin observabilidad completa | OpenTelemetry/Langfuse postergado a R2 — se traza `modelo_usado` como mínimo viable | Equipo IA | 15.09.2026 |

## 4. Deuda técnica registrada (no bloqueante pero trackeada)

| ID | Descripción | Origen | Plan |
|----|-------------|--------|------|
| DEBT-001 | `V155__Licitaciones_Filtro_Monto_Minimo_Maximo.sql` en `docs/migrations` es copia documental de `V151` con SQL dinámico inseguro. Es deuda de troubleshooting de la feature monto. Se marca como `DEBT` y se corrige en R0 para que sea NoOp idempotente seguro. | E02/E07 | R0.2 — NoOp seguro |
| DEBT-002 | `tools/scraper-mp-v2` con 2000+ ficheros `debug_*.js`/`test_*.js` dejados de sesiones de hardening | E12 | R0.4 — mover a `scratch/` |
| DEBT-003 | Bundle frontend 2.3MB sin `manualChunks` | E09 | R3.3 |
| DEBT-004 | `ROADMAP.md` y `cu010_*.txt` divergentes | E19 | R4.2 — `specs/` como fuente |

## 5. Gates y validación

- **Test Gate por capa:** todo cambio con lógica backend pasa `dotnet test`, con lógica frontend pasa `vitest` o `playwright` según corresponda.
- **Security Gate:** `dotnet list package --vulnerable` 0 + `check-secrets` pass.
- **Constitution Gate:** valida Articles 1-9 antes de cada PR.

## 6. Proceso HITL pendiente

Ver `.workflow/state.json` `hitl_pending`. Bloquea `track1 T7-T9` y `track2 T11`. No bloquea R0-R2 pero debe resolverse antes de implementar Go/No-Go por tipo (E15).

## 7. Referencias

- `docs/constitution.md` — 9 artículos
- `VERSIONS.md` — matriz de compatibilidad
- `.workflow/state.json` — estado del workflow
- `docs/artifacts/progress.html` — dashboard (regenerado en R4.2)
