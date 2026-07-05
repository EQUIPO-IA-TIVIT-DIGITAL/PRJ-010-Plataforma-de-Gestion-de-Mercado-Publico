# Feature Specification: Fase 17 — Gestión Documental de Propuestas

**Feature Branch**: `014-fase17-gestion-documental`
**Created**: 2026-06-24
**Status**: Planned
**Semana estimada**: Semana 13 (Diciembre 2026)
**Impacto**: Medio | **Complejidad**: Alta | **Depende de**: Fase 7

---

## Contexto

Cada propuesta de licitación requiere reunir, personalizar y organizar documentos: currículums de profesionales, certificados de experiencia, presentaciones corporativas, modelos de contrato, etc. Actualmente estos documentos están dispersos en emails y carpetas compartidas. Esta fase centraliza el repositorio de plantillas reutilizables y gestiona los documentos de cada propuesta.

---

## User Stories

### User Story 1 — Repositorio de plantillas reutilizables (Priority: P1)

El analista necesita acceder rápidamente a la última versión del "Currículum Corporativo TIVIT" o el "Certificado de Experiencia en Cloud" sin buscarlo en emails.

**Why this priority**: El 40% del tiempo de preparación de propuestas se dedica a buscar y adaptar documentos existentes. Un repositorio central lo elimina.

**Independent Test**: El analista busca "currículum" en el repositorio y encuentra el documento correcto en menos de 10 segundos, descargando la última versión actualizada.

**Acceptance Scenarios**:
1. **Given** el repositorio tiene documentos categorizados, **When** el analista busca por nombre o etiqueta, **Then** los resultados aparecen con nombre, categoría, fecha de última actualización y quién lo modificó.
2. **Given** un documento en el repositorio, **When** el admin sube una nueva versión, **Then** la versión anterior queda en el historial descargable y la nueva es la que aparece por defecto.
3. **Given** una propuesta activa en el pipeline, **When** el analista vincula documentos del repositorio, **Then** la propuesta muestra qué documentos están incluidos con su versión.

---

### User Story 2 — Checklist de completitud de propuesta (Priority: P1)

El jefe de propuesta necesita saber en tiempo real qué porcentaje de los documentos requeridos ya están listos.

**Why this priority**: La causa más frecuente de descalificación es documentación incompleta (como el caso CLOUD AZURE). Un semáforo de completitud previene esto.

**Independent Test**: La propuesta para la licitación X muestra 80% de completitud (8/10 documentos) con los 2 faltantes resaltados en rojo.

**Acceptance Scenarios**:
1. **Given** el checklist de la propuesta tiene 10 ítems y 8 están marcados como "Listo", **When** el usuario abre la vista, **Then** ve 80% de barra de progreso y los 2 pendientes destacados.
2. **Given** faltan documentos 48h antes del cierre, **When** el sistema ejecuta el check, **Then** el responsable recibe alerta urgente con lista exacta de faltantes.

---

## Funcionalidades principales

- Módulo `MPM.Modules.Documentos` con entidades: Plantilla, Documento, Versión, Tag
- Repositorio con categorías: Presentación Corporativa / Experiencia / Legal / Técnico / Financiero
- Control de versiones: historial completo descargable, comparación de versiones
- Vinculación documento-propuesta (pipeline Fase 7)
- Checklist de completitud con % y semáforo visual
- Búsqueda full-text por nombre y etiquetas
- Almacenamiento en GCS (mismo `IStorageService` existente)
- Permisos: solo admins suben/modifican plantillas; analistas las usan

## Definición de Hecho

- [ ] Repositorio con upload, descarga y búsqueda
- [ ] Control de versiones con historial
- [ ] Vinculación documentos-propuesta funcional
- [ ] Checklist de completitud con % calculado
- [ ] Alertas de documentos faltantes a 48h del cierre
- [ ] Permisos diferenciados admin/analista
