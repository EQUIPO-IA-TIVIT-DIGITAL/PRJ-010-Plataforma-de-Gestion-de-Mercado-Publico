# Feature Specification: Ajustes Urgentes del Cliente — UI/UX, Sesión y Coherencia del Análisis

**Feature Branch**: `017-ajustes-urgentes-cliente`

**Created**: 2026-07-01

**Status**: Draft

**Input**: User description: "Lote de ajustes urgentes solicitados por el cliente, con dos niveles de prioridad: prioridad crítica (para mañana) — corregir error JWT (vence el token y no manda a login, todo queda en 401), login solo usuario y contraseña, sidebar con admin TIVIT y foto sin correo, borrar notificaciones, rediseño de licitaciones (espaciado, buscador duplicado, quitar búsqueda inteligente y sincronizar, reiniciar filtros, datos 2025-2026 actualizados cada semana), catálogos con explicaciones, análisis mucho mejor con chat en nueva vista y mejor PDF, ejecutivo con mejor diseño e investigación (solo investigar) de por qué se ganan licitaciones. Ahora mismo: validar por cada licitación los archivos enviados vs. el veredicto ganado/perdido, comparativa de documentos en resumen, mejorar pantalla de licitaciones y generación de PDF."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sesión expirada redirige a login (Priority: P1)

Como usuario del sistema, cuando mi sesión expira mientras trabajo, necesito que la aplicación me lleve automáticamente a la pantalla de login con un mensaje claro, en lugar de quedar "rota" mostrando errores en cada pantalla que abro.

**Why this priority**: Es el defecto más visible y bloqueante del sistema hoy: al vencer el token, toda la aplicación queda inutilizable devolviendo errores de autorización en silencio, y el usuario no entiende qué pasó ni cómo recuperarse. Afecta a todos los usuarios, todos los días.

**Independent Test**: Se puede probar iniciando sesión, forzando la expiración de la sesión (o esperando su vencimiento), y realizando cualquier acción: el sistema debe cerrar la sesión y llevar al login con un aviso, sin dejar pantallas con errores visibles.

**Acceptance Scenarios**:

1. **Given** un usuario con la sesión expirada, **When** realiza cualquier acción que consulte datos (abrir licitaciones, notificaciones, análisis, etc.), **Then** el sistema cierra la sesión, lo redirige al login y muestra un mensaje indicando que la sesión expiró.
2. **Given** un usuario con la sesión expirada en cualquier página, **When** ocurre la redirección al login y vuelve a autenticarse, **Then** puede seguir trabajando con normalidad sin errores residuales.
3. **Given** un usuario con sesión vigente, **When** usa la aplicación con normalidad, **Then** no ocurre ningún cierre de sesión ni redirección inesperada.

---

### User Story 2 - Coherencia del análisis con los documentos enviados (Priority: P1)

Como analista, necesito que por cada licitación analizada el sistema verifique los archivos que efectivamente enviamos y valide si el veredicto de ganado/perdido tiene sentido — por ejemplo, si el análisis afirma que perdimos por no enviar un documento, pero ese documento sí fue enviado, el sistema debe detectar y señalar esa inconsistencia. Además necesito ver en el resumen una comparativa de documentos (los requeridos por la licitación vs. los que enviamos).

**Why this priority**: La credibilidad del análisis es el corazón del producto. Un veredicto de pérdida basado en un hecho falso ("no enviaron el documento X" cuando sí se envió) destruye la confianza del cliente en todo el sistema. Es parte del lote "ahora mismo".

**Independent Test**: Se puede probar con una licitación cuya acta de evaluación mencione un documento faltante que sí figura entre los archivos enviados: el análisis debe señalar la discrepancia en lugar de repetir el motivo sin cuestionarlo, y el resumen debe mostrar la comparativa de documentos requeridos vs. enviados.

**Acceptance Scenarios**:

1. **Given** una licitación analizada donde el acta indica pérdida por un documento faltante, **When** ese documento sí consta entre los archivos enviados, **Then** el análisis marca la inconsistencia de forma visible, distinguiendo lo que dice el acta de lo que evidencian los documentos enviados.
2. **Given** una licitación analizada, **When** el usuario revisa el resumen, **Then** ve una comparativa de documentos: cuáles fueron requeridos, cuáles se enviaron y cuáles aparecen observados o faltantes según el acta.
3. **Given** una licitación donde el veredicto y los documentos enviados son coherentes, **When** el usuario revisa el análisis, **Then** la validación confirma la coherencia sin generar falsas alarmas.

---

### User Story 3 - Rediseño de la pantalla de Licitaciones (Priority: P1)

Como usuario, necesito una pantalla de licitaciones más compacta y clara: sin espaciados excesivos, con un solo buscador (hoy hay dos casi idénticos), sin funciones que confunden (búsqueda inteligente, botón sincronizar), con un botón para reiniciar los filtros, y con datos vigentes del período 2025-2026 que se actualicen automáticamente cada semana.

**Why this priority**: Es la pantalla principal de trabajo diario y la que el cliente observó con más problemas concretos de usabilidad. Es parte del lote crítico para mañana y del lote "ahora mismo" (mejorar diseño de la pantalla de licitaciones).

**Independent Test**: Se puede probar abriendo la pantalla de licitaciones y verificando: un único campo de búsqueda, ausencia del modo de búsqueda inteligente y del botón sincronizar, presencia del botón reiniciar filtros funcionando, espaciado compacto y datos del período 2025-2026.

**Acceptance Scenarios**:

1. **Given** la pantalla de licitaciones, **When** el usuario la abre, **Then** existe un único campo de búsqueda por código o nombre (sin duplicados) y no aparecen ni el modo "búsqueda inteligente" ni el botón "sincronizar".
2. **Given** filtros aplicados (búsqueda, estado, tipo, fechas), **When** el usuario pulsa "Reiniciar filtros", **Then** todos los filtros vuelven a su estado inicial y la lista se recarga completa.
3. **Given** la pantalla de licitaciones, **When** el usuario revisa los datos, **Then** encuentra licitaciones del período 2025-2026, y los datos se renuevan automáticamente al menos una vez por semana sin intervención manual.
4. **Given** el rediseño aplicado, **When** se compara con la versión anterior, **Then** el contenido útil visible sin hacer scroll es mayor (menos espacio desperdiciado entre filtros, tarjetas y tabla).

---

### User Story 4 - Mejoras de Análisis: calidad, chat en vista propia y PDF (Priority: P2)

Como analista, necesito que el análisis de cada licitación sea notoriamente más profundo y útil, que el chat contextual (que funciona bien) esté disponible en una vista propia dedicada para hacer consultas con más comodidad, que las respuestas del chat lleguen siempre con formato correcto y legible, y que la exportación a PDF tenga calidad profesional (hoy es una captura de pantalla).

**Why this priority**: El análisis es el diferenciador del producto y el PDF es el artefacto que se comparte con terceros; su calidad actual no representa bien el trabajo. Va después de P1 porque el sistema es usable sin esto, pero es parte del compromiso de mañana.

**Independent Test**: Se puede probar generando un análisis y su PDF: el documento exportado debe ser legible, con texto seleccionable y estructura de documento (no una imagen), y el chat debe poder abrirse en su propia vista manteniendo el contexto de la licitación.

**Acceptance Scenarios**:

1. **Given** una licitación con análisis completado, **When** el usuario abre la vista de consulta del chat contextual, **Then** accede a una vista dedicada donde puede conversar sobre esa licitación, con el mismo comportamiento del chat actual.
2. **Given** una conversación en el chat contextual, **When** el asistente responde, **Then** la respuesta se muestra con formato correcto y consistente (títulos, listas, tablas legibles), sin texto crudo mal renderizado.
3. **Given** un análisis completado, **When** el usuario exporta a PDF, **Then** obtiene un documento con texto real (seleccionable), paginación correcta y estructura profesional, incluyendo la comparativa de documentos del resumen.
4. **Given** los ajustes de calidad del análisis, **When** se analiza una licitación de prueba conocida, **Then** el resultado cubre con mayor profundidad los motivos del resultado, fortalezas/debilidades y recomendaciones accionables, validado contra el criterio del equipo.

---

### User Story 5 - Ajustes generales de interfaz (Priority: P2)

Como usuario, necesito una serie de ajustes de interfaz solicitados por el cliente: (a) login solo con usuario y contraseña, sin enlace de "¿olvidaste tu contraseña?"; (b) en la parte baja del sidebar, ver la foto/avatar del usuario con la etiqueta "admin TIVIT", sin mostrar el correo; (c) poder borrar notificaciones (una a una y todas); (d) en catálogos, al hacer click sobre un concepto, ver una explicación de qué significa (qué es una licitación pública, un trato directo, el estado publicada, etc.); (e) un dashboard ejecutivo con mejor diseño visual.

**Why this priority**: Son ajustes visibles y de bajo riesgo que el cliente espera ver mañana, pero ninguno bloquea la operación actual.

**Independent Test**: Cada ajuste es verificable de forma independiente abriendo la pantalla correspondiente y comprobando el cambio solicitado.

**Acceptance Scenarios**:

1. **Given** la pantalla de login, **When** el usuario la abre, **Then** solo ve campos de usuario y contraseña (y el acceso), sin enlace de recuperación de contraseña.
2. **Given** un usuario autenticado, **When** observa la parte inferior del sidebar, **Then** ve su foto/avatar y la etiqueta "admin TIVIT", y no se muestra su correo electrónico.
3. **Given** la bandeja de notificaciones con elementos, **When** el usuario elimina una notificación (o todas), **Then** desaparecen de su bandeja de forma permanente y el contador se actualiza.
4. **Given** la pantalla de catálogos, **When** el usuario hace click sobre un estado o tipo (ej. "Licitación Pública", "Trato Directo", "Publicada"), **Then** ve una explicación en lenguaje simple de qué significa ese concepto.
5. **Given** el dashboard ejecutivo, **When** el usuario lo abre tras el rediseño, **Then** la información existente se presenta con una jerarquía visual más clara y un diseño más cuidado, sin perder contenido.

---

### User Story 6 - Investigación: por qué se ganan licitaciones (Priority: P3) — SOLO INVESTIGAR, NO IMPLEMENTAR

Como responsable del negocio, necesito una investigación documentada sobre si es factible determinar por qué ciertas entidades ganan licitaciones (por ejemplo, a partir de datos de la entidad compradora, historial de adjudicaciones, patrones en las actas), como insumo para decidir un futuro desarrollo. El entregable es un documento de hallazgos; explícitamente NO se implementa nada.

**Why this priority**: Es exploratorio y no compromete la entrega de mañana; su valor es informar una decisión futura.

**Independent Test**: Existe un documento de hallazgos que responde: qué fuentes de datos hay disponibles, qué se puede inferir con ellas, qué limitaciones existen y una recomendación de siguiente paso.

**Acceptance Scenarios**:

1. **Given** la investigación finalizada, **When** se revisa el entregable, **Then** es un documento (no código) que identifica fuentes de datos disponibles, viabilidad, limitaciones y recomendación.
2. **Given** el alcance acordado, **When** se revisa el trabajo realizado, **Then** no se introdujo ningún cambio de comportamiento en el sistema por esta user story.

---

### Edge Cases

- ¿Qué pasa si la sesión expira justo durante el envío de un formulario (ej. escribiendo en el chat)? El sistema debe redirigir al login sin perder la aplicación en un estado inconsistente.
- ¿Qué pasa con la conexión de tiempo real (mensajería/notificaciones) cuando la sesión expira? Debe cerrarse ordenadamente junto con la sesión.
- ¿Qué pasa si una licitación no tiene registro de los documentos enviados? La comparativa debe indicar que no hay información de envíos, sin inventar datos.
- ¿Qué pasa si el acta de evaluación no menciona documentos faltantes? La validación de coherencia no debe generar advertencias vacías.
- ¿Qué pasa al borrar una notificación no leída? El contador de no leídas debe actualizarse de inmediato.
- ¿Qué pasa si el usuario no tiene foto? El sidebar debe mostrar un avatar con iniciales, manteniendo la etiqueta "admin TIVIT".
- ¿Qué pasa con un análisis muy extenso al exportar PDF? El documento debe paginar correctamente sin cortar tablas o secciones a la mitad de forma ilegible.
- ¿Qué pasa si la actualización semanal de datos falla? Debe quedar registro del fallo y reintentarse, sin dejar los datos silenciosamente desactualizados.

## Requirements *(mandatory)*

### Functional Requirements

**Sesión (US1)**

- **FR-001**: Cuando cualquier operación del sistema responda que la sesión no es válida o expiró, el sistema DEBE cerrar la sesión del usuario, redirigirlo a la pantalla de login y mostrar un mensaje indicando que la sesión expiró.
- **FR-002**: La redirección por sesión expirada DEBE ocurrir una sola vez aunque varias operaciones fallen simultáneamente (sin mensajes duplicados ni bucles de redirección).
- **FR-003**: Tras volver a iniciar sesión, el usuario DEBE poder operar con normalidad sin estados residuales de la sesión anterior.

**Coherencia del análisis (US2)**

- **FR-004**: Por cada licitación analizada, el sistema DEBE contrastar los documentos que la licitación exigía, los documentos efectivamente enviados y las observaciones del acta de evaluación (documentos faltantes u observados).
- **FR-005**: Cuando el motivo de pérdida declarado en el acta contradiga la evidencia de los documentos enviados (ej. "faltó el documento X" pero X fue enviado), el sistema DEBE señalar la inconsistencia de forma explícita y visible en el análisis, distinguiendo la versión del acta de la evidencia propia.
- **FR-006**: El resumen del análisis DEBE incluir una comparativa de documentos: requeridos vs. enviados vs. observados/faltantes según el acta, con el estado de cada documento.
- **FR-007**: Si no existe información de los documentos enviados para una licitación, la comparativa DEBE indicarlo explícitamente en lugar de asumir que no se envió nada.

**Pantalla de Licitaciones (US3)**

- **FR-008**: La pantalla de licitaciones DEBE tener un único campo de búsqueda por código o nombre, eliminando el buscador duplicado actual.
- **FR-009**: La pantalla de licitaciones NO DEBE mostrar el modo "búsqueda inteligente" ni el botón "sincronizar".
- **FR-010**: La pantalla de licitaciones DEBE ofrecer una acción "Reiniciar filtros" que restablezca todos los filtros a su estado inicial y recargue la lista.
- **FR-011**: El diseño de la pantalla DEBE compactarse (menor espaciado entre búsqueda, filtros y tabla) de modo que se vea más contenido útil sin scroll.
- **FR-012**: El sistema DEBE mantener los datos de licitaciones actualizados para el período 2025-2026, con una actualización automática al menos semanal, sin requerir acción manual del usuario.
- **FR-013**: Si la actualización automática falla, DEBE quedar un registro consultable del fallo y el sistema DEBE reintentar en el siguiente ciclo.

**Análisis, chat y PDF (US4)**

- **FR-014**: El chat contextual del análisis DEBE estar disponible en una vista propia dedicada a consultas, conservando el comportamiento y contexto del chat actual.
- **FR-015**: Las respuestas del chat DEBEN presentarse siempre con formato correcto y consistente (estructura, listas y tablas legibles); el sistema DEBE validar/normalizar el formato antes de mostrarlas.
- **FR-016**: La exportación a PDF del análisis DEBE producir un documento con texto real y seleccionable, paginación correcta y estructura profesional (no una captura de imagen), incluyendo la comparativa de documentos.
- **FR-017**: La calidad del análisis DEBE mejorar en profundidad: motivos del resultado, fortalezas y debilidades, brechas de puntaje y recomendaciones accionables, verificable contra un conjunto de licitaciones de prueba acordado con el equipo.

**Ajustes de interfaz (US5)**

- **FR-018**: La pantalla de login DEBE mostrar solo usuario y contraseña, sin enlace de recuperación de contraseña. (El mecanismo interno de recuperación puede seguir existiendo; solo se elimina su acceso desde el login.)
- **FR-019**: La parte inferior del sidebar DEBE mostrar la foto/avatar del usuario y la etiqueta "admin TIVIT", y NO DEBE mostrar el correo electrónico.
- **FR-020**: El usuario DEBE poder eliminar notificaciones de su bandeja, tanto individualmente como todas a la vez, con actualización inmediata del contador de no leídas.
- **FR-021**: En catálogos, al hacer click sobre un concepto (estado o tipo de licitación), el sistema DEBE mostrar una explicación en lenguaje simple de qué significa (ej. licitación pública, trato directo, publicada).
- **FR-022**: El dashboard ejecutivo DEBE presentar la información existente con un diseño visual mejorado, sin eliminar contenido actual.

**Investigación (US6)**

- **FR-023**: Se DEBE entregar un documento de investigación sobre la factibilidad de determinar por qué se ganan licitaciones (fuentes de datos disponibles, viabilidad, limitaciones, recomendación). Esta user story NO DEBE producir cambios de comportamiento en el sistema.

**Fuera de alcance**

- La sección de Mensajes se mantiene sin cambios.

### Key Entities

- **Sesión de usuario**: estado de autenticación del usuario; tiene vigencia y expira; su expiración debe manejarse de forma visible y ordenada.
- **Licitación**: proceso de compra pública con código, nombre, estado, tipo, fechas y monto; base de las pantallas de listado y análisis.
- **Documento requerido**: antecedente que la licitación exige presentar para ofertar.
- **Documento enviado**: archivo que la empresa efectivamente presentó en una licitación.
- **Observación del acta**: mención en el acta de evaluación sobre documentos faltantes, observados o motivos del resultado.
- **Comparativa de documentos**: cruce entre documentos requeridos, enviados y observaciones del acta, con estado por documento e indicador de inconsistencias.
- **Notificación**: aviso en la bandeja del usuario; ahora además de leerse puede eliminarse.
- **Concepto de catálogo**: estado o tipo de licitación con nombre, y ahora una explicación en lenguaje simple.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de las sesiones expiradas terminan en una redirección al login con mensaje claro; cero pantallas quedan mostrando errores de autorización sin explicación.
- **SC-002**: En un conjunto de licitaciones de prueba con inconsistencias conocidas (acta dice "documento faltante" pero fue enviado), el sistema detecta y señala al menos el 90% de las inconsistencias, sin falsas alarmas en los casos coherentes.
- **SC-003**: El 100% de los análisis muestran la comparativa de documentos en su resumen (o la indicación explícita de que no hay información de envíos).
- **SC-004**: En la pantalla de licitaciones rediseñada, el usuario encuentra y usa "Reiniciar filtros" sin ayuda, y no existe más de un campo de búsqueda; el contenido útil visible sin scroll aumenta respecto a la versión anterior.
- **SC-005**: Los datos de licitaciones del período 2025-2026 nunca superan los 7 días de antigüedad respecto a la fuente, sin intervención manual.
- **SC-006**: El PDF exportado contiene texto seleccionable y paginación correcta en el 100% de los análisis de prueba, incluyendo análisis extensos.
- **SC-007**: El 100% de las respuestas del chat se muestran con formato legible (sin bloques de texto crudo mal renderizado) en las pruebas de aceptación.
- **SC-008**: Los ajustes de interfaz (login, sidebar, borrar notificaciones, explicaciones de catálogo, dashboard ejecutivo) pasan la revisión del cliente en la sesión de mañana.
- **SC-009**: Existe el documento de investigación sobre factibilidad de predecir/explicar victorias, con fuentes, limitaciones y recomendación, sin cambios de código asociados.

## Assumptions

- Quitar el enlace "¿olvidaste tu contraseña?" del login no implica eliminar el flujo interno de recuperación (páginas y soporte backend pueden conservarse sin acceso visible); si el cliente pide eliminarlo por completo, se tratará como ajuste posterior.
- "admin TIVIT" es la etiqueta de rol a mostrar bajo el sidebar para el usuario administrador; si existen otros roles, se muestra el rol correspondiente del usuario con el mismo formato.
- "Actualizar cada semana" reemplaza la necesidad del botón manual "sincronizar": la actualización pasa a ser exclusivamente automática con cadencia al menos semanal, cubriendo el período 2025-2026.
- La comparativa de documentos se construye con la información que el sistema ya conoce: requisitos de la licitación, archivos registrados como enviados y observaciones extraídas del acta de evaluación.
- El chat contextual actual se considera correcto en comportamiento ("el chat está perfecto"); el cambio es de ubicación (vista propia) y de garantía de formato, no de lógica conversacional.
- Borrar una notificación la elimina de la bandeja del usuario de forma permanente; no se requiere papelera ni restauración.
- La mejora del dashboard ejecutivo es de presentación (jerarquía visual, diseño); no se agregan nuevas métricas en este lote.
- La sección de Mensajes queda explícitamente fuera del alcance de este feature.
