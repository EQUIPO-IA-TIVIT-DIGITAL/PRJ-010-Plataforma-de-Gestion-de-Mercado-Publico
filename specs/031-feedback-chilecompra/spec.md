# Feature Specification: Feedback ChileCompra — Filtrado por área, estadísticas de estado, orden de análisis, inteligencia de mercado y flujo colaborativo go/no-go

**Feature Branch**: `031-feedback-chilecompra`

**Created**: 2026-08-04

**Status**: Draft

**Priority**: P0 — máxima prioridad actual del proyecto

**Input**: User description: "Feedback de cliente ChileCompra (reunión 2026-08-03, Manuel Aliaga + Francisco Lopez) — 5 mejoras a implementar sobre licitaciones, análisis y competidores: (1) filtro de licitaciones por área de negocio (Cloud, Ciberseguridad, Digital) en vez del listado completo de 183.000+ registros; (2) clasificación estadística de licitaciones por estado con drill-down; (3) reordenar el listado de análisis por fecha de adjudicación en vez de fecha de creación del análisis; (4) ampliar el informe ejecutivo de competidores con su actividad total de mercado (incluyendo licitaciones donde TIVIT no participa) para detectar brechas; (5) flujo colaborativo de evaluación go/no-go: marcar licitación de interés, análisis único bajo demanda, asignación a trabajadores y comentarios internos."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Filtrar licitaciones por área de negocio (Priority: P1)

Un usuario comercial de TIVIT (ej. Francisco) entra al listado de licitaciones y, en vez de encontrarse con más de 183.000 registros sin relación con el negocio (alumbrado público, vigilancia, etc.), puede filtrar el listado por las áreas donde TIVIT tiene soluciones — Cloud, Ciberseguridad, Digital — para ver solo las licitaciones potencialmente relevantes.

**Why this priority**: Es el bloqueador de usabilidad más citado en la reunión — sin esto, el resto de las funcionalidades (análisis, asignación, drill-down) no importan porque el usuario no llega a encontrar la licitación relevante entre el ruido. Es además prerequisito conceptual para la Historia 5 (marcar una licitación de interés parte de poder encontrarla).

**Independent Test**: Puede probarse de forma aislada abriendo el listado de licitaciones, aplicando el filtro de área de negocio, y verificando que el conteo de resultados se reduce y que las licitaciones mostradas corresponden razonablemente al área elegida — sin necesidad de que ninguna otra historia esté implementada.

**Acceptance Scenarios**:

1. **Given** el listado de licitaciones sin filtrar (183.000+ registros desde 2025), **When** el usuario selecciona el área de negocio "Ciberseguridad", **Then** el sistema muestra solo las licitaciones asociadas a esa área y el total de resultados se reduce visiblemente.
2. **Given** el usuario ya tiene un filtro de área activo, **When** cambia a otra área o lo quita, **Then** el listado se actualiza de inmediato reflejando el nuevo criterio, sin recargar toda la aplicación.
3. **Given** una licitación cuyo texto (nombre/descripción) no calza claramente con ninguna área conocida, **When** se aplican los filtros de área, **Then** la licitación queda fuera de todas las áreas específicas pero sigue siendo accesible desde una vista "todas / sin clasificar", de forma que ninguna licitación desaparece silenciosamente del sistema.

---

### User Story 2 - Ver el desglose estadístico de licitaciones por estado con drill-down (Priority: P2)

Un usuario que hoy solo ve un catálogo estático de qué significa cada estado (publicada, cerrada, desierta, adjudicada, revocada) quiere en cambio ver cuántas licitaciones hay en cada estado, y al hacer clic en un número específico (ej. "desiertas: 4.200"), acceder al listado real de esas licitaciones para investigar por qué.

**Why this priority**: Es la segunda pieza más citada por Francisco ("esto me lo transformes en número... yo pueda ir haciendo un drill down"). Depende conceptualmente de tener un universo de licitaciones ya filtrado (Historia 1) para que las cifras sean útiles y no ruido de 183.000 registros, pero es una funcionalidad de valor propio y demostrable aunque se use sobre el listado completo.

**Independent Test**: Puede probarse navegando a la sección de estadísticas de estado, verificando que los conteos por estado suman el total esperado de licitaciones en el sistema (o del subconjunto filtrado activo), y que al hacer clic en cualquier categoría se llega a un listado filtrado por ese estado específico.

**Acceptance Scenarios**:

1. **Given** el usuario abre la vista de estadísticas de licitaciones, **When** la página carga, **Then** ve un desglose numérico por cada estado existente (publicada, cerrada, desierta, adjudicada, revocada, y cualquier otro estado presente en los datos).
2. **Given** el desglose por estado visible, **When** el usuario hace clic sobre el número correspondiente a "desiertas", **Then** el sistema navega a un listado de licitaciones filtrado exactamente por ese estado.
3. **Given** un filtro de área de negocio activo (Historia 1), **When** el usuario abre las estadísticas de estado, **Then** el desglose refleja solo las licitaciones dentro de esa área, no el total global.

---

### User Story 3 - Ordenar el historial de análisis por fecha de adjudicación de la licitación (Priority: P1)

Un usuario que revisa el listado de análisis generados quiere verlo ordenado de la licitación adjudicada más reciente a la más antigua, no por la fecha en que el sistema ejecutó el análisis — hoy puede ocurrir que un análisis de una licitación de febrero aparezca antes que uno de una licitación adjudicada la semana pasada, si el análisis de febrero se generó después.

**Why this priority**: Causó confusión directa y documentada en la demo ("abrí esta queriendo analizarla pensando que era la última..."). Es un cambio de comportamiento acotado y de bajo riesgo, con alto impacto en la confianza del cliente en la herramienta — se prioriza junto a la Historia 1 porque ambas son correcciones de usabilidad inmediatas sin dependencias entre sí.

**Independent Test**: Puede probarse generando o usando análisis existentes con fechas de adjudicación conocidas y verificando que el listado los muestra en orden descendente por esa fecha, independientemente del orden en que fueron creados en el sistema.

**Acceptance Scenarios**:

1. **Given** dos análisis existentes, uno de una licitación adjudicada en febrero y otro de una adjudicada la semana pasada, **When** el usuario abre el listado de análisis, **Then** el análisis de la licitación adjudicada la semana pasada aparece primero, sin importar cuál de los dos se generó antes en el sistema.
2. **Given** el listado de análisis ordenado por fecha de adjudicación, **When** se agrega un nuevo análisis de una licitación con fecha de adjudicación intermedia entre las existentes, **Then** aparece en la posición cronológica correcta dentro del listado.

---

### User Story 4 - Informe ejecutivo de competidores con actividad total de mercado (Priority: P3)

Un usuario que revisa el informe ejecutivo de competidores hoy solo ve los encuentros directos (licitaciones donde TIVIT y el competidor compitieron juntos). Quiere además saber, para cada competidor ya identificado, cuántas licitaciones en total gana/participa en el mercado — incluyendo las que TIVIT ni siquiera ofertó — para detectar en qué segmentos el competidor está activo y TIVIT tiene una brecha de participación.

**Why this priority**: Fue calificado por el cliente como el hallazgo más valioso de la sesión ("me parece que esto está brutal"), pero requiere datos de licitaciones donde TIVIT no participó, lo cual implica un volumen de análisis mayor y más costoso de obtener que las historias anteriores — por eso queda en P3, después de que el filtrado por área (Historia 1) permita acotar razonablemente ese universo de licitaciones adicionales a analizar.

**Independent Test**: Puede probarse tomando un competidor ya identificado en informes existentes (ej. Entel, Noventiq, Group Partner) y verificando que el informe ejecutivo ahora muestra, además de los encuentros directos, el total de licitaciones y el monto adjudicado de ese competidor en licitaciones donde TIVIT no participó.

**Acceptance Scenarios**:

1. **Given** un competidor con historial de encuentros directos contra TIVIT, **When** el usuario abre el informe ejecutivo de ese competidor, **Then** ve además el total de licitaciones donde ese competidor participó sin TIVIT y el monto total adjudicado en esas licitaciones.
2. **Given** el informe ejecutivo ampliado de un competidor, **When** el usuario revisa las licitaciones donde el competidor participó sin TIVIT, **Then** puede identificar de forma clara cuáles corresponden a áreas de negocio donde TIVIT sí tiene soluciones (oportunidad de expansión) frente a áreas fuera de su oferta actual.
3. **Given** un competidor recién identificado sin historial de encuentros directos aún, **When** se genera su informe, **Then** el sistema indica que no hay datos suficientes en vez de mostrar un error o una sección vacía sin explicación.

---

### User Story 5 - Flujo colaborativo de evaluación go/no-go (Priority: P2)

Un usuario que encuentra una licitación de interés quiere marcarla como tal, disparar un análisis único bajo demanda (no en tiempo real, para controlar el costo de las llamadas a la API de análisis), y luego asignar esa licitación con su análisis a uno o más compañeros de trabajo, quienes pueden dejar comentarios internos para decidir colaborativamente si la empresa participa ("go") o no ("no-go") en esa licitación.

**Why this priority**: Es la funcionalidad de mayor alcance funcional de las cinco (introduce un flujo de trabajo y colaboración nuevo, no solo una mejora de vista existente), por lo que requiere más diseño e implementación. Se prioriza en P2 porque el cliente la calificó como "brutal" y de alto valor de negocio, pero depende de que el usuario ya pueda encontrar y filtrar licitaciones relevantes (Historia 1) antes de marcarlas de interés.

**Independent Test**: Puede probarse de punta a punta marcando una licitación como "de interés", verificando que se dispara un único análisis (no uno por cada persona asignada), asignando esa licitación a dos o más usuarios, y confirmando que cada uno puede ver el análisis compartido y dejar comentarios visibles para el resto del equipo asignado.

**Acceptance Scenarios**:

1. **Given** una licitación en el listado, **When** el usuario la marca como "de interés", **Then** el sistema dispara un análisis único bajo demanda para esa licitación (si aún no existe uno) y no repite el análisis en peticiones posteriores sobre la misma licitación.
2. **Given** una licitación de interés con su análisis ya generado, **When** el usuario la asigna a un grupo de compañeros de trabajo, **Then** todos los asignados pueden ver el mismo análisis y agregar comentarios internos visibles entre sí.
3. **Given** una licitación asignada con comentarios de varios usuarios, **When** cualquiera de los asignados revisa la licitación, **Then** puede identificar claramente quién comentó qué y cuándo, para llegar a una decisión de "go" o "no-go".
4. **Given** una licitación de interés cuyo análisis ya fue generado, **When** un segundo usuario la marca también como de interés o intenta re-analizarla, **Then** el sistema reutiliza el análisis existente en vez de generar uno nuevo y de incurrir en un costo adicional de API.

---

### Edge Cases

- ¿Qué pasa si una licitación no tiene texto suficiente (nombre/descripción muy corto o ambiguo) para clasificarla en ninguna área de negocio? (ver Historia 1, escenario 3 — debe quedar visible en una categoría "sin clasificar", no desaparecer).
- ¿Qué pasa si una licitación cambia de estado (ej. de "publicada" a "revocada") después de haber sido marcada como "de interés" y analizada? El análisis y los comentarios ya generados deben conservarse, y el cambio de estado debe quedar reflejado para que el equipo asignado lo note.
- ¿Qué pasa si dos usuarios distintos marcan la misma licitación como "de interés" casi al mismo tiempo? Solo debe dispararse un análisis (ver Historia 5, escenario 4).
- ¿Qué pasa si un competidor identificado en el informe ejecutivo (Historia 4) no tiene ninguna licitación adicional detectable fuera de los encuentros directos con TIVIT? El informe debe indicarlo explícitamente en vez de mostrar una sección vacía sin contexto.
- ¿Qué pasa si un usuario asignado a una licitación colaborativa (Historia 5) es eliminado o pierde acceso al sistema? Sus comentarios previos deben permanecer visibles para el resto del equipo asignado.
- ¿Qué pasa si el listado de análisis (Historia 3) incluye licitaciones sin fecha de adjudicación registrada (por ejemplo, análisis antiguos o datos incompletos)? Deben ubicarse de forma predecible (ej. al final del listado) en vez de romper el ordenamiento del resto.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST permitir filtrar el listado de licitaciones por una o más áreas de negocio predefinidas (Cloud, Ciberseguridad, Digital), reduciendo el conjunto de resultados mostrado.
- **FR-002**: El sistema MUST clasificar automáticamente cada licitación en cero, una o más áreas de negocio en base a su contenido (nombre, descripción u otros campos disponibles), sin requerir clasificación manual licitación por licitación.
- **FR-003**: El sistema MUST ofrecer una vista "todas / sin clasificar" que permita acceder a las licitaciones que no calzaron con ninguna área de negocio conocida, de forma que ninguna licitación quede inaccesible.
- **FR-004**: El sistema MUST mostrar un desglose numérico de licitaciones agrupadas por estado (publicada, cerrada, desierta, adjudicada, revocada, y cualquier otro estado presente en los datos).
- **FR-005**: El sistema MUST permitir al usuario navegar desde cualquier categoría del desglose por estado hacia el listado de licitaciones correspondiente a ese estado ("drill-down").
- **FR-006**: El desglose por estado MUST respetar el filtro de área de negocio activo cuando el usuario navega a él con un filtro ya aplicado.
- **FR-007**: El sistema MUST ordenar el listado de análisis históricos por la fecha de adjudicación de la licitación asociada (más reciente primero), en vez de por la fecha de creación del análisis.
- **FR-008**: El sistema MUST ubicar de forma predecible (al final del listado) los análisis cuya licitación no tenga fecha de adjudicación registrada.
- **FR-009**: El sistema MUST calcular y mostrar, para cada competidor identificado en el informe ejecutivo, el total de licitaciones en las que participó y el monto total que adjudicó, incluyendo licitaciones donde TIVIT no participó.
- **FR-010**: El sistema MUST distinguir dentro del informe ejecutivo de un competidor entre "encuentros directos con TIVIT" y "actividad total de mercado del competidor", sin mezclar ambas cifras.
- **FR-011**: El sistema MUST indicar explícitamente cuando un competidor no tiene datos suficientes de actividad de mercado, en vez de mostrar una sección vacía sin explicación.
- **FR-012**: El sistema MUST permitir a un usuario marcar una licitación como "de interés".
- **FR-013**: El sistema MUST disparar un único análisis bajo demanda al marcar una licitación como "de interés" por primera vez, y MUST reutilizar el análisis existente en marcados o solicitudes posteriores sobre la misma licitación, sin regenerar el análisis ni incurrir en costo adicional.
- **FR-014**: El sistema MUST permitir asignar una licitación de interés (con su análisis) a uno o más trabajadores.
- **FR-015**: El sistema MUST permitir a los trabajadores asignados a una licitación dejar comentarios internos visibles entre todos los asignados a esa misma licitación.
- **FR-016**: El sistema MUST registrar quién dejó cada comentario y cuándo, de forma visible para todo el equipo asignado a la licitación.
- **FR-017**: El sistema MUST conservar el análisis y los comentarios de una licitación de interés incluso si el estado de la licitación cambia posteriormente (ej. de publicada a revocada), y MUST señalar visualmente ese cambio de estado a los usuarios asignados.
- **FR-018**: El sistema MUST conservar los comentarios de un usuario asignado a una licitación colaborativa aunque ese usuario pierda acceso al sistema posteriormente.

### Key Entities

- **Área de negocio**: Categoría de clasificación de licitaciones (Cloud, Ciberseguridad, Digital) usada para filtrar y agrupar el listado y las estadísticas. Una licitación puede pertenecer a cero, una o varias áreas.
- **Licitación de interés**: Una licitación existente marcada explícitamente por un usuario como candidata a participación, vinculada a un único análisis bajo demanda y, opcionalmente, a un conjunto de trabajadores asignados y comentarios colaborativos.
- **Asignación**: Relación entre una licitación de interés y uno o más trabajadores responsables de revisarla y aportar una decisión go/no-go.
- **Comentario colaborativo**: Anotación interna de un trabajador asignado sobre una licitación de interés, visible para el resto del equipo asignado, con autor y fecha.
- **Informe ejecutivo de competidor**: Vista consolidada de la actividad de un competidor identificado frente a TIVIT, ampliada para incluir su actividad total de mercado (participación y montos adjudicados) más allá de los encuentros directos.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Al aplicar un filtro de área de negocio, el usuario reduce el listado de licitaciones visibles de más de 180.000 registros a un subconjunto acotado y relevante, sin perder acceso a ninguna licitación (las no clasificadas siguen siendo accesibles).
- **SC-002**: El usuario puede pasar de ver un número agregado por estado a ver el listado de licitaciones detrás de ese número en un solo paso (un clic o interacción equivalente).
- **SC-003**: El 100% de los análisis en el listado histórico queda ordenado correctamente por fecha de adjudicación de su licitación, verificable comparando el orden mostrado contra las fechas reales de adjudicación.
- **SC-004**: Para cualquier competidor con actividad de mercado detectable, el informe ejecutivo muestra tanto sus encuentros directos con TIVIT como su actividad total de mercado, permitiendo identificar al menos una brecha de participación potencial por competidor cuando exista.
- **SC-005**: Marcar una licitación como "de interés" y compartirla con un equipo asignado no genera más de un análisis por licitación, sin importar cuántos usuarios la marquen o la revisen.
- **SC-006**: Los usuarios asignados a una licitación colaborativa pueden llegar a una decisión go/no-go documentada (comentarios visibles de al menos los asignados relevantes) sin salir del flujo de la licitación.

## Assumptions

- Las áreas de negocio de TIVIT para la clasificación (Historia 1) son inicialmente Cloud, Ciberseguridad y Digital, tal como se mencionaron explícitamente en la reunión; se asume que esta lista es configurable a futuro pero no que deba serlo desde el lanzamiento de esta feature.
- La clasificación de licitaciones por área de negocio se basa en el contenido textual ya disponible de cada licitación (nombre, descripción); no se asume la necesidad de un proceso de revisión humana individual por licitación.
- El desglose estadístico por estado (Historia 2) reutiliza los estados de licitación ya existentes en el sistema (publicada, cerrada, desierta, adjudicada, revocada, etc.); no se asume la creación de nuevos estados.
- El costo de generar el análisis de una licitación (Historia 5) es suficientemente alto como para requerir que sea "bajo demanda" y compartido entre todo el equipo asignado, en vez de regenerado por cada asignado — este es un requisito explícito del cliente, no una optimización opcional.
- El informe ejecutivo ampliado de competidores (Historia 4) requiere poder observar licitaciones donde TIVIT no participó; se asume que esos datos son obtenibles del mismo origen de datos de licitaciones ya utilizado por el sistema (no un origen de datos externo nuevo).
- Los "trabajadores" a los que se puede asignar una licitación de interés (Historia 5) son usuarios ya existentes del sistema dentro de la misma organización/tenant que quien realiza la asignación.
- La evaluación de viabilidad de despliegue en infraestructura propia (mencionada en la misma reunión por Manuel y Francisco) queda fuera del alcance de esta especificación: es una decisión de infraestructura en evaluación separada, no una funcionalidad de producto a construir. Ver sección "Future Considerations" — no es un bloqueante de las 5 historias de esta spec.

## Future Considerations

- **Despliegue en infraestructura propia (on-premise / data center TIVIT)**: en la misma reunión, Francisco instruyó a Manuel a evaluar la viabilidad técnica y económica de migrar la solución (o parte de ella, ej. el modelo de análisis vía Gema 4 / Qwen cuantizado) al data center propio de TIVIT, para reducir costos de almacenamiento/base de datos y ganar soberanía digital. Esta evaluación está en curso por separado (benchmark de modelos + estimación de costos GCP) y **no bloquea ni condiciona** ninguna de las 5 historias de usuario de esta spec — las funcionalidades descritas aquí deben funcionar igual sobre la infraestructura actual en la nube. Si la migración a infraestructura propia se aprueba más adelante, se traducirá en una spec de infraestructura separada, no en un cambio de alcance de esta.
