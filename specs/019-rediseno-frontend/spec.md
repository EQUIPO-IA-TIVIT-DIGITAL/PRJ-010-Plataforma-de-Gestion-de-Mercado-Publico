# Feature Specification: Rediseño Frontend de MPM — Alcance por pantalla

**Feature Branch**: `019-rediseno-frontend`

**Created**: 2026-07-03 (actualizado 2026-08-05)

**Status**: Draft

**Priority**: P1/P2/P3 mixto — ver historias individuales; reemplaza el alcance genérico anterior de esta misma spec

**Input**: User description: "Armar la spec de frontend que quedó en roadmap, con los cambios que realizaremos: (1) Licitaciones ('landing' post-login) reportada como mal formateada — requiere auditoría; (2) Catálogos desactualizado y poco intuitivo; (3) Análisis — máxima prioridad, rediseño completo, se siente 'fofo y triste, muy ai-slop'; (4) Ejecutivo cumple su función pero muy mejorable, evaluar mostrar más datos comparados; (5) Mensajes funciona bien pero se siente como widget, rediseño para integrarlo mejor; (6) Notificaciones sin cambios; (7) Alertas mismo tratamiento que Ejecutivo; (8) Competidores mismo tratamiento que Ejecutivo."

---

## Contexto y hallazgos previos

Esta spec reemplaza el alcance genérico de la versión anterior (2026-07-03, "Planned", sin `tasks.md`) con un alcance concreto por pantalla, priorizado directamente por el dueño del producto tras revisar el estado actual de cada una.

**Auditoría de consistencia realizada el 2026-08-05** (ver también `redesign.md` del skill `frontend-design`, protocolo de Redesign — Sección 11) encontró que el sistema de theming de Ant Design 5 ya existe y está razonablemente completo (`src/mpm-web/src/main.tsx`: color de marca TIVIT `#E30613`, tipografía Inter, radios de borde, sombras, overrides por componente para `Layout`/`Menu`/`Button`/`Input`/`Table`/etc.), pero **las páginas individuales no lo respetan de forma consistente**. Hallazgos concretos:

- 5 implementaciones distintas de "badge de estado" con paletas hardcodeadas propias (`AnalisisListPage.tsx`, `CatalogoPage.tsx`, `NotificacionesPage.tsx`, `AlertasPage.tsx`, `EjecutivoDashboardPage.tsx`), sin componente compartido.
- 3 estructuras de header de página distintas (ícono con gradiente rojo vs. morado vs. sin ícono).
- Colores hex repetidos manualmente decenas de veces en vez de heredar los tokens del theme.
- `MensajeriaPage.tsx` construida 100% con `div` + estilos inline, sin usar `Card`/`Layout` de Ant Design como el resto del sistema.
- Botones "primary" con gradiente inline reimplementado a mano en vez de heredar `colorPrimary` ya configurado en el theme.
- Emojis mezclados con `@ant-design/icons` en `AnalisisListPage.tsx`.

**Intento descartado**: el mismo día se descartó por completo un intento de rediseño hecho por otro agente (`src/mpm-web-v2`, antes `src/mpm-web/src/front-v2`) — quedó anidado dentro del proyecto original en vez de ser un proyecto hermano real, compartía `package.json`/`node_modules` con la app original, dejó archivos de depuración sueltos en la raíz del proyecto, y tenía bugs de codificación de texto (secuencias unicode sin decodificar visibles en pantalla, ej. `Mercado Público` literal). Ninguna parte de ese código se reutiliza. La lección aplicada a esta spec: cualquier cambio de frontend se hace **dentro de `src/mpm-web` existente**, sin proyectos paralelos.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ordenar y desaturar la pantalla de Licitaciones (Priority: P1)

Un usuario que entra al sistema llega a `/licitaciones` (la pantalla por defecto tras el login) y la ve ordenada y con una densidad de información manejable — hoy la fila de tarjetas de estadísticas por estado tiene huecos de alineación (p. ej. un espacio vacío junto a la tarjeta "Revocada" cuando el número de tarjetas no completa la grilla de forma pareja), y la pantalla en conjunto se percibe sobrecargada de datos (tarjetas de resumen + fila de estadísticas por estado + filtros + tabla, todo compitiendo por atención al mismo tiempo).

**Why this priority**: Es la primera pantalla que ve cualquier usuario al entrar al sistema — el desorden visual y la saturación de datos aquí afectan la primera impresión de todo el producto, a diferencia de un desajuste estético en una pantalla secundaria.

**Independent Test**: Comparar la fila de tarjetas de estadísticas por estado antes/después y confirmar que no quedan huecos de alineación sin importar cuántos estados existan; medir la cantidad de bloques de información visible "sobre el pliegue" (above the fold) antes/después y confirmar una reducción perceptible de saturación sin perder acceso a ningún dato.

**Acceptance Scenarios**:

1. **Given** la fila de tarjetas de estadísticas por estado (Publicada, Cerrada, Desierta, Adjudicada, Revocada, y cualquier otro estado presente), **When** se renderiza, **Then** las tarjetas se distribuyen sin huecos de alineación, cualquiera sea la cantidad de estados existentes.
2. **Given** la pantalla de Licitaciones completa (tarjetas de resumen, estadísticas por estado, filtros, tabla), **When** el usuario la ve por primera vez, **Then** la jerarquía visual prioriza la tabla de licitaciones (la tarea principal) sobre los bloques de estadísticas, en vez de presentar todo con el mismo peso visual.
3. **Given** la pantalla de Licitaciones abierta en un viewport angosto (`< 768px`), **When** el usuario interactúa con filtros y tabla, **Then** el layout colapsa de forma utilizable, sin scroll horizontal roto, huecos de alineación ni controles inaccesibles.
4. **Given** el drawer de detalle de una licitación abierto, **When** el usuario lo revisa, **Then** el contenido del drawer respeta el mismo lenguaje visual que el resto del sistema (tipografía, espaciado, badges de estado) sin romper el layout de la pantalla detrás.

---

### User Story 2 - Rediseño completo de Análisis (Priority: P1)

Un usuario que trabaja a diario en el módulo de Análisis (lista de workspaces, workspace individual, dashboard de resultados, chat de análisis) usa una interfaz que se siente cuidada y con criterio de diseño propio, no genérica ni con la sensación de haber sido generada sin revisión ("ai-slop").

**Why this priority**: Priorización explícita del dueño del producto por sobre el resto del rediseño — es el módulo de mayor uso diario y el que más urgentemente necesita una revisión de fondo, no solo ajustes puntuales de theming.

**Independent Test**: Navegar el flujo completo (lista de análisis → workspace → dashboard de resultados → chat) y confirmar que cada pantalla del flujo usa una composición visual deliberada (jerarquía clara, sin badges/headers/estados con paletas hardcodeadas y divergentes entre sí como las encontradas en la auditoría), manteniendo toda la funcionalidad ya existente.

**Acceptance Scenarios**:

1. **Given** el listado de análisis (`AnalisisListPage.tsx`), **When** el usuario lo revisa, **Then** los badges de estado, el header de página y los estados vacíos usan los componentes compartidos del sistema (no paletas hex propias ni emojis mezclados con el set de íconos del proyecto).
2. **Given** un workspace de análisis en curso, **When** el usuario sube documentos y dispara un análisis, **Then** el flujo conserva toda la funcionalidad actual (carga, progreso, resultado) con una composición visual coherente con el resto del sistema rediseñado.
3. **Given** el dashboard de resultados de un análisis completado, **When** el usuario lo revisa, **Then** la información se presenta con jerarquía visual clara, sin verse como una lista plana de datos sin curar.
4. **Given** el chat de análisis, **When** el usuario lo usa, **Then** se integra visualmente con el resto del módulo de Análisis, no como un componente aislado.

---

### User Story 3 - Actualización de Catálogos (Priority: P2)

Un usuario que administra datos de referencia (estados, tipos, monedas, áreas de negocio) en `/catalogos` encuentra la información organizada de forma clara y actualizada, sin la sensación de estar usando una pantalla desactualizada o poco intuitiva respecto al resto del sistema.

**Why this priority**: Es una pantalla de uso administrativo, no diario para la mayoría de usuarios — importante para la coherencia general del sistema, pero de menor urgencia que Licitaciones y Análisis.

**Independent Test**: Navegar `/catalogos`, localizar y editar/consultar un dato de referencia sin ambigüedad sobre dónde encontrarlo, y confirmar que usa los mismos patrones de tabla/badge/header que el resto del sistema ya rediseñado.

**Acceptance Scenarios**:

1. **Given** un usuario que necesita revisar los estados posibles de una licitación, **When** abre `/catalogos`, **Then** encuentra la información agrupada de forma clara, sin necesitar explicación adicional sobre cómo navegar la pantalla.
2. **Given** las categorías de catálogo existentes (estados, tipos, monedas, áreas de negocio), **When** se muestran en pantalla, **Then** usan los mismos componentes de badge/estado que el resto del sistema rediseñado (no la paleta hex propia encontrada en la auditoría).

---

### User Story 4 - Mensajería mejor integrada al resto del sistema (Priority: P2)

Un usuario que usa la mensajería interna (`/mensajes`) percibe la pantalla como parte integral de MPM, no como un widget de chat insertado aparte, mantenien do toda su funcionalidad actual (conversaciones, presencia, archivos adjuntos).

**Why this priority**: La funcionalidad ya funciona razonablemente bien según el dueño del producto — el ajuste es de integración visual, no de comportamiento, por lo que es de menor urgencia que los rediseños funcionales/estructurales de Licitaciones y Análisis.

**Independent Test**: Abrir `/mensajes`, iniciar y responder una conversación, y confirmar que el layout (lista de conversaciones, panel de chat, indicadores de presencia) usa los componentes compartidos del sistema (`Card`, `Layout` de Ant Design) en vez de la estructura 100% basada en `div` + estilos inline encontrada en la auditoría.

**Acceptance Scenarios**:

1. **Given** la pantalla de Mensajería, **When** el usuario la compara con Licitaciones o Análisis ya rediseñadas, **Then** comparte el mismo lenguaje visual de contenedores, tipografía y espaciado.
2. **Given** una conversación activa con indicador de presencia, **When** se muestra en pantalla, **Then** usa el mismo patrón de indicador de estado que el resto del sistema (no un estilo propio aislado).
3. **Given** toda la funcionalidad actual de mensajería (crear conversación, enviar archivos, ver participantes), **When** se aplica el rediseño, **Then** ninguna capacidad se pierde ni cambia su comportamiento.

---

### User Story 5 - Mejora de Ejecutivo, Alertas y Competidores (Priority: P3)

Un usuario que consulta el dashboard Ejecutivo, gestiona Alertas o revisa Competidores usa pantallas visualmente pulidas y coherentes con el resto del sistema; en el caso particular de Ejecutivo, además puede ver más datos comparativos de los que se muestran hoy (a definir en fase de planificación qué comparativas agregar).

**Why this priority**: Estas tres pantallas ya cumplen su función según el dueño del producto — el ajuste es de pulido visual y, solo para Ejecutivo, de ampliar el contenido informativo. Es la prioridad más baja del rediseño porque no hay una queja funcional ni de usabilidad de fondo, a diferencia de Análisis o Licitaciones.

**Independent Test**: Navegar cada una de las tres pantallas y confirmar que usan los componentes compartidos de badge/header/estado definidos para el resto del sistema, y que en Ejecutivo aparece al menos una comparativa de datos adicional a las ya existentes.

**Acceptance Scenarios**:

1. **Given** el dashboard Ejecutivo, **When** el usuario lo abre, **Then** ve al menos una vista comparativa de datos (p. ej. actividad propia vs. de competidores, evolución en el tiempo) que no está disponible en la versión actual.
2. **Given** las pantallas de Alertas y Competidores, **When** se muestran en pantalla, **Then** usan los mismos componentes de badge de estado y header de página que el resto del sistema rediseñado, sin paletas de color propias.
3. **Given** la funcionalidad actual de las tres pantallas (crear/gestionar alertas, buscar competidores, ver métricas ejecutivas), **When** se aplica el rediseño, **Then** ninguna capacidad existente se pierde.

---

### Edge Cases

- ¿Qué pasa con Notificaciones, explícitamente fuera de alcance? No debe verse visualmente huérfana tras el rediseño del resto del sistema — como mínimo debe seguir usando los componentes de layout/navegación compartidos (sidebar, header general), aunque su contenido interno no se toque.
- ¿Qué pasa si un componente compartido nuevo (ej. `StatusBadge`) cambia el significado visual de un estado que algún flujo (ej. `AnalisisCompletionWatcher`) usa para detectar comportamiento, no solo para mostrar color? El rediseño de componentes visuales no debe alterar contratos de datos ni lógica que dependa de valores de estado.
- ¿Qué pasa con pantallas que dependen de componentes de Ant Design con comportamiento específico (tablas grandes, formularios complejos, el editor de mensajería)? El rediseño no debe romper su funcionalidad ni degradar su rendimiento.
- ¿Cómo se prioriza el rediseño si compite por tiempo del mismo equipo que atiende bugs de producción? Debe poder pausarse y retomarse pantalla por pantalla sin dejar una pantalla individual a medio rediseñar de forma visible para el usuario final.
- ¿Qué pasa si al reducir la densidad de Licitaciones (Historia 1) algún usuario necesita ver todos los bloques de estadísticas sin interacción adicional? La reducción de saturación no debe ocultar datos de forma permanente — puede reorganizar jerarquía visual (colapsar, agrupar, priorizar), pero toda la información actual debe seguir accesible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST corregir la grilla de tarjetas de estadísticas por estado en Licitaciones para que no queden huecos de alineación cualquiera sea la cantidad de estados existentes, y MUST reducir la densidad visual general de la pantalla (tarjetas de resumen + estadísticas + filtros + tabla) sin ocultar de forma permanente ningún dato hoy visible.
- **FR-002**: El sistema MUST introducir componentes compartidos de indicador de estado (badge) y de header de página, reemplazando las implementaciones divergentes encontradas en la auditoría (Análisis, Catálogos, Notificaciones, Alertas, Ejecutivo).
- **FR-003**: El rediseño de Análisis MUST cubrir las cuatro superficies del flujo (lista, workspace, dashboard de resultados, chat) de forma coherente entre sí, no solo la lista.
- **FR-004**: El sistema MUST reemplazar los colores hex hardcodeados repetidos en las páginas auditadas por los tokens ya definidos en el theme de Ant Design (`main.tsx`), o por nuevos tokens agregados al mismo theme si hace falta un color adicional.
- **FR-005**: El sistema MUST reconstruir `MensajeriaPage.tsx` (y los componentes que dependen de ella) usando los componentes de layout de Ant Design ya en uso en el resto del sistema, en vez de `div` + estilos inline.
- **FR-006**: El sistema MUST reemplazar los botones con gradiente inline reimplementado a mano por el estilo `primary` ya configurado en el theme de `Button`.
- **FR-007**: El sistema MUST reemplazar los emojis usados como iconografía por el set de iconos ya en uso en el proyecto (`@ant-design/icons`).
- **FR-008**: El dashboard Ejecutivo MUST incorporar al menos una vista comparativa de datos adicional a las disponibles hoy (el conjunto exacto de comparativas se define en la fase de planificación).
- **FR-009**: El rediseño MUST preservar toda la funcionalidad existente de cada pantalla tocada — no elimina ni oculta capacidades ya disponibles para el usuario.
- **FR-010**: El rediseño MUST poder ejecutarse pantalla por pantalla (o módulo por módulo, en el caso de Análisis) de forma independiente, sin requerir que todas las pantallas se actualicen a la vez.
- **FR-011**: El sistema MUST NOT introducir un proyecto o carpeta paralela para el frontend — todo cambio se aplica dentro de `src/mpm-web` existente, reusando su `package.json`/dependencias salvo justificación explícita puntual.
- **FR-012**: La pantalla de Notificaciones MUST permanecer sin cambios funcionales ni de contenido, aunque puede heredar automáticamente ajustes de componentes compartidos de layout/navegación si estos se actualizan como parte de otra historia.

### Key Entities *(include if feature involves data)*

- **Sistema de diseño / tokens de theme**: conjunto ya existente de decisiones de color, tipografía, espaciado y forma en `src/mpm-web/src/main.tsx` (Ant Design `ConfigProvider`), que se completa y se hace cumplir de forma consistente en vez de reemplazarse.
- **Componentes compartidos de UI**: `StatusBadge` (indicador de estado unificado) y `PageHeader` (encabezado de página unificado) como nuevas piezas reutilizables que reemplazan las implementaciones divergentes por pantalla encontradas en la auditoría.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Las 8 pantallas evaluadas (Licitaciones, Catálogos, Análisis, Ejecutivo, Mensajes, Notificaciones, Alertas, Competidores) comparten el mismo lenguaje visual verificable a simple vista (tipografía, color, indicadores de estado, estructura de header).
- **SC-002**: Cero regresiones funcionales reportadas tras el rediseño de cada pantalla tocada (ninguna capacidad existente se pierde).
- **SC-003**: Cero implementaciones de badge de estado o header de página con paleta de color propia fuera de los componentes compartidos, verificable por búsqueda de código (cero coincidencias de hex hardcodeado fuera del archivo de theme).
- **SC-004**: El dashboard Ejecutivo muestra al menos una comparativa de datos no disponible en la versión anterior al rediseño.
- **SC-005**: Un usuario externo al equipo de desarrollo, al navegar las 8 pantallas en secuencia, no identifica inconsistencias visuales evidentes entre ellas.

## Assumptions

- "Landing" se refiere a la pantalla de Licitaciones (`/licitaciones`), la pantalla por defecto tras el login — confirmado explícitamente con el usuario, no la pantalla de login/autenticación en sí.
- Este rediseño no requiere cambios de arquitectura backend — es exclusivamente frontend (`src/mpm-web`), sobre los mismos endpoints y datos ya expuestos, salvo que Ejecutivo (Historia 5) requiera un endpoint nuevo para alguna comparativa que hoy no se calcula en backend (a confirmar en fase de planificación).
- Se mantiene Ant Design 5 como librería base y el theme ya existente en `main.tsx` como punto de partida — no se evalúa un cambio de librería de componentes ni un rediseño tipo landing page con librerías de motion/animación (GSAP, Framer Motion/Motion). El enfoque es evolución del theming existente, siguiendo el protocolo de "Redesign - Preserve" (auditar, extraer tokens, evolucionar) en vez de un rediseño desde cero.
- No se agregan dependencias nuevas al proyecto salvo justificación puntual explícita (por ejemplo, una librería de gráficos para las comparativas nuevas de Ejecutivo, si el equipo de planificación determina que hace falta).
- Existe un transcript de feedback de Francisco (feedback de ChileCompra) disponible como fuente adicional de contexto si alguna de las 8 historias necesita más detalle durante la planificación o implementación — no se incorporó a esta spec porque el alcance ya fue suficientemente detallado directamente por el dueño del producto en esta sesión.
- La ejecución es incremental: se asume que el equipo la trabaja pantalla por pantalla o módulo por módulo, priorizando Licitaciones y Análisis (P1) antes que Catálogos y Mensajes (P2), y estas antes que Ejecutivo/Alertas/Competidores (P3).
- Fuera de alcance: cualquier cambio a Notificaciones, y cualquier reconstrucción tipo "landing page" con heroes/animaciones/motion — ya se descartó ese enfoque en esta misma sesión (ver Contexto).
