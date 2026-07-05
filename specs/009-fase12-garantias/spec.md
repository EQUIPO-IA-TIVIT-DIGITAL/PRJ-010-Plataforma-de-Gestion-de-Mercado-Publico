# Feature Specification: Fase 12 — Gestión de Garantías

**Feature Branch**: `009-fase12-garantias`
**Created**: 2026-06-24
**Status**: Planned
**Semana estimada**: Semana 8 (Septiembre 2026)
**Impacto**: Medio | **Complejidad**: Media | **Depende de**: Fase 7

---

## Contexto

En las licitaciones chilenas existen dos tipos de garantías obligatorias: la "Garantía de Seriedad de la Oferta" (se entrega al presentar) y la "Garantía de Fiel Cumplimiento" (se entrega al adjudicar). Su seguimiento es manual y un vencimiento inadvertido puede generar pérdidas millonarias o descalificación. Esta fase centraliza su gestión con alertas automáticas.

---

## User Stories

### User Story 1 — Registro y seguimiento de garantías (Priority: P1)

El equipo de finanzas necesita saber en todo momento qué garantías están vigentes, cuánto capital tienen inmovilizado y cuándo vence cada una.

**Why this priority**: Una garantía vencida en una licitación activa genera descalificación automática. El costo es la licitación completa.

**Independent Test**: El sistema muestra todas las garantías activas con días para vencimiento, monto, banco emisor y licitación asociada. Una garantía que vence en 7 días genera alerta automática.

**Acceptance Scenarios**:
1. **Given** una oportunidad en el pipeline llega a "En cotización", **When** el usuario registra la garantía de seriedad, **Then** aparece en el panel de garantías con monto, banco, número de documento y fecha de vencimiento.
2. **Given** una garantía vence en 14 días, **When** el sistema ejecuta el check diario, **Then** el responsable recibe notificación in-app y email de alerta.
3. **Given** la licitación fue perdida, **When** el usuario marca la garantía como "Devuelta", **Then** desaparece del panel activo y pasa al historial con la fecha de devolución.

---

### User Story 2 — Dashboard de capital comprometido (Priority: P2)

El CFO necesita saber cuánto capital tiene TIVIT inmovilizado en garantías en cualquier momento.

**Why this priority**: Las garantías bancarias tienen costo financiero. Sin visibilidad, se puede sobre-comprometer capital.

**Independent Test**: El dashboard muestra el total en CLP de garantías activas, desglosado por tipo (seriedad/cumplimiento) y banco.

**Acceptance Scenarios**:
1. **Given** hay 5 garantías activas por un total de $150M CLP, **When** el CFO abre el dashboard, **Then** ve el total comprometido y el desglose por banco y tipo.

---

## Funcionalidades principales

- Módulo `MPM.Modules.Garantias` con CRUD completo
- Tipos: Garantía de Seriedad de Oferta / Garantía de Fiel Cumplimiento
- Campos: monto (CLP/USD/UF), banco emisor, número de documento, fecha emisión, fecha vencimiento, estado, licitación vinculada
- Dashboard: total comprometido, próximas a vencer (semáforo), historial
- Alertas automáticas: 30 días, 14 días, 7 días antes del vencimiento
- Vinculación con oportunidades del pipeline (Fase 7)
- Exportación Excel para área financiera

## Definición de Hecho

- [ ] CRUD de garantías funcional
- [ ] Vinculación con oportunidades del pipeline
- [ ] Alertas en 30/14/7 días antes del vencimiento
- [ ] Dashboard de capital comprometido
- [ ] Estados: Vigente / Por vencer / Vencida / Devuelta / Ejecutada
- [ ] Exportación Excel para finanzas
