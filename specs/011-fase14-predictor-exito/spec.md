# Feature Specification: Fase 14 — Predictor de Éxito

**Feature Branch**: `011-fase14-predictor-exito`
**Created**: 2026-06-24
**Status**: Planned
**Semana estimada**: Semana 10 (Octubre 2026)
**Impacto**: Alto | **Complejidad**: Muy Alta | **Depende de**: Fase 11

---

## Contexto

Con suficiente historial de licitaciones analizadas, es posible entrenar un modelo que prediga la probabilidad de que TIVIT gane una licitación específica, basado en: tipo de licitación, organismo comprador, rango de monto, competidores históricos en ese espacio y fortalezas/debilidades de TIVIT detectadas por los análisis Gemini. Esta predicción orienta la decisión de participar o no.

---

## User Stories

### User Story 1 — Score de probabilidad de ganar (Priority: P1)

El Gerente Comercial necesita priorizar en cuáles de las 20 licitaciones activas vale la pena invertir el esfuerzo de preparar propuesta.

**Why this priority**: Preparar una propuesta cuesta 40-80 horas de trabajo. Un mal filtrado implica desperdicio de recursos en licitaciones no ganables.

**Independent Test**: Dado una nueva licitación en el pipeline, el predictor muestra un score 0-100% con los 3 factores positivos y 3 factores negativos más relevantes.

**Acceptance Scenarios**:
1. **Given** una licitación en "En Evaluación" del pipeline, **When** el usuario hace clic en "Predecir probabilidad", **Then** el sistema responde con un score y las razones (ej: "Fuerte: ganamos 3 de 4 licitaciones similares en MINSAL. Débil: Sonda tiene ventaja de precio en este rango").
2. **Given** el score calculado, **When** el usuario cambia algún parámetro (ej: baja el precio ofertado 10%), **Then** el predictor recalcula el score en tiempo real.
3. **Given** una predicción de < 20%, **When** el gerente la ve, **Then** el sistema sugiere "Evaluar no participar" con el fundamento.

---

### User Story 2 — Historial de precisión del predictor (Priority: P2)

El equipo de calidad quiere saber qué tan confiable es el predictor antes de tomar decisiones basadas en él.

**Why this priority**: Un predictor sin track record conocido genera desconfianza. Mostrar la precisión histórica construye confianza gradualmente.

**Independent Test**: El dashboard del predictor muestra que en las últimas 10 licitaciones, el score fue correcto en 7 (precisión 70%).

**Acceptance Scenarios**:
1. **Given** hay 20 licitaciones con predicción registrada y resultado conocido, **When** el usuario abre el panel de métricas, **Then** ve la precisión histórica desglosada por rango de score.

---

## Funcionalidades principales

- Motor de scoring en `MPM.Modules.Analisis` usando reglas + embeddings de Gemini
- Factores del score: historial organismo, tipo licitación, rango monto, competidores esperados, capacidad técnica TIVIT inferida de análisis previos
- Panel de explicación: top factores positivos y negativos con peso relativo
- Simulador: "¿Qué pasa si bajo el precio X%?" → recálculo en tiempo real
- Umbral configurable: recomendación automática (participar / evaluar / no participar)
- Historial de predicciones vs. resultados reales con métricas de precisión
- Alerta automática cuando un predictor detecta oportunidad de > 70% que no está en el pipeline

## Definición de Hecho

- [ ] Score 0-100% calculado para cualquier licitación en pipeline
- [ ] Explicación de factores positivos/negativos
- [ ] Simulador de cambio de precio funcional
- [ ] Historial de predicciones vs. resultados reales
- [ ] Alerta de oportunidades de alta probabilidad
- [ ] Panel de métricas de precisión del modelo
