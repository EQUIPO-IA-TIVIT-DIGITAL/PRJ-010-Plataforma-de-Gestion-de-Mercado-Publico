# Implementation Plan: Migración Gemini 2.5 Pro → Qwen 3.7 (G4)

**Branch**: `033-migracion-qwen-g4` | **Date**: 2026-08-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/033-migracion-qwen-g4/spec.md`

## Summary

El sistema (MPM) usa Google Gemini en 4 puntos: análisis de PDFs (`gemini-2.5-pro`), análisis de competidores (`gemini-2.5-pro`), búsqueda semántica (`gemini-2.5-flash-lite`) y sinónimos de alertas (`gemini-2.5-flash`), todos acoplados a Vertex AI. El objetivo: eliminar la dependencia de Google migrando los 4 usos a Qwen 3.7 G4 (cuantizado 4-bit, servido vía API OpenAI-compatible en una URL entregada por el equipo), con un **switch en la UI para el super admin** que alterna entre gcloud y qwen (la solución se mudará a infraestructura privada). Enfoque técnico: interfaz `ILlmClient` en `MPM.Shared` (la `VertexGeminiClient` actual pasa a ser la implementación `gemini`), cliente nuevo `OpenAiCompatClient`, resolución dinámica del proveedor por request (`LlmClientResolver` con configuración persistida en tabla `system_ai_provider` + fallback a env vars), tabla + SPs nuevos, endpoint de administración solo SuperAdmin, harness de benchmark (umbral ≥ 90% + revisión manual) y runbook de cutover/rollback vía switch. Sin cambios de esquema en tablas de análisis, sin cambios de frontend salvo la nueva página de administración.

## Technical Context

**Language/Version**: C# 12, .NET 8 (Nullable + ImplicitUsings)

**Primary Dependencies**: Dapper 2.x, Npgsql 8.x (sin cambios). Sin SDKs nuevos de IA: HttpClient crudo + System.Text.Json (consistente con `VertexGeminiClient` actual). `GoogleAdcTokenProvider` existente se mantiene para el camino `gemini`.

**Storage**: PostgreSQL 15+ (sin cambios de esquema; `modelo_usado` ya existe como varchar). GCS sigue siendo usado por el camino Gemini (`fileData`).

**Testing**: xUnit + Moq + FluentAssertions (unit por módulo en `tests/MPM.Modules.Xxx.Tests`), integración con `WebApplicationFactory` en `tests/MPM.Tests`, E2E Playwright en `src/mpm-web/e2e/` (sin cambios esperados).

**Target Platform**: Cloud Run (producción actual, camino Gemini) + servidor de inferencia Qwen G4 (on-premise TIVIT o endpoint externo — pendiente clarificación FR-012; para desarrollo/pruebas: vLLM u Ollama en Docker).

**Project Type**: Modular Monolith (.NET 8) + React/Vite frontend — migración solo afecta backend.

**Performance Goals**: Latencia de análisis síncrono dentro de rango medible en benchmark (p50/p95); presupuesto de salida de 65536 tokens preservado; cero regresión en el camino Gemini.

**Constraints**: Constitución MPM: módulos independientes (comunicación solo vía MPM.Shared/MPM.Core), stored procedures + Dapper (sin ORM), secretos en CSMS nunca en repo, testing por capas obligatorio. `AI:Provider` con `gemini` como default. Sin cambios de contrato HTTP ni de BD.

**Scale/Scope**: 4 servicios de IA en 3 módulos + MPM.Shared; 1 cliente nuevo; 1 harness de benchmark; 1 runbook. Sin nuevas tablas ni endpoints.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Evaluación |
|-----------|-----------|
| I. Modular Monolith | ✅ Cumple: el refactor se queda dentro de módulos existentes + MPM.Shared (el cliente de IA ya vive ahí). La configuración del proveedor es infraestructura transversal → MPM.Core (como TenantMiddleware), controller en MPM.Api (como HealthController). No se crea módulo de dominio nuevo. |
| II. Stored Procedures First | ✅ Cumple: la nueva tabla se accede solo vía `usp_SystemConfig_ObtenerAiProvider` / `usp_SystemConfig_ActualizarAiProvider` (Dapper, sin ORM). |
| III. Migraciones embebidas | ✅ Cumple: 1 script nuevo `V130__Add_System_AI_Provider.sql` embebido + `DatabaseInitializer`. |
| IV. Multi-Tenancy por middleware | ✅ Cumple: `system_ai_provider` es config global (sin tenant_id, documentado); la auditoría usa `TenantContext` para `updated_by_*` (los controllers no leen claims directo). |
| V. Abstracción de Storage | ✅ Cumple: `IStorageService` intacto; el camino Gemini sigue usando GCS vía `fileData` como hoy. |
| VI. Real-Time SignalR | ✅ Cumple: sin cambios. |
| VII. Testing por capas | ✅ Cumple: unit tests para `ILlmClient`/`OpenAiCompatClient`/resolver/SP handler (proyectos de test), integración para el switch de proveedor y el endpoint de administración en `tests/MPM.Tests`, E2E del switch en `src/mpm-web/e2e/`. |

**Gate**: ✅ Aprobado. La única adición es una tabla de configuración transversal, cubierta por los mecanismos existentes (SPs + migración + auditoría). Sin violaciones que justificar en Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/033-migracion-qwen-g4/
├── plan.md              # Este archivo
├── research.md          # Decisiones D1–D9
├── data-model.md        # Tabla system_ai_provider + SPs (sin cambios en análisis)
├── quickstart.md        # Escenarios de validación
├── contracts/
│   ├── llm-client.md            # Contrato ILlmClient + resolución dinámica
│   ├── openai-compat-api.md     # Contrato del endpoint Qwen (URL entregada)
│   └── ai-provider-admin-api.md # Contrato del switch super admin (GET/PUT)
├── tasks.md             # Fases de implementación
└── checklists/requirements.md   # Checklist de calidad de la spec
```

### Source Code (repository root)

```text
src/
├── MPM.Shared/
│   └── Services/
│       ├── ILlmClient.cs              # NUEVO: contrato neutral al proveedor
│       ├── LlmModels.cs               # NUEVO: LlmRequest/LlmResult/LlmPart (texto, pdf, fileUri)
│       ├── LlmClientResolver.cs       # NUEVO: resuelve el cliente por request (BD > env > default, cache TTL 30s)
│       ├── VertexGeminiClient.cs      # EXISTENTE: pasa a implementar ILlmClient (adaptador)
│       └── OpenAiCompatClient.cs      # NUEVO: implementación OpenAI-compatible (Qwen)
├── MPM.Core/
│   ├── SystemConfig/
│   │   ├── SystemConfigService.cs     # NUEVO: lógica de config del proveedor (get/update + invalidación cache)
│   │   └── AiProviderSettings.cs      # NUEVO: DTO + precedencia BD/env/default
│   └── Data/
│       └── SystemConfigData.cs        # NUEVO: SPs usp_SystemConfig_* vía DbConnectionFactory (Dapper)
├── MPM.Modules.Analisis/
│   └── Services/
│       ├── GeminiService.cs           # MODIFICADO: usa LlmClientResolver
│       ├── AnalisisService.cs         # MODIFICADO: inyección de resolver
│       └── AnalisisBackgroundService.cs # MODIFICADO: resolución de cliente por request
├── MPM.Modules.Competidores/
│   └── Services/
│       ├── CompetidorGeminiService.cs # MODIFICADO: usa LlmClientResolver
│       └── CompetidorAnalysisService.cs # MODIFICADO: inyección
├── MPM.Modules.Licitaciones/
│   └── Services/
│       └── ConsultaSemanticaService.cs # MODIFICADO: elimina HTTP crudo → resolver
├── MPM.Modules.Alertas/
│   └── Services/
│       └── SinonimosIaService.cs      # MODIFICADO: elimina HTTP crudo → resolver
├── MPM.Api/
│   ├── Controllers/
│   │   └── SystemConfigController.cs  # NUEVO: GET/PUT /api/system/ai-provider (rol SuperAdmin)
│   ├── Database/Scripts/
│   │   └── V130__Add_System_AI_Provider.sql  # NUEVO: tabla + unique parcial
│   └── Program.cs                     # MODIFICADO: registra clientes por key, resolver, SystemConfigService
├── mpm-web/src/
│   ├── pages/AdminConfiguracionIaPage.tsx   # NUEVO: switch gcloud/qwen (solo SuperAdmin)
│   ├── hooks/useSystemConfig.ts              # NUEVO: useQuery/useMutation del contrato admin
│   ├── types/systemConfig.ts                 # NUEVO: tipos del contrato
│   └── components/AppLayout.tsx              # MODIFICADO: item de menú visible solo SuperAdmin
└── tools/
    └── BenchmarkLlm/                  # NUEVO: harness de benchmark (US2)
        └── Program.cs                 # consola: compara proveedores, emite informe go/no-go

tests/
├── MPM.Shared.Tests/                  # NUEVO: OpenAiCompatClient + resolver (TTL, fallback, precedencia)
├── MPM.Core.Tests/                    # NUEVO: SystemConfigData/Service (SPs mockeados o integración)
├── MPM.Modules.Analisis.Tests/        # MODIFICADO: tests con resolver mockeado
├── MPM.Tests/                         # MODIFICADO: integración switch proveedor + endpoint admin (403/200)
└── mpm-web/e2e/                       # MODIFICADO: spec del switch super admin

docs/
└── infraestructura-cu010-v6.md        # NUEVO (o update de v5): estado Qwen + switch + runbook rollback
```

**Structure Decision**: Estructura de Modular Monolith existente, sin opciones. El refactor sigue la distribución de responsabilidades ya vigente: contrato y clientes en `MPM.Shared.Services` (como `VertexGeminiClient` hoy), prompts/parsers en los servicios de cada módulo, infraestructura transversal (config del proveedor) en `MPM.Core`, composición y endpoints de sistema en `MPM.Api` (como `HealthController`), UI de administración como página nueva solo SuperAdmin. El harness de benchmark vive en `tools/` (no es dominio de negocio ni parte del producto).

## Complexity Tracking

> Sin violaciones de constitución — sección no aplica (gate aprobado sin excepciones).
