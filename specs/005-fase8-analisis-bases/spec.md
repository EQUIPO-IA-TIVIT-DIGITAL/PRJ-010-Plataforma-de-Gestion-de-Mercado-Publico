# Feature Specification: Fase 8 — Análisis IA de Bases de Licitación

**Feature Branch**: `005-fase8-analisis-bases`
**Created**: 2026-06-24
**Status**: Planned
**Semana estimada**: Semana 4 (Agosto 2026)
**Impacto**: Alto | **Complejidad**: Media | **Depende de**: Fase 5

---

## Contexto

Las "Bases de Licitación" son el documento principal que define los requisitos de una licitación. Actualmente, un analista de TIVIT debe leerlas manualmente (a veces 50+ páginas) para entender qué se pide, qué documentos hay que presentar, cuáles son los criterios de evaluación y cuáles son los riesgos. Esta fase usa Gemini para automatizar esa extracción.

---

## User Stories

### User Story 1 — Extracción automática de requisitos de bases (Priority: P1)

Un analista recibe una alerta de licitación interesante y necesita saber en 2 minutos si TIVIT puede calificar, sin leer las 40 páginas de bases.

**Why this priority**: El mayor cuello de botella operativo es la lectura y comprensión de bases. Automatizarlo multiplica la capacidad del equipo.

**Independent Test**: Dado el PDF de bases de una licitación, el sistema genera en menos de 60 segundos un resumen estructurado con: objeto, monto estimado, requisitos técnicos, documentos exigidos, criterios de evaluación y fechas.

**Acceptance Scenarios**:
1. **Given** el scraper descargó el PDF de bases, **When** se dispara el análisis, **Then** Gemini genera un JSON estructurado con los campos clave.
2. **Given** el análisis completado, **When** el analista abre la licitación, **Then** ve una ficha con objeto, requisitos, criterios y riesgos en formato legible.
3. **Given** la ficha de análisis, **When** el analista hace clic en "¿Puede TIVIT calificar?", **Then** el sistema responde con un diagnóstico preliminar basado en los requisitos detectados.

---

### User Story 2 — Pre-populado de checklist desde bases (Priority: P1)

El Pipeline de oportunidades (Fase 7) necesita saber automáticamente qué documentos exige cada licitación.

**Why this priority**: Elimina el trabajo manual de leer bases para construir el checklist de propuesta.

**Independent Test**: Al agregar una licitación al pipeline, su checklist de documentos viene pre-completado desde el análisis de bases, sin intervención manual.

**Acceptance Scenarios**:
1. **Given** el análisis de bases contiene "Requisitos administrativos: Boleta de garantía, Personería del Representante Legal", **When** se crea la oportunidad en pipeline, **Then** esos ítems aparecen en el checklist automáticamente.

---

### User Story 3 — Detección de riesgos en bases (Priority: P2)

El equipo legal/comercial necesita saber antes de presentar si hay cláusulas inusuales o riesgosas en las bases.

**Why this priority**: Cláusulas de penalización agresivas o requisitos inalcanzables descubiertos tarde pueden costar más que no presentarse.

**Independent Test**: El análisis identifica y etiqueta como "Riesgo" cláusulas como "multa del 20% por incumplimiento de plazo" o "experiencia mínima de 10 años en el rubro".

**Acceptance Scenarios**:
1. **Given** las bases tienen cláusulas de penalización severas, **When** Gemini las analiza, **Then** se muestran como alertas de riesgo con nivel Alto/Medio/Bajo.

---

## Funcionalidades principales

- Descarga automática del PDF de bases junto al acta de evaluación (scraper)
- Nuevo tipo de análisis en `MPM.Modules.Analisis`: `tipo = 'bases'`
- Prompt Gemini especializado para extracción de: objeto, requisitos técnicos, documentos exigidos, criterios de evaluación, fechas, cláusulas de riesgo
- Ficha de bases en el frontend: resumen ejecutivo + secciones colapsables
- Botón "¿Califica TIVIT?" con respuesta contextual
- Integración con Pipeline de Oportunidades (Fase 7) para auto-populado de checklist

## Definición de Hecho

- [ ] Scraper descarga PDF de bases automáticamente
- [ ] Análisis Gemini genera JSON estructurado de bases
- [ ] Frontend muestra ficha de bases con riesgos destacados
- [ ] Integración con pipeline: checklist pre-populado
- [ ] "¿Puede TIVIT calificar?" responde con diagnóstico
