# Feature Specification: Frontend de Licitaciones Alineado al Catálogo Real de Tipos/Estados

**Feature Branch**: `027-catalogo-frontend-licitaciones-generales`

**Created**: 2026-07-16

**Status**: **Implementado y validado en vivo** (2026-07-16) — migración V108 aplicada, las 3 user stories confirmadas contra Docker real (filtro de Tipo con 17 códigos reales devolviendo resultados reales, ej. "LP" → 28.504 licitaciones; selector de Estado con exactamente 5 opciones sin duplicados; tabla sin columnas de Organismo/Monto/Items; detalle de licitación conservando esos datos cuando existen). Sin pendientes.

**Input**: Tras la migración a scraper v2 (`tools/scraper-mp-v2/`, ver `tools/scraper-mp/DEPRECATED.md`) y a la sincronización masiva diaria con tipos reales (spec `026-robustez-sincronizacion-tipos-reales`), el catálogo de Tipo y Estado en el frontend y las columnas del buscador de licitaciones quedaron desalineados con los datos reales que hoy tiene el sistema para las ~178 mil licitaciones generales (universo masivo sincronizado a diario, distinto de las licitaciones donde participa TIVIT, que sí están completas y no requieren cambios). El usuario pidió específicamente: (1) ajustar el combobox de Estado y de Tipo para reflejar los códigos reales, (2) quitar Monto e Items de la vista de licitaciones generales porque el dato no se detecta/recolecta para ellas, (3) retirar Organismo por el mismo motivo.

> **Corrección de alcance (2026-07-16)**: Una versión anterior de este documento asumía un defecto en el scraper de licitaciones de TIVIT (scraper v2). El usuario confirmó que ese pipeline ya fue validado extensamente y está fuera de alcance — el problema real está en cómo el frontend presenta el universo masivo de licitaciones generales, no en los datos de participación de TIVIT. Esta versión reemplaza el enfoque anterior.

## Contexto — qué se verificó el 2026-07-16

- **Catálogo de Tipo desalineado con los datos reales**: la tabla de catálogo de tipos de licitación en base de datos solo tiene 4 categorías genéricas (Licitación Pública / Trato Directo / Convenio Marco / Compra Ágil). El selector de Tipo del buscador se arma a partir de ese catálogo. Sin embargo, la columna real de tipo en las licitaciones generales contiene 16 valores distintos, casi todos códigos oficiales granulares del portal (LE, LP, L, LR, LQ, O, CO, R, B, E, I, H, LS, CI, DC) — de 178.391 licitaciones generales, solo **1** tiene el valor genérico que sí reconoce el catálogo actual. En la práctica, el selector de Tipo del buscador no sirve para filtrar el universo real de datos.
- **Glosario ya existe y está documentado**: la spec `026-robustez-sincronizacion-tipos-reales` ya define en su sección "Glosario de Tipos de Licitación (para uso en Frontend)" el significado oficial de estos códigos (LE, LP, LQ, LR, CO, CA, TD, LS, L/B/R, E/I) — esta spec reutiliza ese glosario como fuente, no lo redefine. Códigos observados en datos reales pero no cubiertos ahí (O, H, CI, DC) quedan pendientes de documentar.
- **Catálogo de Estado con códigos duplicados**: la tabla de catálogo de estados de licitación tiene códigos heredados de una versión anterior (1=Publicada, 2=Modificada, 3=Desierta, 4=Revocada) coexistiendo con los códigos reales vigentes (5=Publicada, 6=Cerrada, 7=Desierta, 8=Adjudicada, 15=Revocada). El selector de Estado del buscador se arma directamente de esa tabla, por lo que hoy puede mostrar "Publicada" dos veces, "Desierta" dos veces y "Revocada" dos veces, sin distinción visible de cuál es el vigente.
- **Monto, Items y Organismo prácticamente ausentes en el universo general**: de 178.391 licitaciones generales, **178.387 no tienen Organismo**, **178.391 no tienen Monto estimado** (el 100%), y **ninguna tiene ítems asociados**. Esto es un límite conocido y ya documentado del listado diario de la API oficial (spec 026: "el JSON de listado diario es extremadamente minimalista"), no un defecto nuevo — pero el buscador hoy muestra columnas de Organismo, Monto e Items que para prácticamente todas las filas quedan vacías o en cero, generando ruido visual y la falsa impresión de datos faltantes o rotos.
- **Fuera de alcance confirmado por el usuario**: las licitaciones donde participa TIVIT (recolectadas por el scraper, un universo mucho menor) sí tienen estos campos completos y no requieren ningún cambio — esta spec no las toca.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Filtrar licitaciones generales por su tipo real (Priority: P1)

Un analista comercial usa el selector de Tipo en el buscador de licitaciones y puede filtrar efectivamente por los tipos reales que existen en los datos (ej. "Licitación Pública Menor (LE)", "Convenio Marco (CO)"), en vez de un selector que hoy prácticamente no encuentra nada.

**Why this priority**: El selector de Tipo es una herramienta de filtrado central del buscador — que hoy no funcione sobre el 99.999% de los datos reales lo vuelve inútil, y es el hallazgo de mayor impacto de este documento.

**Independent Test**: Abrir el buscador de licitaciones, seleccionar cada opción del filtro de Tipo, y confirmar que cada una devuelve resultados reales (no una lista vacía) proporcionales a los volúmenes reales por tipo observados en los datos.

**Acceptance Scenarios**:

1. **Given** el selector de Tipo en el buscador, **When** el usuario lo abre, **Then** ve las opciones correspondientes a los códigos reales de tipo de licitación (LE, LP, LQ, LR, CO, CA, TD, LS, y el resto del glosario de la spec 026), con su nombre oficial legible, no el código crudo ni las 4 categorías genéricas actuales.
2. **Given** el usuario selecciona un tipo real (ej. "LP"), **When** aplica el filtro, **Then** el buscador devuelve las licitaciones cuyo tipo real corresponde a ese código.

---

### User Story 2 - Ver el estado real sin duplicados en el selector (Priority: P1)

Un analista comercial usa el selector de Estado y ve cada estado real una sola vez, sin duplicados confusos entre códigos vigentes y heredados.

**Why this priority**: Un selector con estados duplicados (dos "Publicada", dos "Desierta") genera duda sobre cuál usar y resultados de filtro potencialmente inconsistentes entre sí.

**Independent Test**: Abrir el selector de Estado y confirmar que cada nombre de estado aparece una sola vez, y que filtrar por cualquiera de ellos devuelve el universo completo de licitaciones en ese estado real.

**Acceptance Scenarios**:

1. **Given** el selector de Estado en el buscador, **When** el usuario lo abre, **Then** ve exactamente 5 opciones (Publicada, Cerrada, Desierta, Adjudicada, Revocada), sin duplicados.
2. **Given** el usuario filtra por un estado, **When** aplica el filtro, **Then** el resultado incluye todas las licitaciones generales en ese estado real, sin que códigos heredados dejen resultados fuera.

---

### User Story 3 - Ver el buscador de licitaciones generales sin columnas vacías (Priority: P2)

Un analista comercial que revisa el listado de licitaciones generales ya no ve columnas de Organismo, Monto e Items que en la práctica están vacías para casi todos los registros, y en cambio percibe un listado limpio y consistente con lo que el sistema realmente puede mostrar para ese universo de datos.

**Why this priority**: No bloquea el filtrado (ya cubierto por US1/US2), pero mejora la percepción de calidad y confiabilidad del listado — columnas casi siempre vacías se leen como datos rotos, no como una limitación conocida de la fuente.

**Independent Test**: Abrir el listado de licitaciones generales y confirmar que no se muestran columnas cuyo valor está ausente en la abrumadora mayoría de las filas, sin que esto afecte la visualización de licitaciones (como las de participación de TIVIT) que sí tienen esos datos completos.

**Acceptance Scenarios**:

1. **Given** el listado de licitaciones generales, **When** el usuario lo visualiza, **Then** no ve columnas de Organismo, Monto estimado ni cantidad de Items en la tabla principal.
2. **Given** una licitación específica que sí tiene Organismo, Monto o Items disponibles (ej. una de participación de TIVIT), **When** el usuario abre su ficha de detalle, **Then** esos datos siguen visibles ahí — esta historia solo afecta la tabla/listado general, no la información ya disponible a nivel de detalle.

### Edge Cases

- ¿Qué pasa si aparece un código de tipo nuevo en los datos que todavía no está en el glosario de la spec 026 (ej. los códigos O, H, CI, DC ya observados pero no documentados)? El selector de Tipo debe seguir permitiendo filtrar por ese código aunque no tenga una descripción amigable todavía, en vez de ocultarlo u omitirlo silenciosamente.
- ¿Qué pasa si una licitación general excepcionalmente sí tiene Organismo o Monto disponible (no todas las 178.391 carecen de estos datos por igual)? No debe perderse ni ocultarse esa información — debe seguir accesible en la ficha de detalle aunque no se muestre como columna en el listado general.
- ¿Cómo distingue el sistema una licitación general de una de participación de TIVIT para decidir qué columnas mostrar? Debe basarse en una señal ya existente en los datos (no requiere pedir al usuario que lo indique manualmente).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El selector de Tipo del buscador de licitaciones DEBE ofrecer como opciones los códigos reales de tipo de licitación documentados en el glosario de la spec `026-robustez-sincronizacion-tipos-reales`, con su nombre oficial legible.
- **FR-002**: El sistema DEBE permitir filtrar licitaciones por cualquier código de tipo real presente en los datos, incluyendo códigos que todavía no tengan una descripción amigable documentada.
- **FR-003**: El selector de Estado del buscador DEBE mostrar únicamente los 5 estados reales y vigentes del portal (Publicada, Cerrada, Desierta, Adjudicada, Revocada), sin duplicados de códigos heredados.
- **FR-004**: El listado/tabla principal de licitaciones generales NO DEBE mostrar columnas de Organismo, Monto estimado ni cantidad de Items.
- **FR-005**: La ficha de detalle de una licitación individual DEBE seguir mostrando Organismo, Monto estimado e Items cuando estén disponibles, sin importar si la licitación es general o de participación de TIVIT.
- **FR-006**: El sistema DEBE distinguir automáticamente, usando una señal ya presente en los datos, entre licitaciones generales y de participación de TIVIT, para aplicar las reglas de columnas de FR-004/FR-005 sin intervención manual del usuario.

### Key Entities

- **Tipo de licitación (catálogo)**: código real del portal con nombre oficial y descripción, según el glosario de la spec 026. Debe ampliarse para cubrir todos los códigos observados en los datos reales del sistema.
- **Estado de licitación (catálogo)**: los 5 estados reales vigentes del portal, sin códigos heredados duplicados.
- **Licitación general vs. de participación TIVIT**: dos subconjuntos del mismo universo de licitaciones con niveles de completitud de datos distintos — el listado principal debe reflejar razonablemente lo que cada subconjunto realmente tiene disponible.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de las opciones del selector de Tipo devuelve al menos un resultado real al aplicarse como filtro (ninguna opción "muerta").
- **SC-002**: El selector de Estado muestra exactamente 5 opciones, una por cada estado real vigente, sin duplicados.
- **SC-003**: El listado de licitaciones generales no presenta columnas con valor ausente en más del 95% de las filas visibles.
- **SC-004**: Ningún dato de Organismo, Monto o Items que ya existía en el sistema se pierde ni deja de ser accesible desde la ficha de detalle de la licitación correspondiente.

## Assumptions

- El glosario de tipos de licitación de la spec 026 es la fuente de verdad para los nombres oficiales; esta spec no redefine esos significados, solo los usa para poblar el catálogo real.
- La señal para distinguir licitaciones generales de licitaciones de participación de TIVIT ya existe en los datos actuales del sistema (por ejemplo, el origen de la captura) y no requiere una migración de datos adicional para estar disponible.
- Los códigos de tipo observados en los datos reales pero no documentados todavía en el glosario de la spec 026 (O, H, CI, DC) se pueden agregar al catálogo con una descripción provisoria o pendiente, sin bloquear esta spec a que alguien confirme su significado oficial exacto.
- Esta spec no modifica ningún pipeline de ingesta de datos (scraper ni sincronización diaria) — es exclusivamente un ajuste de catálogo y de presentación en el frontend sobre los datos que el sistema ya tiene.
