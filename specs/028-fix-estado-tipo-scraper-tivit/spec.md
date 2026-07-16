# Feature Specification: Corrección de Estado y Tipo en Licitaciones Scrapeadas de TIVIT

**Feature Branch**: `028-fix-estado-tipo-scraper-tivit`

**Created**: 2026-07-16

**Status**: Draft — parqueada, no es prioridad inmediata (ver `specs/027-catalogo-frontend-licitaciones-generales/` como prioridad actual)

**Input**: Durante la investigación de la spec 027 se encontró y se verificó con datos reales un defecto en las licitaciones de participación de TIVIT capturadas por el scraper (`tools/scraper-mp-v2/`): su estado y tipo reales quedan mal grabados en base de datos pese a que el scraper sí captura el dato correcto en crudo. Además se encontró un caso de workspace de análisis duplicado. El usuario pidió dejar esto documentado como spec aparte para revisar más adelante, priorizando primero la 027.

## Contexto — qué se verificó el 2026-07-16

- **Estado incorrecto pese a tener el dato real disponible**: de las 40 licitaciones de TIVIT capturadas por el scraper hasta ahora (identificables porque su `raw_data` contiene la clave `demandante`), **las 40** tienen una fecha de adjudicación real capturada en el dato crudo (`raw_data.fechas.adjudicacion`) — es decir, el portal las marca como adjudicadas — pero **ninguna (0 de 40)** quedó grabada con el código de estado real de "Adjudicada" en la tabla `licitaciones`. Ejemplo verificado: `897096-3-LR25` (créditos GCP para DRP de mercadopublico.cl) tiene `raw_data.fechas.adjudicacion = "23-05-2025 9:34:26"` pero su `codigo_estado` en base de datos corresponde a "Cerrada".
- **Tipo real capturado en crudo pero no reflejado en la columna final**: el mismo `raw_data` de esas 40 licitaciones contiene el texto real del tipo con su código entre paréntesis (ej. `"...Licitación Pública igual o superior a 5.000 UTM (LR)"`), pero la columna `tipo` de la tabla `licitaciones` quedó con el valor genérico `"Licitacion"` para las 40, sin excepción.
- **Causa raíz**: la función que traduce el texto de estado/tipo capturado por el scraper a un código de catálogo no reconoce el formato real de texto que usa el portal de Mercado Público hoy, y cae siempre al valor por defecto en vez de detectar el estado/tipo verdadero — pese a que el dato correcto ya está disponible en el mismo registro capturado.
- **Workspace de análisis duplicado**: la licitación `5240-37-LQ25` tiene dos workspaces de análisis asociados en vez de uno, sin que quede claro cuál es el vigente.
- **Nota — no es parte de este defecto**: 4 de las 40 licitaciones (todas contratos de créditos cloud: GCP/Azure) no tienen Acta de Evaluación entre sus adjuntos y por eso no generaron análisis automático — esto es comportamiento esperado del sistema, no un defecto, y no forma parte del alcance de esta spec.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ver el estado real de una licitación de TIVIT ya adjudicada (Priority: P1)

Un analista comercial revisa una licitación de TIVIT que ya fue adjudicada según el portal de Mercado Público, y la ve marcada como "Adjudicada" en MPM, no como "Cerrada" ni ningún otro estado incorrecto.

**Why this priority**: El estado "Adjudicada" es el dato más relevante para saber si TIVIT ganó o perdió una licitación — mostrarlo como "Cerrada" oculta esa información crítica y puede llevar a que el equipo comercial no se entere de una adjudicación real.

**Independent Test**: Tomar una licitación de TIVIT con fecha de adjudicación real conocida y confirmar que su estado en MPM coincide con "Adjudicada", cruzando contra el portal público de Mercado Público.

**Acceptance Scenarios**:

1. **Given** una licitación de TIVIT que el portal marca como adjudicada, **When** el scraper la captura, **Then** su estado en MPM queda como "Adjudicada", no como un valor por defecto incorrecto.
2. **Given** las 40 licitaciones ya afectadas por este defecto, **When** se aplica la corrección, **Then** cada una queda con su estado real verificable contra el dato crudo ya capturado (`fecha de adjudicación` presente implica estado "Adjudicada").

---

### User Story 2 - Ver el tipo real de una licitación de TIVIT (Priority: P1)

Un analista comercial ve el tipo real de una licitación de TIVIT (ej. "LR", "LQ", "LP") en vez del valor genérico actual, de forma consistente con el resto del sistema.

**Why this priority**: Sin el tipo real, estas licitaciones no aparecen bajo ningún filtro de tipo específico (ver spec 027), quedando efectivamente invisibles para cualquier análisis o filtro por tipo de compra.

**Independent Test**: Confirmar que el tipo mostrado para una licitación de TIVIT coincide con el código real visible en el texto capturado por el scraper para esa misma licitación.

**Acceptance Scenarios**:

1. **Given** una licitación de TIVIT cuyo texto capturado indica un código de tipo real (ej. "(LR)"), **When** se guarda en MPM, **Then** su tipo queda como ese código real, no como un valor genérico.
2. **Given** las 40 licitaciones ya afectadas, **When** se aplica la corrección, **Then** cada una queda con el tipo real extraído del dato ya capturado.

---

### User Story 3 - No ver workspaces de análisis duplicados (Priority: P3)

Un analista que abre el módulo de Análisis no encuentra más de un workspace para la misma licitación.

**Why this priority**: Es un caso puntual (una sola licitación detectada hasta ahora) que genera confusión menor, no pérdida de información — prioridad más baja que los dos defectos anteriores.

**Independent Test**: Revisar la licitación `5240-37-LQ25` en el módulo de Análisis y confirmar que aparece un único workspace.

**Acceptance Scenarios**:

1. **Given** una licitación con workspaces duplicados, **When** se aplica la corrección, **Then** queda un único workspace, conservando el análisis más completo o más reciente de los duplicados en vez de perder trabajo ya hecho.
2. **Given** el flujo normal de captura de una licitación nueva, **When** el scraper la procesa, **Then** nunca se crea más de un workspace para la misma licitación.

### Edge Cases

- ¿Qué pasa si el texto de estado o tipo que devuelve el portal cambia de formato en el futuro y la corrección deja de reconocerlo? El sistema no debe volver a caer silenciosamente en un valor por defecto sin indicarlo — debe quedar marcado de forma distinguible como no reconocido para que alguien lo revise.
- ¿Qué pasa con licitaciones de TIVIT nuevas que el scraper capture después de esta corrección? Deben quedar bien grabadas desde el primer momento, no solo las 40 ya existentes.
- Al fusionar los workspaces duplicados de una misma licitación, ¿qué pasa si ambos tienen análisis distintos ya completados? No debe perderse ningún análisis ya generado — debe quedar claro cuál se conserva como el vigente.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE asignar el código de estado real de una licitación capturada por el scraper a partir del dato ya disponible en la captura (incluyendo la presencia de fecha de adjudicación como señal de estado Adjudicada), no de un valor por defecto.
- **FR-002**: El sistema DEBE extraer y asignar el código de tipo real de una licitación capturada por el scraper a partir del texto ya disponible en la captura, no de un valor genérico.
- **FR-003**: El sistema DEBE corregir retroactivamente el estado y tipo de las 40 licitaciones ya afectadas, sin alterar ni perder ningún otro dato ya asociado a ellas (adjuntos, análisis, seguimiento).
- **FR-004**: El sistema NO DEBE crear más de un workspace de análisis para la misma licitación.
- **FR-005**: El sistema DEBE resolver el caso de workspace duplicado ya identificado (`5240-37-LQ25`), conservando el análisis más completo o más reciente sin pérdida de trabajo ya hecho.

### Key Entities

- **Licitación de participación TIVIT**: registro capturado por el scraper que debe reflejar su estado y tipo reales del portal, usando el mismo dato que el scraper ya captura correctamente en crudo.
- **Workspace de análisis**: debe existir como máximo uno por licitación.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de las licitaciones de TIVIT con fecha de adjudicación real capturada queda marcado con estado "Adjudicada" en MPM.
- **SC-002**: El 100% de las licitaciones de TIVIT con un código de tipo real identificable en el texto capturado queda marcado con ese código real, no con un valor genérico.
- **SC-003**: Cero licitaciones tienen más de un workspace de análisis asociado.

## Assumptions

- La corrección retroactiva de las 40 licitaciones se puede resolver reprocesando el dato crudo ya capturado (`raw_data`), sin necesidad de volver a scrapear el portal — dado que el dato correcto ya está ahí, solo mal traducido al guardar.
- Esta spec depende del mismo glosario de tipos de licitación que usa la spec `026-robustez-sincronizacion-tipos-reales` y `027-catalogo-frontend-licitaciones-generales` — no redefine esos significados.
- No se investigó si existen más casos de workspace duplicado además de `5240-37-LQ25` — si al implementar se encuentran más, se corrigen con el mismo mecanismo sin requerir una spec nueva.
