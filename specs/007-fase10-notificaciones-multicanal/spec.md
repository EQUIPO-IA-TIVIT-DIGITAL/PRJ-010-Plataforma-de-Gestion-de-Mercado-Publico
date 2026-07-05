# Feature Specification: Fase 10 — Notificaciones Multicanal

**Feature Branch**: `007-fase10-notificaciones-multicanal`
**Created**: 2026-06-24
**Status**: Planned
**Semana estimada**: Semana 6 (Agosto 2026)
**Impacto**: Medio | **Complejidad**: Media | **Depende de**: Fase 6

---

## Contexto

El módulo de notificaciones actual solo funciona dentro del sistema (in-app). Esta fase extiende la entrega a canales externos: Email y WhatsApp Business API. El usuario elige qué tipo de eventos le llegan por cada canal.

---

## User Stories

### User Story 1 — Notificaciones por email (Priority: P1)

El analista quiere recibir alertas de nuevas licitaciones en su email corporativo, incluso si no tiene el sistema abierto.

**Why this priority**: Los usuarios no tienen el sistema siempre abierto. El email garantiza que nunca se pierdan una alerta crítica.

**Independent Test**: Al dispararse una alerta de keyword, el sistema envía un email al analista en menos de 5 minutos con el nombre, monto y link de la licitación.

**Acceptance Scenarios**:
1. **Given** el usuario tiene email configurado y alertas activas, **When** se dispara una alerta, **Then** recibe email con subject "[MPM] Nueva licitación: [nombre]" y botón "Ver licitación".
2. **Given** el usuario quiere solo notificaciones de alta prioridad por email, **When** configura sus preferencias, **Then** solo los eventos marcados como P1 generan emails.
3. **Given** muchas alertas en poco tiempo, **When** se acumulan más de 5 en 1 hora, **Then** se envía un resumen único en lugar de 5 emails individuales.

---

### User Story 2 — Notificaciones por WhatsApp (Priority: P2)

El gerente Francisco quiere recibir en su WhatsApp personal cuando aparezca una licitación de más de $500M.

**Why this priority**: WhatsApp tiene tasa de apertura 98% vs. 20% del email. Para alertas críticas, asegura visibilidad inmediata.

**Independent Test**: Al detectarse una licitación con monto > $500M que coincide con una alerta, el sistema envía un mensaje de WhatsApp al número configurado.

**Acceptance Scenarios**:
1. **Given** el usuario configuró su número de WhatsApp, **When** se dispara una alerta de alta prioridad, **Then** recibe un mensaje WhatsApp con el nombre y link de la licitación.
2. **Given** el usuario responde "PAUSAR" al mensaje, **Then** las alertas de WhatsApp se pausan por 24 horas.

---

## Funcionalidades principales

- Página de preferencias de notificación: por tipo de evento, elegir canal (in-app / email / WhatsApp)
- Proveedor email: integración con servicio SMTP existente o SendGrid
- Proveedor WhatsApp: Twilio WhatsApp Business API o Meta Cloud API
- Template de email HTML responsive con branding TIVIT
- Anti-spam: rate limiting y agrupación de notificaciones
- Configuración de número de WhatsApp con verificación OTP
- Opt-out respetado por canal

## Definición de Hecho

- [ ] Preferencias de canal configurables por usuario
- [ ] Emails enviados en menos de 5 min desde el evento
- [ ] WhatsApp enviado para alertas configuradas como WA
- [ ] Anti-spam con agrupación horaria
- [ ] Templates HTML branded para email
- [ ] Opt-out funcional por canal
