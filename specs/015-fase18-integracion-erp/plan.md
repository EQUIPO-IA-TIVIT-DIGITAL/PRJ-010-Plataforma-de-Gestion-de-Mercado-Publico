# Implementation Plan: Fase 18 — Integración ERP (SAP / Oracle)

**Branch**: `015-fase18-integracion-erp` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 14 (Diciembre 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Nuevo módulo `MPM.Integrations.ERP` con adaptadores configurables para distintos sistemas ERP. Al marcar una oportunidad como "Ganada" en el pipeline (Fase 7), MPM dispara un webhook al ERP configurado para crear el proyecto automáticamente. El módulo incluye cola de reintentos, log de integraciones y modo simulación para testing.

---

## Technical Context

**Nuevo módulo**: `MPM.Integrations.ERP`
**Patrón**: Adaptador (interfaz `IERPAdapter` con implementaciones por sistema)
**Sistemas soportados v1**: SAP RFC/SOAP, API REST genérica, Oracle EBS
**Queue**: Tabla `erp_cola_envios` con estado y reintentos (no RabbitMQ en v1)
**Estimación**: 1 semana | **Complejidad**: Muy Alta

---

## Module Structure

```text
src/MPM.Integrations.ERP/
├── Controllers/
│   ├── ERPConfigController.cs              ← Configuración de la integración
│   └── ERPWebhookController.cs             ← Inbound: recibe actualizaciones del ERP
├── Services/
│   ├── ERPDispatchService.cs               ← Orquesta envío al ERP
│   ├── ERPQueueService.cs                  ← BackgroundService: procesa cola de reintentos
│   └── Adapters/
│       ├── IERPAdapter.cs                  ← Interfaz
│       ├── SAPAdapter.cs                   ← Implementación SAP
│       └── GenericoRestAdapter.cs          ← Implementación REST genérica
├── Data/
│   ├── ERPHandler.cs
│   └── ERPStoredProcedures.cs
├── Models/
│   └── ERPDtos.cs
└── ModuleRegistration.cs

src/MPM.Api/Database/Scripts/
└── V087__Create_ERP_Integration.sql        ← erp_configuracion, erp_cola_envios, erp_log

src/mpm-web/src/
├── pages/ERPConfigPage.tsx                 ← Config + historial de integraciones
└── hooks/useERPIntegration.ts
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | `AddERPIntegrationModule()` independiente |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_ERP_*` |
| **III. Migraciones SQL** | ✅ Aplicar | V087 |
| **IV. Multi-Tenancy** | ✅ Aplicar | Configuración ERP por `tenant_id` |
| **Seguridad** | ⚠️ Revisar | Credenciales ERP almacenadas cifradas o en Secret Manager |

---

## Artefactos pendientes

- [ ] `research.md` — protocolos ERP: SAP RFC vs. REST, autenticación ERP, cifrado de credenciales
- [ ] `data-model.md` — ERPConfiguracion, ColaEnvio, LogIntegracion
- [ ] `contracts/erp-webhook-api.md`
- [ ] `quickstart.md` — escenario simulación end-to-end
- [ ] `tasks.md` — generado con `/speckit-tasks`
