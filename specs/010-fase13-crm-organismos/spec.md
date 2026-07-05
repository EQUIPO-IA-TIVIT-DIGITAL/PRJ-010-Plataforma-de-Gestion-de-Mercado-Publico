# Feature Specification: Fase 13 — CRM de Organismos Compradores

**Feature Branch**: `010-fase13-crm-organismos`
**Created**: 2026-06-24
**Status**: Planned
**Semana estimada**: Semana 9 (Octubre 2026)
**Impacto**: Medio | **Complejidad**: Alta | **Depende de**: Fase 7

---

## Contexto

Cada organismo del estado (ministerios, municipios, FFAA, hospitales) tiene sus propios patrones de compra, contactos clave y relación histórica con TIVIT. Esta fase crea una ficha CRM por organismo que consolida: contactos, historial de licitaciones ganadas/perdidas, notas de relación comercial y alertas de próximas licitaciones esperadas.

---

## User Stories

### User Story 1 — Ficha de organismo comprador (Priority: P1)

El ejecutivo comercial que maneja la cuenta del Ministerio de Salud necesita ver en un solo lugar todo el historial de TIVIT con ese organismo antes de una reunión.

**Why this priority**: El conocimiento de cuentas está en la cabeza de las personas. Si alguien sale, se pierde. Centralizarlo protege el activo comercial.

**Independent Test**: El usuario abre la ficha del "Ministerio de Salud" y ve: monto adjudicado histórico, win rate en ese organismo, contactos registrados, notas de reuniones y próximas licitaciones en el pipeline.

**Acceptance Scenarios**:
1. **Given** el sistema tiene análisis de licitaciones de un organismo, **When** el usuario abre su ficha, **Then** ve KPIs calculados automáticamente: # de licitaciones, ganadas, monto total adjudicado, win rate.
2. **Given** la ficha de un organismo, **When** el usuario agrega un contacto con nombre, cargo y email, **Then** el contacto queda asociado al organismo y aparece en el directorio de contactos global.
3. **Given** la ficha, **When** el usuario agrega una nota de reunión con fecha, **Then** el historial de notas aparece cronológicamente y es buscable.

---

### User Story 2 — Directorio de contactos del estado (Priority: P2)

El equipo comercial necesita encontrar rápidamente quién es el encargado de compras de un organismo específico.

**Why this priority**: Las reuniones de preventa requieren identificar al decisor. Sin un directorio, cada búsqueda es desde cero.

**Independent Test**: El usuario busca "Ministerio de Educación" en el directorio y ve todos los contactos registrados con su cargo y última actividad.

**Acceptance Scenarios**:
1. **Given** el directorio tiene 50 contactos, **When** el usuario busca por nombre o cargo, **Then** los resultados se filtran en tiempo real.
2. **Given** un contacto en el directorio, **When** el usuario hace clic, **Then** ve su ficha completa con todos los organismos asociados y el historial de interacciones.

---

## Funcionalidades principales

- Módulo `MPM.Modules.CRM` con entidades: Organismo, Contacto, Nota, Interacción
- Ficha de organismo: KPIs auto-calculados desde análisis existentes, mapa de licitaciones, contactos, notas
- Directorio de contactos: búsqueda por nombre/cargo/organismo, exportación
- Línea de tiempo de interacciones: reuniones, emails, licitaciones presentadas
- Alertas de "Próximas licitaciones esperadas" basadas en historial (organismo suele licitar en Q4)
- Enriquecimiento automático: organismo detectado en análisis → ficha creada automáticamente
- Integración con Pipeline de Oportunidades (Fase 7): organismo de la licitación vincula a ficha CRM

## Definición de Hecho

- [ ] Ficha de organismo con KPIs auto-calculados
- [ ] CRUD de contactos vinculados a organismos
- [ ] Historial de notas con fecha y autor
- [ ] Directorio global de contactos con búsqueda
- [ ] Integración automática con licitaciones analizadas
- [ ] Alertas de próximas licitaciones por organismo
