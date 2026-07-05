# Feature Specification: Extracción de Documentos vía API Directa

**Feature Branch**: `016-extraccion-documentos-api`

**Created**: 2026-07-01

**Status**: Draft

**Input**: User description: "Mira, quiero mejorar el sistema de extraccion de documentos, creo que ahora mismo lo que hace es abrir un chrome y poco a poco ir tocando y moviendose hasta extraerlos? en caso siga asi, ir a la web, hacer scrapping hasta lograr replicar este flujo usando solo api directa de mercado publico"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Descarga de documentos sin navegador (Priority: P1)

Como sistema de sincronización de licitaciones, necesito obtener el Acta de Evaluación y demás documentos adjuntos de una licitación directamente mediante llamadas HTTP, sin tener que abrir y controlar un navegador (Chrome/Playwright) que navega paso a paso por la interfaz web.

**Why this priority**: Es el objetivo central del feature. El flujo actual basado en automatización de navegador es lento, frágil (depende de selectores de UI que pueden cambiar) y consume muchos recursos (memoria/CPU de un Chrome headless por cada licitación). Reemplazarlo por llamadas directas es lo que da todo el valor del feature.

**Independent Test**: Se puede probar tomando un conjunto conocido de licitaciones adjudicadas que ya tienen Acta de Evaluación descargada por el flujo actual, ejecutando el nuevo flujo de extracción directa sobre las mismas licitaciones, y verificando que se obtienen los mismos documentos (mismo contenido/página) sin abrir un navegador visible ni headless durante la descarga.

**Acceptance Scenarios**:

1. **Given** una licitación adjudicada con Acta de Evaluación disponible en el portal, **When** el sistema ejecuta la extracción de documentos, **Then** el Acta de Evaluación queda descargada y almacenada igual que hoy, sin que se haya iniciado un proceso de navegador para esa descarga.
2. **Given** una licitación con múltiples documentos adjuntos (bases, anexos, resoluciones, acta), **When** el sistema ejecuta la extracción, **Then** todos los documentos que el flujo actual descarga hoy son descargados también por el nuevo flujo.
3. **Given** el nuevo flujo de extracción directa, **When** se compara su tiempo de ejecución y consumo de recursos contra el flujo actual para el mismo lote de licitaciones, **Then** el nuevo flujo es sensiblemente más rápido y liviano.

---

### User Story 2 - Continuidad ante fallas del nuevo flujo (Priority: P2)

Como responsable de operación del sistema, necesito que si el nuevo flujo de extracción directa no logra obtener los documentos de una licitación puntual (por ejemplo porque el portal cambió su estructura interna, exige un paso adicional de validación, o hay un adjunto con formato no anticipado), el sistema recurra automáticamente al flujo actual basado en navegador para esa licitación, y que solo si ambos mecanismos fallan, me lo informe claramente sin perder esa licitación en silencio.

**Why this priority**: Sin esta garantía, una migración al nuevo flujo podría introducir "huecos" invisibles de documentos no descargados, lo cual es peor que el problema actual (lentitud) porque afecta la confiabilidad de los datos que usa el análisis con IA. Mantener el flujo de navegador como respaldo automático evita esos huecos mientras el flujo directo madura.

**Independent Test**: Se puede probar simulando una licitación cuyo documento no puede obtenerse por el nuevo flujo directo y verificando que el sistema intenta automáticamente el flujo basado en navegador para esa misma licitación, y que solo registra un fallo visible (log/alerta/estado de la licitación) si ese respaldo también falla.

**Acceptance Scenarios**:

1. **Given** una licitación cuyo documento no puede obtenerse mediante el flujo de extracción directa, **When** el sistema procesa esa licitación, **Then** recurre automáticamente al flujo basado en navegador para completar la descarga, sin intervención manual.
2. **Given** una licitación cuyo documento no puede obtenerse ni por el flujo directo ni por el flujo de respaldo basado en navegador, **When** el sistema termina de procesarla, **Then** queda un registro claro del fallo, identificando la licitación y el motivo, accesible para quien opera el sistema.
3. **Given** que existen fallos de extracción registrados, **When** se revisan las licitaciones sincronizadas del período, **Then** es posible distinguir cuáles quedaron completas (por flujo directo o por respaldo) y cuáles con documentos pendientes.

---

### User Story 3 - Mismo ritmo de sincronización que hoy (Priority: P3)

Como responsable del negocio, necesito que la frecuencia y cobertura con la que se sincronizan licitaciones y documentos no empeore respecto al flujo actual, para no perder oportunidades de licitación por demoras en el nuevo flujo.

**Why this priority**: Es una condición de no-regresión más que una mejora nueva; de ahí su prioridad menor, pero es necesaria para poder reemplazar el flujo actual con confianza.

**Independent Test**: Se puede probar comparando, durante un período de operación en paralelo, la cantidad de licitaciones con documentos completos obtenidos por ambos flujos en la misma ventana de tiempo.

**Acceptance Scenarios**:

1. **Given** el nuevo flujo de extracción directa operando de forma continua, **When** se compara con la cobertura histórica del flujo actual en un período equivalente, **Then** la cantidad de licitaciones con documentos completos es igual o mayor.

---

### Edge Cases

- ¿Qué ocurre si el portal de Mercado Público exige un paso de autenticación o token que expira y el flujo directo no lo renueva a tiempo?
- ¿Qué ocurre si una licitación no tiene ningún documento adjunto publicado (caso legítimo, no una falla)?
- ¿Qué ocurre si el portal introduce medidas anti-automatización (bloqueo por volumen de solicitudes, verificación adicional) que afecten las llamadas directas?
- ¿Qué ocurre si el portal cambia la estructura de sus respuestas internas (nuevos campos, formato distinto) de forma que el flujo directo deja de reconocer los documentos?
- ¿Qué ocurre con licitaciones que ya fueron procesadas por el flujo actual? ¿Se vuelven a procesar con el flujo nuevo o se dejan como están?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE identificar, mediante análisis del tráfico real del sitio de Mercado Público (observando qué solicitudes hace el propio sitio al listar y descargar adjuntos), el conjunto de solicitudes necesarias para obtener el listado de documentos adjuntos y el Acta de Evaluación de una licitación.
- **FR-002**: El sistema DEBE descargar el Acta de Evaluación y los documentos adjuntos de una licitación realizando únicamente solicitudes HTTP directas, sin iniciar un proceso de navegador (visible o headless) para esa descarga.
- **FR-003**: El sistema DEBE mantener/replicar el mecanismo de autenticación o sesión necesario para acceder a los documentos, de forma equivalente a como el flujo actual inicia sesión hoy.
- **FR-004**: El sistema DEBE producir el mismo resultado (documentos descargados y su metadata asociada) que produce hoy el flujo basado en navegador, para el mismo conjunto de licitaciones.
- **FR-005**: El sistema DEBE registrar de forma clara y consultable cualquier licitación cuyos documentos no pudieron obtenerse mediante el flujo directo, indicando el motivo del fallo.
- **FR-006**: Si el flujo de extracción directa falla para una licitación puntual, el sistema DEBE recurrir automáticamente al flujo actual basado en navegador para esa licitación, y solo debe registrarse un fallo real cuando ambos mecanismos fallan.
- **FR-007**: El nuevo flujo directo DEBE cubrir el mismo conjunto de documentos que descarga hoy el flujo basado en navegador (Acta de Evaluación, bases, anexos y resoluciones), sin reducir el alcance actual.
- **FR-008**: La adopción del nuevo flujo DEBE hacerse mediante un período de validación en paralelo, en el que ambos flujos (directo y basado en navegador) se ejecutan sobre las mismas licitaciones y se comparan sus resultados, antes de retirar el flujo basado en navegador como mecanismo principal.

### Key Entities

- **Licitación**: Proceso de compra pública identificado por un código único; tiene un estado (p. ej. adjudicada) y una fecha de cierre.
- **Documento Adjunto**: Archivo asociado a una licitación (Acta de Evaluación, bases, anexos, resoluciones), con un nombre/tipo y contenido descargable.
- **Registro de Extracción**: Resultado de intentar obtener los documentos de una licitación mediante el nuevo flujo directo — indica éxito o falla, y en caso de falla, el motivo.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El tiempo para obtener los documentos de una licitación se reduce en al menos 70% respecto al flujo actual basado en navegador.
- **SC-002**: El sistema procesa el mismo lote de licitaciones utilizando significativamente menos recursos de cómputo (sin necesidad de mantener un proceso de navegador por licitación).
- **SC-003**: Al menos el 95% de las licitaciones que hoy obtienen su Acta de Evaluación mediante el flujo basado en navegador la obtienen también mediante el nuevo flujo directo, sin intervención manual.
- **SC-004**: El 100% de las licitaciones cuyos documentos no pudieron obtenerse mediante el flujo directo quedan identificables en un registro consultable, sin excepción silenciosa.
- **SC-005**: La cobertura de licitaciones con documentos completos por período no disminuye respecto al histórico del flujo actual.

## Assumptions

- El sitio de Mercado Público, si bien no publica un endpoint oficial y documentado para adjuntos, expone internamente las solicitudes que su propia interfaz web usa para listar y descargar documentos, y estas pueden observarse e identificarse mediante análisis del tráfico de red del sitio (sin necesidad de automatizar clics).
- Las credenciales/autenticación que hoy usa el flujo de navegador (usuario y contraseña de Mercado Público) siguen siendo el mecanismo de acceso disponible; no se asume la existencia de un token de API oficial para adjuntos.
- El nuevo flujo se integra en el mismo proceso de sincronización de licitaciones que existe hoy (no se asume un sistema separado).
- Los documentos actualmente descargados (Acta de Evaluación como mínimo) siguen siendo relevantes para el análisis posterior con IA; el nuevo flujo no cambia qué se hace con los documentos una vez descargados, solo cómo se obtienen.
- El flujo actual basado en navegador no se retira de inmediato: se mantiene disponible como respaldo automático ante fallas del flujo directo, y como comparación durante el período de validación en paralelo, hasta que se decida su retiro definitivo en una etapa posterior (fuera del alcance de este feature).
