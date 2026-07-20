# Feature Specification: Ajustes QoL de Frontend + Fix Scraper "0 Resultados"

**Feature Branch**: `030-qol-frontend-y-fix-scraper`

**Created**: 2026-07-20

**Status**: Cerrado 2026-07-20 — implementado, validado en vivo contra Docker real y revisado por el usuario ("revisado por mi, todo perfecto"). Ver `tasks.md` para el detalle de las 37 tareas y el hallazgo no anticipado (bug de `scheduler.js` en US3, y el bug de columna ambigua reintroducido por copiar V052 en vez de V059 en la migración V113 de US4).

**Input**: User description: "Ajustes menores de QoL en el frontend post-rediseño (019): claridad de datos en /ejecutivo, filtros y orden por fecha en /analisis, rediseño visual de /analisis/:id, /analisis/:id/dashboard y /alertas, fecha correcta en /notificaciones, y diagnóstico + fix del scraper que reporta '0 licitaciones' con exit code 0."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ranking de competidores sin ambigüedad en el Dashboard Ejecutivo (Priority: P1)

Un usuario de negocio (account manager o gerencia) revisa `/ejecutivo` para entender cómo le va a TIVIT frente a un competidor específico. Hoy el ranking muestra tarjetas como "NOVENTIQ INTERNATIONAL CHILE SPA — 5× competidor — 4 ganada(s) — $703.679.633", y la tabla de detalle debajo muestra licitaciones donde TIVIT perdió casi todas. El usuario no puede saber, sin abrir la tabla y leer con cuidado, si "4 ganada(s)" significa que ganó TIVIT o que ganó el competidor — y termina reportando el número equivocado hacia arriba.

**Why this priority**: Es un dato ejecutivo, consumido por gerencia y account managers para tomar decisiones comerciales. Un número ambiguo que se lee al revés (leer "4 ganadas" como victorias de TIVIT cuando son del competidor) es peor que no tener el dato, porque genera confianza falsa. Máximo impacto de negocio del lote.

**Independent Test**: Se puede validar entrando a `/ejecutivo`, mirando cualquier tarjeta de competidor con `vecesGanador > 0`, y confirmando sin abrir la tabla de detalle que el texto deja explícito de quién es esa victoria (del competidor, no de TIVIT).

**Acceptance Scenarios**:

1. **Given** un competidor con licitaciones ganadas por él y perdidas por TIVIT, **When** el usuario ve la tarjeta de ese competidor en el ranking, **Then** el texto indica explícitamente "X licitaciones ganadas por [competidor]" (o equivalente inequívoco), sin dejar lugar a leerlo como una victoria de TIVIT.
2. **Given** la tabla de detalle de un competidor con la columna "Resultado TIVIT", **When** el usuario la compara contra el resumen de la tarjeta, **Then** ambos números son consistentes entre sí y con la etiqueta usada (ganadas por el competidor vs. perdidas por TIVIT son la misma cifra, presentada sin contradicción aparente).
3. **Given** un competidor con 0 licitaciones ganadas por él, **When** el usuario ve su tarjeta, **Then** no se muestra la etiqueta de "ganada(s)" o se muestra en un estado neutro que no sugiera actividad.

---

### User Story 2 - Fecha y hora correctas en Notificaciones (Priority: P1)

Un usuario revisa `/notificaciones` para saber cuándo ocurrió un evento (ej. cuándo terminó el último ciclo del scraper). La fecha mostrada no coincide con la hora real del servidor / la zona horaria de Chile, generando dudas sobre si la notificación es reciente o antigua.

**Why this priority**: Sin una fecha confiable, el usuario no puede confiar en ninguna notificación del sistema — incluidas las de fallas críticas (scraper, sync). Es un problema de confianza transversal a todo el módulo de notificaciones.

**Independent Test**: Se puede validar disparando una notificación de prueba (o comparando el timestamp más reciente contra la hora real del servidor/base de datos al momento de la prueba) y confirmando que lo mostrado en `/notificaciones` coincide, en hora de Chile, con el momento real del evento.

**Acceptance Scenarios**:

1. **Given** una notificación generada en un instante conocido del servidor, **When** el usuario la ve en `/notificaciones`, **Then** la fecha/hora mostrada corresponde a ese instante convertido correctamente a la zona horaria de Chile, sin importar la zona horaria del navegador del usuario.
2. **Given** dos notificaciones generadas con pocos minutos de diferencia, **When** el usuario las compara en la lista, **Then** el orden y las horas relativas mostradas son consistentes con el orden real en que ocurrieron.

---

### User Story 3 - Diagnóstico y corrección del scraper que reporta "0 licitaciones" con código 0 (Priority: P1)

El sistema ya detecta como anómalo un ciclo del scraper que termina con `exitCode == 0` pero `0` licitaciones procesadas (`EsCicloExitoso`), y genera una notificación de advertencia: "El scraper terminó con código 0. Licitaciones: 0, Actas: 0". Este patrón se sigue observando en producción. El equipo necesita entender la causa raíz (sesión colgada, cambio de estructura en el sitio de Mercado Público, paginación rota, timeout silencioso, etc.) y corregirla, para que el scraper vuelva a traer licitaciones de forma confiable en cada ciclo.

**Why this priority**: Si el scraper reporta éxito técnico (código 0) pero no trae datos reales, el catálogo de licitaciones queda desactualizado sin que nadie lo note salvo por esta advertencia — es un riesgo silencioso para el negocio (licitaciones nuevas no llegan a Alertas ni al Buscador). Máxima prioridad funcional, aunque no sea una pantalla de frontend.

**Independent Test**: Se puede validar forzando o esperando un ciclo real del scraper contra Mercado Público y confirmando que procesa un número de licitaciones consistente con lo esperado (no 0) en condiciones normales del sitio; y revisando los logs para confirmar que la causa raíz identificada quedó documentada y mitigada.

**Acceptance Scenarios**:

1. **Given** un ciclo del scraper ejecutado en condiciones normales (Mercado Público disponible, sesión válida), **When** el ciclo termina, **Then** el número de licitaciones procesadas es mayor a 0 y consistente con el volumen esperado del período.
2. **Given** que ocurre una condición que antes producía "0 licitaciones con código 0" silenciosamente tratado como parcialmente exitoso, **When** el ciclo termina en esa condición, **Then** el sistema distingue claramente entre "no había licitaciones nuevas" (caso normal) y "el scraper no pudo leer el sitio" (falla real), y notifica cada caso de forma distinta.
3. **Given** la causa raíz identificada durante el diagnóstico, **When** se revisa el resultado de este trabajo, **Then** existe documentación de qué la causaba y qué cambio la corrigió (o mitigó, si la causa depende de un tercero como Mercado Público).

---

### User Story 4 - Filtrar, ordenar y ver la fecha de análisis en /analisis (Priority: P2)

Un usuario en `/analisis` tiene muchos análisis acumulados y quiere encontrar uno reciente o de un período específico, pero hoy no puede filtrar por fecha, no ve la fecha de cada análisis de forma visible en la lista, y el orden no está garantizado de más reciente a más antiguo.

**Why this priority**: Mejora de productividad diaria (QoL) sobre una pantalla de uso frecuente, pero no bloquea ninguna decisión de negocio como los P1 anteriores.

**Independent Test**: Se puede validar entrando a `/analisis` con varios análisis existentes, confirmando que aparecen ordenados de más reciente a más antiguo por defecto, que cada fila muestra su fecha, y que se puede filtrar por un rango de fechas y obtener solo los análisis de ese rango.

**Acceptance Scenarios**:

1. **Given** una lista de análisis con distintas fechas de creación, **When** el usuario abre `/analisis` sin aplicar ningún filtro, **Then** los análisis aparecen ordenados de fecha más reciente a menos reciente.
2. **Given** la lista de análisis, **When** el usuario mira cualquier fila, **Then** la fecha del análisis es visible sin necesidad de abrir el detalle.
3. **Given** un rango de fechas ingresado por el usuario, **When** aplica el filtro, **Then** solo se muestran los análisis creados dentro de ese rango.

---

### User Story 5 - Rediseño visual de la vista de análisis individual (Priority: P2)

Un usuario abre `/analisis/:id` (workspace de un análisis) y encuentra una interfaz con problemas visuales (jerarquía confusa, espaciado inconsistente, elementos que no se distinguen claramente) que dificultan trabajar cómodamente en ella.

**Why this priority**: Afecta la experiencia de una pantalla de trabajo central del producto (donde se sube y revisa documentación), pero no bloquea la funcionalidad — es una mejora de calidad percibida.

**Independent Test**: Se puede validar mostrando la pantalla rediseñada a un usuario real y confirmando que identifica sin ayuda las secciones principales (documentos, estado del análisis, acciones disponibles) y que la percibe visualmente alineada con el resto del sistema post-rediseño (019).

**Acceptance Scenarios**:

1. **Given** un análisis con documentos cargados y en distintos estados de procesamiento, **When** el usuario abre `/analisis/:id`, **Then** puede identificar el estado de cada documento y las acciones disponibles sin ambigüedad visual.
2. **Given** la nueva versión de la pantalla, **When** se compara contra el resto de las pantallas del sistema (post spec 019), **Then** usa los mismos patrones visuales (tipografía, espaciados, componentes) sin introducir un estilo distinto.

---

### User Story 6 - Rediseño visual del dashboard de resultados de un análisis (Priority: P2)

Un usuario abre `/analisis/:id/dashboard` para ver los resultados de un análisis ya procesado, y encuentra datos repetidos en más de un lugar de la pantalla y una presentación visual poco cuidada, lo que dificulta interpretar los resultados de un vistazo.

**Why this priority**: Es la pantalla donde se consume el valor final del análisis (el resultado de la IA); su claridad visual afecta directamente qué tan rápido el usuario extrae valor del producto. Igual prioridad que el workspace por ser parte del mismo flujo.

**Independent Test**: Se puede validar abriendo el dashboard de un análisis ya completado y confirmando que ningún dato se repite sin razón en dos secciones distintas de la pantalla, y que la jerarquía visual guía al usuario primero hacia los hallazgos más importantes.

**Acceptance Scenarios**:

1. **Given** el dashboard de un análisis completado, **When** el usuario lo recorre, **Then** cada dato relevante aparece en un solo lugar (o, si se repite intencionalmente, con un propósito visualmente claro, ej. un resumen arriba y detalle abajo).
2. **Given** la nueva versión del dashboard, **When** se compara contra el resto de las pantallas del sistema (post spec 019), **Then** usa los mismos patrones visuales que el resto del sistema.

---

### User Story 7 - Rediseño visual de /alertas (Priority: P3)

Un usuario administra sus reglas de alertas en `/alertas` y encuentra una interfaz que no está a la altura visual del resto del sistema rediseñado.

**Why this priority**: Menor frecuencia de uso que las pantallas anteriores (se configura una vez y se revisa ocasionalmente) — mejora de calidad percibida, no bloquea ningún flujo crítico.

**Independent Test**: Se puede validar mostrando la pantalla rediseñada y confirmando que un usuario puede crear, editar y desactivar una regla de alerta sin fricción adicional respecto a la versión anterior, y que visualmente es consistente con el resto del sistema.

**Acceptance Scenarios**:

1. **Given** la pantalla rediseñada de `/alertas`, **When** el usuario crea o edita una regla, **Then** el flujo conserva toda la funcionalidad existente (palabras clave, canal Telegram, activar/desactivar) sin pasos adicionales.
2. **Given** la nueva versión de la pantalla, **When** se compara contra el resto de las pantallas del sistema (post spec 019), **Then** usa los mismos patrones visuales que el resto del sistema.

---

### Edge Cases

- ¿Qué pasa en el ranking de competidores (US1) cuando TIVIT y el competidor empatan (ninguno "gana", licitación desierta o sin adjudicar)? El texto no debe atribuir la licitación a ninguno de los dos como "ganada".
- ¿Qué pasa si el usuario que revisa `/notificaciones` está en una zona horaria distinta a Chile (ej. viaje, VPN)? La fecha debe seguir mostrando la hora de Chile de forma explícita (o marcada), no la hora local del dispositivo sin aclaración.
- ¿Qué pasa si el scraper legítimamente no encuentra licitaciones nuevas en un ciclo (ej. corre en un feriado o fin de semana sin publicaciones)? Este caso no debe notificarse igual que una falla real de lectura del sitio (ver US3, escenario 2).
- ¿Qué pasa con el filtro de fecha en `/analisis` (US4) si el usuario ingresa un rango sin resultados? Debe mostrarse un estado vacío claro, no una lista en blanco sin explicación.
- ¿Qué pasa si un análisis no tiene fecha de finalización (sigue en proceso) al ordenar por fecha en `/analisis`? Debe usar una fecha consistente (ej. fecha de creación) para no romper el orden.

## Requirements *(mandatory)*

### Functional Requirements

**Dashboard Ejecutivo (`/ejecutivo`)**

- **FR-001**: El sistema DEBE mostrar, para cada competidor en el ranking, el número de licitaciones ganadas por ese competidor con una etiqueta que identifique explícitamente al competidor como ganador (no ambigua con una victoria de TIVIT).
- **FR-002**: El sistema DEBE mantener consistencia numérica entre el resumen de la tarjeta de un competidor y su tabla de detalle (misma cifra de licitaciones ganadas por el competidor en ambos lugares).
- **FR-003**: El sistema NO DEBE mostrar la etiqueta de "ganada(s)" en la tarjeta de un competidor cuando ese competidor tiene 0 licitaciones ganadas.

**Notificaciones (`/notificaciones`)**

- **FR-004**: El sistema DEBE mostrar la fecha y hora de cada notificación convertida a la zona horaria de Chile, independiente de la zona horaria configurada en el navegador o dispositivo del usuario.
- **FR-005**: El sistema DEBE persistir y transmitir los timestamps de notificaciones de forma inequívoca respecto a su zona horaria de origen (ej. UTC explícito), de modo que el frontend pueda convertirlos correctamente.

**Scraper — ciclo "0 licitaciones" con código 0**

- **FR-006**: El sistema DEBE identificar la causa raíz por la cual un ciclo del scraper puede terminar con código de salida 0 mientras procesa 0 licitaciones, y corregirla o mitigarla.
- **FR-007**: El sistema DEBE distinguir, en su notificación de resultado, entre "ciclo exitoso sin licitaciones nuevas que reportar" y "ciclo con código 0 pero sin poder leer datos reales del sitio" (la anomalía actual), usando mensajes y/o niveles de severidad distintos para cada caso.

**Análisis — listado (`/analisis`)**

- **FR-008**: El sistema DEBE mostrar la lista de análisis ordenada por fecha de más reciente a menos reciente por defecto.
- **FR-009**: El sistema DEBE mostrar la fecha del análisis de forma visible en cada fila de la lista, sin requerir abrir el detalle.
- **FR-010**: El sistema DEBE permitir filtrar la lista de análisis por un rango de fechas.

**Análisis — workspace (`/analisis/:id`) y dashboard (`/analisis/:id/dashboard`)**

- **FR-011**: El sistema DEBE rediseñar la interfaz de `/analisis/:id` para eliminar inconsistencias visuales (jerarquía, espaciado, agrupación) y alinearla con los patrones visuales establecidos en el rediseño frontend (spec 019).
- **FR-012**: El sistema DEBE rediseñar la interfaz de `/analisis/:id/dashboard`, eliminando la repetición no intencional de datos y mejorando la jerarquía visual de los resultados.
- **FR-013**: Los rediseños de `/analisis/:id` y `/analisis/:id/dashboard` DEBEN preservar toda la funcionalidad existente (subida de documentos, chat de análisis, visualización de hallazgos) sin remover capacidades.

**Alertas (`/alertas`)**

- **FR-014**: El sistema DEBE rediseñar la interfaz de `/alertas`, alineándola con los patrones visuales establecidos en el rediseño frontend (spec 019), preservando toda la funcionalidad existente de creación, edición y activación/desactivación de reglas.

**Fuera de alcance (confirmado con el usuario)**

- **FR-015**: `/catalogos` se mantiene como página independiente en el menú principal — no se modifica su ubicación ni estructura en este spec.
- **FR-016**: `/mensajes` no requiere cambios en este spec.

### Key Entities

- **Notificación**: Registro de un evento del sistema (ej. resultado de ciclo del scraper) con un timestamp; la representación correcta de ese timestamp en la zona horaria del usuario es el foco de US2.
- **Ranking de competidor**: Agregado por competidor con conteo de licitaciones donde participó y conteo de licitaciones que ganó él (no TIVIT); el foco de US1 es que esta distinción quede explícita en la UI.
- **Análisis**: Unidad de trabajo sobre uno o más documentos de una licitación, con una fecha de creación/finalización usada para ordenar y filtrar en US4.
- **Ciclo de scraper**: Ejecución individual del proceso de sincronización, caracterizada por un código de salida y un conteo de licitaciones/actas procesadas; el foco de US3 es diagnosticar por qué puede reportar éxito técnico sin datos reales.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un usuario de negocio que revisa el ranking de competidores en `/ejecutivo` identifica correctamente, en el 100% de los casos de prueba, quién ganó cada licitación listada sin necesidad de abrir la tabla de detalle.
- **SC-002**: El timestamp mostrado en `/notificaciones` coincide con la hora real del servidor (zona horaria de Chile) con un margen de error menor a 1 minuto, verificado contra al menos 3 notificaciones generadas en momentos conocidos.
- **SC-003**: Tras el fix, el scraper procesa un número de licitaciones mayor a 0 en el 100% de los ciclos ejecutados bajo condiciones normales del sitio de Mercado Público, durante al menos una semana de operación posterior al despliegue.
- **SC-004**: Cuando el scraper legítimamente no encuentra licitaciones nuevas, la notificación generada es distinguible (mensaje o severidad distinta) de una notificación de falla real, verificado en al menos un caso de cada tipo.
- **SC-005**: Un usuario puede encontrar un análisis específico dentro de un rango de fechas conocido en menos de 15 segundos usando el nuevo filtro, sin necesidad de recorrer manualmente la lista completa.
- **SC-006**: Las pantallas rediseñadas (`/analisis/:id`, `/analisis/:id/dashboard`, `/alertas`) no presentan ningún dato duplicado sin propósito visual claro, verificado por revisión manual de cada pantalla contra su versión anterior.
- **SC-007**: Ningún flujo funcional existente (crear/editar alerta, subir documento, ver resultados de análisis) se rompe o pierde pasos como consecuencia de los rediseños visuales, verificado por regresión manual de cada flujo.

## Assumptions

- El "rediseño visual completo" de `/analisis/:id`, `/analisis/:id/dashboard` y `/alertas` (US5, US6, US7) reutiliza los mismos patrones, componentes y librería visual establecidos en el rediseño frontend previo (spec `019-rediseno-frontend`) y en el rediseño de chat/análisis con Ant Design X (spec `025-rediseno-chat-analisis-antd-x`) — no introduce una librería o sistema de diseño nuevo.
- `/catalogos` se mantiene sin cambios de ubicación ni estructura (decisión confirmada con el usuario) — queda fuera de alcance de este spec.
- `/mensajes` no requiere cambios — queda fuera de alcance de este spec.
- El diagnóstico del scraper (US3) puede requerir cambios tanto en el proceso Node del scraper (`tools/scraper-mp`) como en el `ScraperBackgroundService` del backend que interpreta su resultado — ambos se consideran dentro de alcance porque el pedido del usuario fue "diagnosticar y arreglarlo dentro de este mismo spec".
- La corrección del scraper (US3) puede terminar siendo una mitigación (ej. reintentos, mejor detección de sesión colgada) en lugar de una eliminación total del riesgo, si la causa raíz depende de cambios en el sitio de Mercado Público fuera del control del equipo — en ese caso, FR-007 (distinguir el caso legítimo del caso de falla) se vuelve el requisito mínimo innegociable aunque la causa de fondo no se elimine por completo.
- El filtro de fecha en `/analisis` (US4) filtra por la fecha de creación del análisis, salvo que el equipo de implementación determine que "fecha del análisis" debe referirse a otra fecha (ej. fecha de finalización) durante el diseño técnico.
