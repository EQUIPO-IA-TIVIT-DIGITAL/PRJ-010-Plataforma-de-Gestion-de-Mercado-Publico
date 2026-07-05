# Feature Specification: Fase 15 — Pricing Intelligence

**Feature Branch**: `012-fase15-pricing-intelligence`
**Created**: 2026-06-24
**Status**: Planned
**Semana estimada**: Semana 11 (Noviembre 2026)
**Impacto**: Alto | **Complejidad**: Alta | **Depende de**: Fase 11

---

## Contexto

Una de las principales causas de pérdida en licitaciones es el precio. Los análisis Gemini ya extraen los montos ofertados por cada competidor y el monto adjudicado. Esta fase agrega una capa de análisis de pricing: ¿cuánto cobran los competidores? ¿cuál es el precio óptimo para ganar sin dejar dinero sobre la mesa? ¿cómo varía el precio por tipo de servicio y organismo?

---

## User Stories

### User Story 1 — Benchmarking de precios por categoría (Priority: P1)

El equipo de pricing de TIVIT necesita saber qué precio están pagando los organismos del estado por servicios similares a los que TIVIT ofrece.

**Why this priority**: Sin datos de mercado, TIVIT fija precios a ciegas. El benchmarking permite posicionarse estratégicamente.

**Independent Test**: El usuario selecciona la categoría "Cloud Computing / IaaS" y ve: precio promedio adjudicado, rango (min-max), precio típico de Sonda vs. otros competidores, y tendencia de precios en los últimos 12 meses.

**Acceptance Scenarios**:
1. **Given** hay 10+ licitaciones analizadas en una categoría, **When** el usuario selecciona la categoría, **Then** ve el precio promedio de mercado, el precio de TIVIT ofertado y la diferencia porcentual.
2. **Given** el benchmarking de una categoría, **When** el usuario filtra por organismo, **Then** los precios se recalculan para ese organismo específico (el Ministerio de Educación paga diferente que FFAA).
3. **Given** la vista de competidor (Fase 11), **When** el usuario ve el perfil de Sonda, **Then** aparece una sección de pricing con el rango de precios que Sonda ofrece por categoría.

---

### User Story 2 — Recomendación de precio para nueva propuesta (Priority: P1)

El analista que va a cotizar una propuesta necesita saber qué precio tiene la mayor probabilidad de ser competitivo sin ser el más barato.

**Why this priority**: El objetivo no es siempre ganar por precio — sino ganar con el mejor margen posible. Un precio óptimo es el más alto que aún es competitivo.

**Independent Test**: Dado el tipo de licitación, organismo y descripción del servicio, el sistema recomienda un rango de precio con probabilidad de adjudicación estimada para cada valor.

**Acceptance Scenarios**:
1. **Given** una licitación en el pipeline, **When** el usuario abre "Recomendador de precio", **Then** ve una curva precio vs. probabilidad de adjudicación con el punto óptimo marcado.
2. **Given** el rango recomendado, **When** el usuario ajusta el precio manualmente, **Then** el sistema actualiza la probabilidad estimada y muestra los competidores que quedarían debajo de ese precio.

---

## Funcionalidades principales

- Motor de pricing en `MPM.Modules.Analisis` con extracción de montos desde JSONs de análisis
- Dashboard de benchmarking: precio promedio por categoría, por organismo, por año
- Gráfico de curva precio vs. probabilidad de adjudicación
- Historial de precios ofertados por competidor por categoría
- Recomendador de precio para nueva propuesta (input: tipo + organismo + monto referencial)
- Alerta de "precio fuera de rango": si la propuesta actual es X% más cara que el mercado
- Exportación de tabla de precios competitivos para presentar a gerencia

## Definición de Hecho

- [ ] Extracción automática de precios desde análisis Gemini existentes
- [ ] Dashboard de benchmarking por categoría y organismo
- [ ] Recomendador de precio con curva probabilidad
- [ ] Historial de precios de competidores
- [ ] Alerta de precio fuera de rango de mercado
- [ ] Exportación Excel de benchmarking
