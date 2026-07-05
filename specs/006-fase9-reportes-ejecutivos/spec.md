# Feature Specification: Fase 9 — Reportes Ejecutivos Automáticos

**Feature Branch**: `006-fase9-reportes-ejecutivos`
**Created**: 2026-06-24
**Status**: Planned
**Semana estimada**: Semana 5 (Agosto 2026)
**Impacto**: Alto | **Complejidad**: Media | **Depende de**: Fase 7

---

## Contexto

Cada jueves, el equipo tiene reunión con directivos (Leonardo, Pablo, Fernando, Francesco). Actualmente Matías prepara manualmente un resumen de la situación comercial. Esta fase genera ese reporte automáticamente cada miércoles a las 20:00 y lo envía por email a los destinatarios configurados.

---

## User Stories

### User Story 1 — Reporte ejecutivo semanal automático (Priority: P1)

El Gerente Francisco necesita recibir cada miércoles en su email un resumen del estado del pipeline comercial, sin que nadie tenga que prepararlo manualmente.

**Why this priority**: La preparación manual del reporte toma 1-2 horas semanales. Automatizarlo libera tiempo y garantiza consistencia.

**Independent Test**: Cada miércoles a las 20:00 se genera y envía un email a los destinatarios configurados con el PDF del reporte adjunto. Sin intervención humana.

**Acceptance Scenarios**:
1. **Given** es miércoles 20:00, **When** el cron job ejecuta, **Then** se genera un PDF con los KPIs actualizados y se envía por email.
2. **Given** el email llega, **When** Francisco lo abre, **Then** ve: pipeline actual, licitaciones ganadas/perdidas del mes, win rate, próximos cierres y comparativa vs. mes anterior.
3. **Given** el admin configura la lista de destinatarios, **When** guarda, **Then** el próximo reporte llega a todos los correos configurados.

---

### User Story 2 — Reporte descargable on-demand (Priority: P1)

Un directivo que perdió el email necesita generar el reporte en el momento desde el sistema.

**Why this priority**: Complementa el envío automático para consultas adhoc.

**Independent Test**: El admin hace clic en "Generar reporte ahora" y en 10 segundos descarga un PDF con los datos actuales.

**Acceptance Scenarios**:
1. **Given** el usuario está en la página de reportes, **When** selecciona rango de fechas y hace clic en "Exportar PDF", **Then** el PDF se descarga con los datos del período seleccionado.
2. **Given** el usuario prefiere Excel, **When** selecciona "Exportar Excel", **Then** recibe un .xlsx con tablas de datos crudos para análisis propio.

---

## Funcionalidades principales

- Cron job semanal (miércoles 20:00 Chile) para generación y envío
- Generación de PDF con: portada, KPIs del pipeline, tabla de oportunidades activas, gráfico win rate, top competidores, próximos cierres
- Generación de Excel con datos crudos del período
- Envío por email con PDF adjunto y resumen en cuerpo del email
- Página `/reportes` con historial de reportes generados y descarga on-demand
- Configuración de destinatarios y periodicidad desde panel admin
- Librería de generación: QuestPDF (.NET) para PDF, ClosedXML para Excel

## Definición de Hecho

- [ ] Cron job genera reporte cada miércoles 20:00
- [ ] PDF incluye todos los KPIs acordados con directivos
- [ ] Email se envía a lista de destinatarios configurable
- [ ] Descarga on-demand funcional desde frontend
- [ ] Exportación Excel con datos crudos
- [ ] Historial de reportes accesible en `/reportes`
