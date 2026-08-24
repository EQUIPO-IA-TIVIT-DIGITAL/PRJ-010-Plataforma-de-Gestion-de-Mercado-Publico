# Feature Spec: Go/No-Go condicionado al tipo de licitación

**Feature**: go-nogo-por-tipo
**Generado por**: feature-spec (agente design)
**Fecha**: 2026-08-20
**Origen**: Reunión con cliente 14-08-2026 — prometido como v1.1: "que el Go/No-Go considere EL TIPO DE LICITACIÓN".
**Superficie REST**: Ninguna nueva (reutiliza `POST` análisis comercial y decisión humana existentes). Por eso esta spec usa formato `feature-spec`, no `api-first-spec`.
**Estado**: Reglas por tipo propuestas para validación del cliente — marcadas `[HITL]`.

---

## 1. Scope

### Included

- La recomendación IA (`go_no_go` + `score_confianza` de `AnalisisComercialService`) pasa a **modularse por el tipo real de la licitación**, tomado del catálogo `tipos_licitacion` (V045/V108/V128) vía el campo `tipo` de la licitación analizada.
- Prompt parametrizado por tipo: instrucciones específicas de evaluación según grupo de tipo (ver §6).
- Nuevo objeto estructurado `modulacion_tipo` dentro de `resultado_json` (aditivo, no rompe `SanearYExtraer` ni consumidores actuales): registra tipo detectado/usado, regla aplicada y notas.
- Caso especial Convenio Marco (CO): la recomendación estándar no aplica; se instruye evaluación de ajuste a catálogo, dejando rastro explícito en `modulacion_tipo`.
- Visibilidad en UI: el panel de análisis comercial muestra el tipo de la licitación y la regla aplicada junto al badge Go/No-Go.

### Excluded

- Cambios al registro de decisión humana (`DecisionService`, DEC-R001..R011): la decisión sigue siendo go|no_go con motivo obligatorio en NO GO y snapshot IA intacto. El tipo modula la RECOMENDACIÓN, no la decisión.
- Reglas duras post-IA que sobrescriban el output del modelo (ej. forzar `no_go`) → diferido a v1.2 si el cliente lo pide tras ver v1; hoy se mantiene simple (prompt parametrizado).
- Re-análisis retroactivo masivo de licitaciones ya analizadas → los análisis existentes conservan su resultado sin modulación hasta que se re-ejecuten manualmente.
- Nuevos endpoints ni cambios de contrato REST → todo viaja dentro de `resultado_json` existente.

## 2. Actors & Triggers

| Actor | Rol |
|-------|-----|
| Usuario comercial (Account Manager) | Dispara el análisis comercial de una licitación y lee la recomendación en `AnalisisComercialPanel`. |
| Decisor (gerencia) | Toma la decisión formal go/no_go informado por la recomendación modulada. |
| Sistema (AnalisisComercialService) | Al procesar el análisis, resuelve el tipo de la licitación desde BD e inyecta las instrucciones por tipo en el prompt. |
| Modelo LLM | Aplica las instrucciones del tipo al producir `go_no_go`, `score_confianza` y `modulacion_tipo`. |

**Triggers**: click en "Analizar" / re-análisis de una licitación (flujo existente `IniciarAsync`). No hay trigger nuevo.

## 3. Data Touched

| Entidad | Lectura/Escritura | Detalle |
|---------|-------------------|---------|
| `licitaciones.tipo` | Lectura | Código del tipo (LE, LP, CO, TD...) proveniente de la sincronización. Fuente de verdad del tipo. |
| `tipos_licitacion` | Lectura | Catálogo V108+V128: nombre/descripción oficial del código. |
| `analisis_licitacion_comercial.resultado_json` | Escritura | Se agrega clave `modulacion_tipo` (objeto) al JSON que ya persiste. Columnas `go_no_go`/`score_confianza` sin cambios de esquema. |
| `decisiones_go_no_go.recomendacion_ia` (snapshot) | Lectura indirecta | Sin cambios: copia `go_no_go`/`score_confianza` como hoy (DEC-R005/R006). |

Sin migraciones de esquema en v1.

## 4. Behavior Spec

- Dado una licitación con `tipo = 'CO'` (Convenio Marco), cuando se ejecuta el análisis comercial, entonces la recomendación se produce bajo instrucciones de evaluación de catálogo y `resultado_json.modulacion_tipo.regla_aplicada = "convenio_marco_evaluacion_catalogo"`.
- Dado una licitación cuyo `tipo` NO existe en `tipos_licitacion` (código nuevo del portal aún no catalogado), cuando se ejecuta el análisis, entonces el prompt usa el grupo genérico "sin clasificar" y `modulacion_tipo.regla_aplicada = "generico_sin_clasificar"` — el análisis nunca falla por tipo desconocido.
- Dado una licitación con `tipo` NULL o vacío, cuando se ejecuta el análisis, entonces se aplica el grupo genérico y `modulacion_tipo.tipo_codigo = null`; el resto del análisis funciona idéntico a hoy.
- Dado cualquier tipo del grupo "pública" (LE/LP/LQ/LR), cuando el modelo emite su recomendación, entonces `modulacion_tipo.grupo_regla = "licitacion_publica"` y las instrucciones aplicadas son las de §6 para ese grupo.
- Dado un análisis completado antes de esta feature (sin `modulacion_tipo`), cuando la UI muestra el panel, entonces el badge Go/No-Go se renderiza igual que hoy y la sección de tipo muestra "no disponible (análisis previo)".
- Dado que el usuario toma la decisión formal, cuando se registra en `DecisionService`, entonces el snapshot de recomendación IA es exactamente el mismo valor que veía el usuario en el panel (sin reinterpretación por tipo en la capa de decisión).
- Dado un re-análisis de la misma licitación, cuando cambia el conjunto de documentos pero no el tipo, entonces la regla aplicada es la misma; si el tipo cambió en Mercado Público entre análisis, la nueva corrida usa el tipo vigente.

## 5. UI States

Pantalla única afectada: panel de análisis comercial (`AnalisisComercialPanel.tsx`).

| Estado | Comportamiento |
|--------|----------------|
| Loading | Sin cambio (spinner existente del análisis). |
| Success con `modulacion_tipo` | Junto al badge Go/No-Go: chip con nombre del tipo (ej. "Convenio Marco") y línea pequeña con la regla aplicada ("Evaluación de catálogo" / "Licitación pública ≤ umbral menor"...). |
| Success sin `modulacion_tipo` (análisis legacy) | Badge Go/No-Go normal; texto "Tipo: no disponible (análisis previo)". |
| Tipo desconocido/genérico | Chip "Sin clasificar" + nota de que la evaluación usó criterios generales. |
| Error | Sin cambio (manejo actual del panel). |
| Panel de decisión (`DecisionGoNoGoPanel.tsx`) | Sin cambios funcionales; opcionalmente muestra el mismo chip informativo de tipo (solo lectura). |

## 6. Business Rules

### Catálogo real de tipos (fuente: V108 + V128 — verificado en BD)

| Grupo de regla | Códigos | Tipos |
|----------------|---------|-------|
| licitacion_publica | LE, LP, LQ, LR | Pública Menor (<100 UTM), Media (100–1.000), Mayor (1.000–2.000), Grande (>2.000) |
| convenio_marco | CO | Convenio Marco |
| compra_agil | CA | Compra Ágil (≤30 UTM) |
| trato_directo | TD | Trato Directo |
| servicios | LS | Licitación de Servicios |
| obras | L, B, R, O | Obras Públicas / Suministros (códigos legados agrupados) |
| especiales | E, I, H, CI, DC | Especiales/Internacionales, Privada Media (H), Contrato Innovación, Diálogo Competitivo |
| generico_sin_clasificar | (fallback) | Tipo NULL, vacío o no catalogado |

> ⚠️ `[HITL]` El cliente mencionó "Licitación Pública ≤450 UTM". Ese umbral NO existe en el catálogo real (los cortes son 100/1.000/2.000 UTM según V108). Las reglas siguientes usan los umbrales reales del catálogo; validar con cliente antes de implementar.

### Reglas de modulación por tipo (propuesta v1 — todas `[HITL]`)

- **GO-T001 (licitacion_publica)**: Evaluar capacidad de respuesta formal completa (garantías, personal, certificaciones). En LR (>2.000 UTM) exigir evidencia fuerte de experiencia previa comparable; ante brechas, bajar score y sesgar a `no_go`. En LE (<100 UTM) ponderar si el esfuerzo de propuesta se justifica frente al monto.
- **GO-T002 (convenio_marco)**: La recomendación estándar NO aplica. Instruir al modelo: evaluar solo ajuste de la oferta del proveedor al catálogo (alcance, precio referencial, condiciones), expresar el resultado en el enum existente pero justificado en términos de catálogo, y setear `regla_aplicada = "convenio_marco_evaluacion_catalogo"`. La UI etiqueta estos casos distinto ("evaluación de catálogo", no "go/no-go").
- **GO-T003 (compra_agil)**: Ciclo corto y monto bajo: priorizar velocidad de respuesta y margen; penalizar procesos que exijan desarrollo a medida no reutilizable.
- **GO-T004 (trato_directo)**: Verificar causal legal de contratación directa citada en documentos; riesgo reputacional/compliance si la causal es débil → sesgo conservador.
- **GO-T005 (servicios)**: Ponderar experiencia en consultoría/servicios gestionados y perfil del equipo exigido.
- **GO-T006 (obras)**: Dominio ajeno al core de TIVIT (infraestructura): score base más bajo salvo componente tecnológico claro (ej. suministro de insumos complejos tech).
- **GO-T007 (especiales)**: E/I con financiamiento multilateral: revisar requisitos de elegibilidad; CI/DC: mecanismos nuevos de la reforma — evaluar encaje innovador; H (privada media): tratar como pública media.
- **GO-T008 (generico_sin_clasificar)**: Criterios generales actuales (comportamiento equivalente al prompt vigente), dejando `regla_aplicada = "generico_sin_clasificar"`.

### Reglas transversales

- **GO-R010**: El enum de `go_no_go` (strong_go/go/no_go/strong_no_go) NO cambia; ninguna regla introduce valores nuevos.
- **GO-R011**: `modulacion_tipo` es aditivo dentro de `resultado_json`: `{ "tipo_codigo": string|null, "tipo_nombre": string|null, "grupo_regla": string, "regla_aplicada": string, "notas": string|null }`. Su ausencia = análisis legacy.
- **GO-R012**: El tipo usado SIEMPRE proviene de `licitaciones.tipo` + catálogo; si el modelo detecta un tipo distinto en los documentos, lo reporta en `notas` pero no cambia la regla aplicada.
- **GO-R013**: Fallo al resolver el tipo (BD/catálogo) NO aborta el análisis: cae a `generico_sin_clasificar` y se loggea warning.
- **GO-R014**: La decisión humana (`DecisionService`) permanece invariante: motivo obligatorio en NO GO, snapshot tal cual (DEC-R001..R011).

## 7. Non-Goals

- No se modifican endpoints REST ni DTOs de respuesta HTTP (todo viaja en `resultado_json`).
- No se agregan columnas ni migraciones en v1.
- No se implementan reglas duras determinísticas post-IA (override del modelo) — candidatas a v1.2 con datos de calibración.
- No se re-calibran ni re-ejecutan análisis históricos.
- No se cambia el flujo de decisión humana ni sus notificaciones (DEC-R010 Fase 3 sigue en su track).
- No se expone configuración de reglas por tenant/admin — las reglas viven en código (prompt builder) hasta que el cliente valide su estabilidad.

## 8. Acceptance Criteria

- [ ] Analizar una licitación CO produce recomendación con `modulacion_tipo.regla_aplicada = "convenio_marco_evaluacion_catalogo"` y justificación en términos de catálogo visible en UI.
- [ ] Analizar una licitación LE/LP/LQ/LR aplica instrucciones del grupo pública (verificable en `modulacion_tipo.grupo_regla`).
- [ ] Una licitación con tipo NULL o código inexistente completa el análisis sin error usando el grupo genérico.
- [ ] `SanearYExtraer` sigue extrayendo `go_no_go`/`score_confianza`/`resumen_ejecutivo` de respuestas CON y SIN `modulacion_tipo` (tests unitarios cubren ambos).
- [ ] El panel muestra tipo + regla aplicada para análisis nuevos, y estado degradado elegante para legacy.
- [ ] Registrar una decisión go/no_go después de un análisis modulado persiste snapshot idéntico a la recomendación mostrada.
- [ ] Tests unitarios del prompt builder cubren: cada grupo de regla, fallback genérico, y tipo NULL.
- [ ] Ningún test existente de `AnalisisComercialService` ni `DecisionService` se rompe (regresión verde).

## Open questions (resolver antes de `tasks` de implementación fina)

1. `[HITL]` Validar con cliente las reglas GO-T001..T007 (especialmente el desajuste "450 UTM" vs umbrales reales del catálogo).
2. `[HITL]` Confirmar que para CO basta la modulación por prompt en v1 (sin bloqueo duro post-IA).
3. `[NEEDS CLARIFICATION]` ¿El chip de tipo debe aparecer también en el listado de licitaciones o solo en el panel de análisis? (Asumido: solo panel.)
