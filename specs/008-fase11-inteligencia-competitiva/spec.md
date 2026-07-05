# Feature Specification: Fase 11 — Inteligencia Competitiva Avanzada

**Feature Branch**: `008-fase11-inteligencia-competitiva`
**Created**: 2026-06-24
**Status**: Planned
**Semana estimada**: Semana 7 (Septiembre 2026)
**Impacto**: Alto | **Complejidad**: Alta | **Depende de**: Fase 8

---

## Contexto

El dashboard ejecutivo actual muestra el ranking de competidores basado en los análisis de licitaciones perdidas. Esta fase profundiza ese análisis: perfiles completos por competidor, análisis de patrones de victoria por tipo de licitación, organismo y rango de monto, y comparativa dinámica TIVIT vs. competidor seleccionado.

---

## User Stories

### User Story 1 — Perfil de competidor (Priority: P1)

Francisco necesita saber exactamente cómo compite Sonda: en qué tipo de licitaciones gana, a qué precios, con qué argumentos técnicos y frente a quién.

**Why this priority**: "¿Por qué Sonda vendió $80M más?" fue la pregunta que originó este sistema. Esta fase la responde con datos profundos.

**Independent Test**: El usuario selecciona "Sonda" en el ranking de competidores y ve: historial de victorias por año/categoría, precio promedio ofertado, puntaje técnico promedio, organismos donde más gana, y en qué licitaciones compitió vs. TIVIT.

**Acceptance Scenarios**:
1. **Given** Sonda aparece en el ranking, **When** el usuario hace clic en su nombre, **Then** ve su perfil completo con KPIs agregados de todos los análisis disponibles.
2. **Given** el perfil de Sonda, **When** el usuario filtra por "vs TIVIT", **Then** solo muestra los enfrentamientos directos con resultado y diferencia de puntaje.
3. **Given** el perfil, **When** el usuario selecciona el gráfico de tendencias, **Then** ve la evolución de monto adjudicado por trimestre.

---

### User Story 2 — Análisis de patrones de pérdida (Priority: P1)

El equipo estratégico necesita identificar en qué dimensiones pierde TIVIT sistemáticamente para mejorar las propuestas futuras.

**Why this priority**: Sin identificar los patrones, cada propuesta empieza de cero sin aprender del pasado.

**Independent Test**: El sistema muestra que el 70% de las pérdidas de TIVIT son por puntaje económico, no técnico — y que Sonda ofrece en promedio 15% más barato en licitaciones de infraestructura.

**Acceptance Scenarios**:
1. **Given** el histórico de 10+ análisis, **When** el usuario accede al panel de patrones, **Then** ve: razón de pérdida más frecuente (técnica/económica/documental), brecha promedio vs. ganador, y categoría donde TIVIT es más competitivo.
2. **Given** los patrones, **When** el usuario filtra por tipo de licitación, **Then** los KPIs se recalculan para ese subconjunto.

---

## Funcionalidades principales

- Página `/competidores/:nombre` con perfil completo de competidor
- Gráficos: victorias por trimestre, distribución por categoría, rango de montos, organismos favoritos
- Tabla de enfrentamientos directos con TIVIT: fecha, licitación, resultado, diferencia de puntaje
- Panel de patrones de pérdida: causa raíz más frecuente, brecha económica promedio, categorías fuertes/débiles de TIVIT
- Comparativa lado a lado: TIVIT vs. competidor seleccionado en todos los KPIs
- Filtros: año, tipo de licitación, rango de monto, organismo comprador
- Exportación a Excel del análisis

## Definición de Hecho

- [ ] Perfil de competidor con todos los KPIs calculados
- [ ] Tabla de enfrentamientos directos TIVIT vs. competidor
- [ ] Panel de patrones de pérdida con causa raíz
- [ ] Comparativa lado a lado funcional
- [ ] Filtros dinámicos aplicados a todos los gráficos
- [ ] Exportación Excel
