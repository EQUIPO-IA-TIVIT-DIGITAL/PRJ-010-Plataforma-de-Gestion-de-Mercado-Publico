# Feature Specification: Rediseño Frontend de MPM

**Feature Branch**: `019-rediseno-frontend`
**Created**: 2026-07-03
**Status**: Planned
**Semana estimada**: Paralelo / sin fecha fija — no compite por prioridad con Alertas, Buscador Inteligente ni Pipeline de Oportunidades
**Impacto**: Medio | **Complejidad**: Media | **Depende de**: —

**Origen**: Repriorización del 2026-07-03. El cliente pidió explícitamente que este rediseño se ubique **por debajo** de la Fase 7 (Pipeline de Oportunidades) en el orden de ejecución, y que se trabaje en paralelo a las implementaciones funcionales cuando el equipo tenga disponibilidad — no es un punto de dolor del cliente como sí lo son Alertas, Buscador y Pipeline.

---

## Contexto

La aplicación web de MPM (React + Ant Design) creció incorporando pantallas de forma incremental (Licitaciones, Análisis, Mensajería, Notificaciones, Catálogos) a medida que se construían las fases funcionales. Esto dejó inconsistencias visuales y de usabilidad entre pantallas que no bloquean el uso del sistema, pero que afectan la percepción de calidad, especialmente de cara a que este producto se plantee como potencialmente comercializable a futuro. Esta fase moderniza la identidad visual y la coherencia de experiencia sin tocar la lógica de negocio de los módulos existentes.

---

## User Stories

### User Story 1 — Coherencia visual entre pantallas (Priority: P1)

Un usuario que navega entre Licitaciones, Análisis, Mensajería, Notificaciones y Catálogos percibe un mismo sistema, no pantallas construidas en momentos distintos con criterios distintos.

**Why this priority**: Es la base de cualquier mejora posterior — sin un lenguaje visual consistente, cualquier ajuste puntual se ve parchado.

**Independent Test**: Navegar las 5 pantallas principales en secuencia y confirmar que tipografía, espaciados, colores de estado (activo/cerrado/adjudicado, leído/no leído, etc.) y patrones de tabla/filtro se ven y comportan de forma consistente.

**Acceptance Scenarios**:
1. **Given** el usuario navega entre módulos, **When** compara encabezados, botones primarios/secundarios y tablas, **Then** siguen el mismo lenguaje visual (tipografía, espaciado, color) en todas las pantallas.
2. **Given** un estado semántico (p. ej. licitación "activa" o notificación "no leída"), **When** se muestra en cualquier pantalla, **Then** usa el mismo color y forma de indicador en todo el sistema.

---

### User Story 2 — Mejoras de usabilidad en pantallas existentes (Priority: P2)

Un usuario frecuente (analista comercial, account manager) encuentra más rápido lo que busca en las pantallas que ya usa a diario, sin necesidad de reaprender la interfaz.

**Why this priority**: El valor de este rediseño está en reducir fricción del uso diario, no en cambiar el propósito de cada pantalla.

**Independent Test**: Un usuario que ya conoce el sistema completa sus tareas habituales (filtrar licitaciones, revisar un análisis, marcar una notificación) en igual o menor cantidad de clics que antes del rediseño.

**Acceptance Scenarios**:
1. **Given** una pantalla con filtros existentes, **When** se rediseña, **Then** los filtros más usados quedan más visibles/accesibles sin remover funcionalidad existente.
2. **Given** un usuario habituado a la interfaz anterior, **When** usa la interfaz rediseñada, **Then** no requiere capacitación adicional para completar sus tareas habituales.

---

### User Story 3 — Identidad visual pulida (Priority: P3)

El equipo directivo puede mostrar el sistema a un cliente externo o prospecto (dado el potencial de comercialización mencionado por el cliente) sin que la interfaz se perciba como una herramienta interna inacabada.

**Why this priority**: Es la de menor urgencia operativa, pero es la que más valor aporta si el producto se convierte en algo comercializable, como plantearon Manuel y Francisco en las reuniones de alcance.

**Independent Test**: Mostrar el sistema rediseñado a alguien ajeno al equipo de desarrollo y confirmar que no identifica inconsistencias visuales evidentes ni percibe la interfaz como "en construcción".

**Acceptance Scenarios**:
1. **Given** el sistema rediseñado, **When** se presenta a un usuario externo, **Then** la primera impresión no requiere aclaraciones del tipo "esto todavía no está terminado".

---

### Edge Cases

- ¿Qué pasa con pantallas que dependen de componentes de Ant Design con comportamiento específico (tablas grandes, formularios complejos)? El rediseño no debe romper su funcionalidad ni su rendimiento.
- ¿Cómo se prioriza el rediseño si compite por tiempo del mismo equipo que implementa Alertas/Buscador/Pipeline? Debe poder pausarse y retomarse sin dejar una pantalla a medio rediseñar de forma visible para el usuario final (se libera módulo por módulo, no a medias dentro de un mismo módulo).
- ¿Qué pasa con usuarios que ya tienen hábitos formados con la interfaz actual? Los cambios deben ser incrementales por pantalla, no un relanzamiento completo de un día para otro.

## Requirements

### Functional Requirements

- **FR-001**: El sistema MUST aplicar un lenguaje visual consistente (tipografía, espaciado, color, indicadores de estado) across todas las pantallas existentes (Licitaciones, Análisis, Mensajería, Notificaciones, Catálogos).
- **FR-002**: El rediseño MUST preservar toda la funcionalidad existente de cada pantalla — no elimina ni oculta capacidades ya disponibles para el usuario.
- **FR-003**: El rediseño MUST poder ejecutarse pantalla por pantalla de forma independiente, sin requerir que todas las pantallas se actualicen a la vez.
- **FR-004**: El sistema MUST mantener o mejorar la cantidad de pasos necesarios para completar las tareas habituales del usuario (filtrar, revisar, marcar, exportar) respecto a la interfaz actual.
- **FR-005**: El rediseño MUST poder pausarse entre pantallas sin dejar una pantalla individual en un estado visualmente inconsistente a medio camino.

### Key Entities

- **Lenguaje visual / sistema de diseño**: conjunto de decisiones de tipografía, color, espaciado e indicadores de estado que se aplica de forma consistente a todas las pantallas.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Las 5 pantallas principales (Licitaciones, Análisis, Mensajería, Notificaciones, Catálogos) comparten el mismo lenguaje visual verificable a simple vista (tipografía, color, espaciado).
- **SC-002**: Cero regresiones funcionales reportadas tras el rediseño de cada pantalla (ninguna capacidad existente se pierde).
- **SC-003**: Los usuarios habituales completan sus tareas frecuentes en igual o menor número de clics que antes del rediseño.
- **SC-004**: El rediseño se ejecuta sin desplazar en el tiempo ninguna entrega de Alertas, Buscador Inteligente o Pipeline de Oportunidades.

## Assumptions

- Este rediseño no requiere un service nuevo ni cambios de arquitectura backend — es exclusivamente frontend (`src/mpm-web`), sobre los mismos endpoints y datos ya expuestos.
- Se mantiene Ant Design 5 como librería base (no se evalúa un cambio de librería de componentes en esta fase); el rediseño es de tokens visuales (tema) y de composición de pantallas, no de reemplazo de framework.
- La ejecución es incremental y de baja prioridad: se asume que el equipo la trabaja en tiempo disponible entre entregas de Alertas, Buscador Inteligente y Pipeline, o después de cerrar Pipeline — nunca desplazando esas fechas.
- Fuera de alcance: rediseño de flujos nuevos que aún no existen (eso corresponde a cada fase funcional al construirse), y cambios de identidad de marca que requieran aprobación de partes externas al equipo TIVIT.
