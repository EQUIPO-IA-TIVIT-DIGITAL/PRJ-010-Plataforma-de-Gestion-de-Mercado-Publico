# Implementation Plan: Fase 6 — Alertas Inteligentes por Palabras Clave

**Branch**: `003-fase6-alertas-keywords` | **Status**: PLANIFICADO — listo para implementar (`tasks.md` generado)
**Spec**: [spec.md](./spec.md) | **Semana**: N2 — entrega de esta semana (demo jueves 9 de julio, 7:59 a.m.)
**Actualizado**: 2026-07-06 — research.md, data-model.md, contracts/, quickstart.md generados; alcance ampliado con User Story 5 (Telegram)

---

## Summary

Nuevo módulo `MPM.Modules.Alertas` que permite a los usuarios configurar reglas de alerta por palabras clave, monto y tipo de licitación. El motor de matching se ejecuta en cada ciclo de sync de licitaciones, expande cada keyword a sinónimos/conceptos relacionados vía IA (precalculado al crear/editar la regla, no en cada ciclo — ver research.md §2), y genera notificaciones in-app con un resumen enriquecido (requisitos, competidores, presupuesto, forma de pago, multas, señal de renovación/proveedor actual) enrutadas a los account managers de gobierno. **Nuevo 2026-07-06**: además envía la misma alerta a un bot de Telegram (canal adicional, no reemplaza el in-app), y expone un endpoint para disparar una alerta de prueba sobre una licitación real, pensado para la demo del jueves.

---

## Technical Context

**Lenguaje**: .NET 8 + React 18 + TypeScript
**IA**: Gemini (reutiliza `HttpClient` directo a `generativelanguage.googleapis.com`, mismo patrón que `GeminiService` de `MPM.Modules.Analisis`, sin SDK nuevo) — para expansión de sinónimos (una vez por regla) y para el resumen liviano cuando no hay análisis de documentos aún.
**Notificaciones**: in-app vía módulo `MPM.Modules.Notificaciones` existente + **nuevo**: Telegram Bot API vía `HttpClient` directo (`POST https://api.telegram.org/bot{token}/sendMessage`), sin SDK de terceros.
**Storage**: PostgreSQL — nuevas tablas `alertas_reglas`, `alertas_disparadas`, `alertas_destinatarios` (ver data-model.md).
**Background Service**: motor de matching se invoca desde `SyncEngineService` existente al final de cada ciclo (no un nuevo `IHostedService` con su propio Timer — evita un segundo proceso de fondo compitiendo por recursos, y garantiza que las alertas se evalúan sobre licitaciones ya persistidas del mismo ciclo).
**Migración**: `V079__Create_Alertas.sql` (V078 ya la usa `016-extraccion-documentos-api`, implementada el mismo día).
**Estimación**: 1.5 semanas original + Telegram/demo agregados 2026-07-06, pero la entrega parcial (User Stories 1, 2, 3) debe estar demostrable el jueves 9 de julio — ver "Estrategia para el jueves" abajo. **Complejidad**: Media-Alta.

---

## Module Structure

**Nuevo módulo**: `MPM.Modules.Alertas`

```text
src/MPM.Modules.Alertas/
├── Controllers/
│   └── AlertasController.cs            ← CRUD + toggle + historial + probar (ver contracts/alertas-api.md)
├── Services/
│   ├── AlertasService.cs               ← Lógica de negocio, CRUD
│   ├── SinonimosIaService.cs           ← Llama a Gemini para expandir keyword a sinónimos (research.md §2)
│   ├── AlertasMatchingService.cs       ← Motor de matching, invocado desde SyncEngineService
│   ├── AlertaEnriquecimientoService.cs ← Genera resumen (reusa análisis existente o metadatos, research.md §3)
│   └── TelegramNotificationService.cs  ← NUEVO: envío a Telegram Bot API, con try/catch aislado
├── Data/
│   ├── AlertasHandler.cs               ← Dapper queries (reglas, disparadas, destinatarios)
│   └── AlertasStoredProcedures.cs      ← Constantes SP (usp_Alertas_*)
├── Models/
│   └── AlertasDtos.cs                  ← ReglaAlertaDto, AlertaDisparadaDto, ResumenEnriquecidoDto, DestinatarioDto
└── ModuleRegistration.cs               ← AddAlertasModule()

src/MPM.Api/Database/Scripts/
└── V079__Create_Alertas.sql            ← Tablas (alertas_reglas, alertas_disparadas, alertas_destinatarios) + SPs

src/mpm-web/src/
├── pages/AlertasPage.tsx               ← Lista + formulario de reglas + botón "probar" (demo)
└── hooks/useAlertas.ts                 ← TanStack Query hooks
```

**Integración con `SyncEngineService`** (Licitaciones): al final de `EjecutarCicloUnaVezAsync`/`DoWorkAsync`, invoca `AlertasMatchingService` sobre las licitaciones nuevas/actualizadas de ese ciclo — llamada cross-module vía interfaz inyectada (Principio I: los módulos no se referencian entre sí directamente salvo `MPM.Shared`/`MPM.Core`; `AlertasMatchingService` se expone a través de una interfaz registrada en DI que `MPM.Api` conecta, igual que `IAnalisisBackgroundService`).

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | Módulo `MPM.Modules.Alertas` independiente, `AddAlertasModule()`; integración con Licitaciones vía interfaz inyectada, no referencia directa de proyecto |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_Alertas_*` para todo acceso a BD, sin ORM |
| **III. Migraciones SQL** | ✅ Aplicar | `V079__Create_Alertas.sql`, siguiente número libre confirmado |
| **IV. Multi-Tenancy** | ✅ Aplicar | `usuario_id` en `alertas_reglas`/`alertas_destinatarios`; TenantContext inyectado en controllers |
| **VI. Real-Time via SignalR** | ✅ Reusa | La notificación in-app sigue el mecanismo existente de `MPM.Modules.Notificaciones`; Telegram es un canal adicional fuera de SignalR (HTTP saliente propio) |

---

## Estrategia para el jueves (demo)

Dado que la demo es el 9 de julio y el alcance completo es 1.5 semanas, se prioriza para el jueves:
1. User Story 1 (crear alertas, notificación in-app) — **imprescindible**.
2. User Story 3 (sinónimos IA) — **imprescindible**, es el diferenciador pedido por el cliente.
3. User Story 5 escenario 3 (endpoint "probar") — **imprescindible para poder demostrar sin esperar una licitación real nueva**.
4. User Story 5 escenarios 1-2 (Telegram) — deseable, pedido por Manuel, pero si el tiempo no alcanza se puede demostrar con el bot enviando manualmente vía Postman contra el endpoint de Telegram como fallback de demo.
5. User Story 4 (resumen enriquecido completo) y User Story 2 (panel de gestión completo) — pueden quedar parciales si el tiempo aprieta, ya que no son lo que se pidió mostrar primero según la reunión del 2026-07-06 (Manuel pidió ver "el módulo de alertas" funcionando, no cada detalle).

## Artefactos

- [x] `research.md` — decisiones de matching, sinónimos IA, resumen enriquecido, Telegram, demo
- [x] `data-model.md` — `alertas_reglas`, `alertas_disparadas`, `alertas_destinatarios`
- [x] `contracts/alertas-api.md` — endpoints REST completos
- [x] `quickstart.md` — 6 escenarios de validación
- [x] `tasks.md` — generado 2026-07-06: 45 tareas en 9 fases, priorizadas explícitamente para llegar demostrable al jueves (Setup → Foundational → US1 → US3 → US5 parcial/demo → US4 → US5 completo/Telegram → US2 → Polish)
