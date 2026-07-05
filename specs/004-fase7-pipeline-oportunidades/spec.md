# Feature Specification: Fase 7 — Pipeline de Oportunidades

**Feature Branch**: `004-fase7-pipeline-oportunidades`
**Created**: 2026-06-24
**Status**: Planned
**Semana estimada**: Semana 4 (Julio-Agosto 2026) — desplazada una semana el 2026-07-03 por la repriorización que insertó `018-buscador-inteligente-nl` en la Semana 3 (ver `specs/ROADMAP.md`)
**Impacto**: Alto | **Complejidad**: Alta | **Depende de**: Fase 5

**Nota 2026-07-03**: La reunión "[CU010] - Revisión de Alcance" (2026-07-01) confirmó que el "motor de validación de completitud de ofertas antes de enviarlas" pedido por Francisco Lopez Balart corresponde a la User Story 2 de esta fase (checklist de documentos con % de completitud) y que debe priorizarse recién después de cerrar Análisis histórico (`017`, hecho) y Alertas (`003`). No requiere un spec nuevo.

---

## Contexto

Cuando TIVIT decide participar en una licitación, el proceso de preparación de la propuesta es complejo: múltiples personas, documentos, fechas límite y decisiones. Esta fase implementa un tablero Kanban donde cada licitación de interés avanza por etapas (Detectada → En evaluación → En cotización → Presentada → Ganada/Perdida), con responsable asignado, fecha límite y estado de documentos.

---

## User Stories

### User Story 1 — Tablero Kanban de oportunidades (Priority: P1)

El Gerente Comercial necesita ver de un vistazo en qué licitaciones está trabajando el equipo, quién es responsable y cuál es el estado de cada una.

**Why this priority**: Sin visibilidad del pipeline, se pierden licitaciones por falta de seguimiento o duplicación de esfuerzo.

**Independent Test**: Una licitación pasa de "Detectada" a "En cotización" con un responsable asignado. El gerente ve el cambio en tiempo real en el tablero sin necesidad de consultar a nadie.

**Acceptance Scenarios**:
1. **Given** una licitación en la lista, **When** el usuario hace clic en "Agregar al pipeline", **Then** aparece en la columna "Detectada" del Kanban con su fecha de cierre visible.
2. **Given** una tarjeta en el Kanban, **When** se arrastra a otra columna, **Then** el estado se actualiza y el responsable recibe notificación.
3. **Given** una oportunidad en "En cotización", **When** pasa la fecha de cierre sin haber avanzado, **Then** la tarjeta se marca visualmente como urgente.

---

### User Story 2 — Detalle de oportunidad con checklist (Priority: P1)

El analista asignado necesita saber exactamente qué documentos faltan y qué tareas quedan pendientes para presentar la propuesta.

**Why this priority**: La mayoría de las descalificaciones son por documentos faltantes — exactamente el problema que encontramos con TIVIT en el análisis CLOUD AZURE.

**Independent Test**: El analista abre una oportunidad, ve la lista de documentos requeridos extraídos del análisis de bases, marca los completados y el sistema calcula el % de completitud.

**Acceptance Scenarios**:
1. **Given** una oportunidad con análisis de bases disponible, **When** se abre el detalle, **Then** el checklist de documentos se pre-populó desde el análisis IA.
2. **Given** el checklist con ítems incompletos, **When** quedan menos de 48h para el cierre, **Then** el sistema envía recordatorio al responsable.

---

### User Story 3 — Resultado y lecciones aprendidas (Priority: P2)

Después de cada licitación, el equipo necesita registrar el resultado y las razones, para mejorar en el futuro.

**Why this priority**: Sin este registro, los mismos errores se repiten licitación tras licitación.

**Independent Test**: Al marcar una oportunidad como "Perdida", el sistema pide motivo y lo registra para el dashboard ejecutivo.

**Acceptance Scenarios**:
1. **Given** una oportunidad en "Presentada", **When** se registra el resultado (ganada/perdida + motivo), **Then** la información alimenta el dashboard ejecutivo comparativo.

---

## Funcionalidades principales

- Página `/pipeline` con tablero Kanban drag-and-drop
- 6 columnas: Detectada / En Evaluación / En Cotización / Presentada / Ganada / Perdida
- Tarjeta por oportunidad: nombre licitación, responsable, días al cierre, % completitud
- Panel de detalle: descripción, documentos requeridos (checklist), notas, historial de cambios
- Asignación de responsable con notificación
- Alertas automáticas por vencimiento de fechas
- Registro de resultado con campo "motivo de pérdida" categorizado

## Definición de Hecho

- [ ] Tablero Kanban funcional con drag-and-drop
- [ ] CRUD de oportunidades completo
- [ ] Checklist de documentos editable
- [ ] Notificaciones de asignación y vencimiento
- [ ] Resultado registrado alimenta dashboard ejecutivo
