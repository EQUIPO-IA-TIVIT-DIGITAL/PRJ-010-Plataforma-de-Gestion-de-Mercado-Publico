# Implementation Plan: Fase 10 — Notificaciones Multicanal

**Branch**: `007-fase10-notificaciones-multicanal` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 6 (Agosto 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Extensión del módulo `MPM.Modules.Notificaciones` existente para entregar notificaciones por Email (SMTP existente) y WhatsApp (Twilio o Meta Cloud API). El usuario configura sus preferencias de canal por tipo de evento desde su perfil.

---

## Technical Context

**Extensión del módulo existente**: `MPM.Modules.Notificaciones`
**Dependencias nuevas**:
- Email: reutiliza `IEmailService` existente del módulo Auth
- WhatsApp: `Twilio` NuGet package (Twilio WhatsApp Business API)
**Anti-spam**: tabla `notificaciones_enviadas` para tracking de deduplicación
**Estimación**: 1 semana | **Complejidad**: Media

---

## Module Structure

**Extensión del módulo existente** `MPM.Modules.Notificaciones`:

```text
src/MPM.Modules.Notificaciones/
├── Services/
│   ├── EmailNotificacionService.cs    ← Nuevo: entrega por email
│   ├── WhatsAppNotificacionService.cs ← Nuevo: entrega por WhatsApp (Twilio)
│   └── NotificacionDispatchService.cs ← Nuevo: router por preferencias
├── Models/
│   └── PreferenciasNotificacionDto.cs ← Nuevo
└── (resto sin cambios)

src/MPM.Api/Database/Scripts/
└── V079__Add_notificaciones_preferencias.sql

src/mpm-web/src/
├── pages/PreferenciasPage.tsx         ← Sección notificaciones
└── components/NotificacionCanalPicker.tsx
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | Extensión del módulo existente |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_Notificaciones_Preferencias_*` |
| **III. Migraciones SQL** | ✅ Aplicar | V079 |
| **IV. Multi-Tenancy** | ✅ Sin violación | Preferencias por `usuario_id` |

---

## Artefactos pendientes

- [ ] `research.md` — Twilio vs. Meta Cloud API para WhatsApp, costo por mensaje
- [ ] `data-model.md` — PreferenciasNotificacion, canalesdisponibles
- [ ] `quickstart.md`
- [ ] `tasks.md` — generado con `/speckit-tasks`
