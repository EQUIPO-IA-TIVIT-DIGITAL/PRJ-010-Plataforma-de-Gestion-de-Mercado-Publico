# Feature Specification: Corrección de Hallazgos QA Pre-Producción

**Feature Branch**: `022-qa-fixes-preproduccion`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "Correcciones de hallazgos QA técnicos pre-producción (auditoría 2026-07-07, Jean Franco Benique). 13 bugs (3 críticos, 8 altos, 2 medios) verificados contra el código actual — los 13 siguen presentes (11 sin corregir, 2 parcialmente corregidas). Prioridad: primero los bloqueantes del deploy del jueves 9 de julio 2026 7:59am (coincide con specs/002-fase5-deploy-gcp/), luego seguridad/negocio, luego performance/robustez menor."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - El sistema nunca queda en producción con un esquema de base de datos a medias (Priority: P1)

Como responsable de operar el sistema, necesito que si una migración de base de datos falla durante el arranque, el servicio NO quede disponible sirviendo tráfico con un esquema incompleto — debe fallar de forma visible y detenerse, en vez de reportar "healthy" mientras las pantallas que dependen de las tablas nuevas revientan en producción.

**Why this priority**: Bloqueante #1 confirmado por QA (BUG-001, Crítico) para el deploy del jueves. Un esquema a medias en un entorno stateless con múltiples instancias arrancando en paralelo (Cloud Run) puede además producir carreras entre instancias aplicando la misma migración.

**Independent Test**: Introducir una migración que falle en un entorno de staging, desplegar, y verificar que el servicio no queda disponible (o se detiene) en vez de responder "healthy" con tablas faltantes. Arrancar dos instancias a la vez y verificar que no compiten por aplicar la misma migración.

**Acceptance Scenarios**:

1. **Given** una migración de base de datos que falla al aplicarse, **When** el servicio intenta arrancar, **Then** el arranque se detiene de forma visible (el servicio no queda disponible) en vez de continuar con un esquema incompleto.
2. **Given** dos instancias del servicio arrancando simultáneamente, **When** ambas intentan aplicar migraciones pendientes, **Then** solo una las aplica a la vez y la otra espera, sin duplicar ni corromper el proceso.
3. **Given** todas las migraciones aplicadas correctamente, **When** el servicio arranca, **Then** el comportamiento es idéntico al actual (sin regresiones).

---

### User Story 2 - El análisis de un documento siempre termina, sin importar reinicios o escalado de instancias (Priority: P1)

Como analista, necesito que cuando subo un documento para análisis con IA, el proceso se complete y el resultado aparezca, incluso si la instancia del servidor que lo procesó se reinicia, escala a cero, o queda momentáneamente sin CPU asignada.

**Why this priority**: Bloqueante #2 confirmado por QA (BUG-002, Crítico). El mecanismo actual (disparar el análisis en segundo plano justo después de responder la petición HTTP) no sobrevive al comportamiento normal de un entorno como Cloud Run, donde la CPU se puede retirar a una instancia entre peticiones. Hoy el análisis queda "procesando" para siempre sin que el usuario pueda saberlo ni recuperarlo.

**Independent Test**: Subir un documento para análisis en el entorno de despliegue objetivo, forzar que la instancia que lo recibió quede sin CPU o se reinicie antes de terminar, y verificar que el análisis igual se completa (por la misma instancia al recuperar CPU, o por otra) sin intervención manual.

**Acceptance Scenarios**:

1. **Given** un documento subido para análisis, **When** la instancia que lo procesa se reinicia o pierde CPU a mitad del proceso, **Then** el análisis se retoma y se completa sin que el usuario deba volver a subir el documento.
2. **Given** un análisis en curso, **When** el usuario cierra la pantalla o pierde la conexión, **Then** el análisis continúa y el resultado está disponible al volver a abrir el workspace.
3. **Given** el mecanismo corregido, **When** se despliega en el entorno objetivo (Cloud Run), **Then** ya no depende de que la instancia mantenga CPU asignada fuera del ciclo de vida de una petición HTTP.

---

### User Story 3 - La sincronización de licitaciones y el scraper se ejecutan una sola vez, en el lugar correcto (Priority: P1)

Como responsable de operar el sistema, necesito que la sincronización de licitaciones y el scraper se ejecuten exactamente una vez por ciclo, en el proceso designado para ello, sin que el servicio web también los dispare por su cuenta, y que el scraper pueda conectarse a la base de datos gestionada del entorno de producción.

**Why this priority**: Bloqueantes #3 y #4 confirmados por QA (BUG-004 y BUG-005, Altos) para el deploy del jueves. Hoy el servicio web arranca sus propios temporizadores de sincronización y scraping en paralelo a los procesos dedicados para esa tarea (duplicando trabajo y carga), y el scraper intenta conectarse a un nombre de servidor de base de datos que solo existe en el entorno local, por lo que en producción no puede conectarse en absoluto.

**Independent Test**: Desplegar el servicio web junto con los procesos dedicados de sincronización/scraper en el entorno objetivo, y verificar en un ciclo completo que: (a) la sincronización ocurre una sola vez, no dos, y (b) el scraper se conecta exitosamente a la base de datos de producción.

**Acceptance Scenarios**:

1. **Given** el servicio web desplegado junto con los procesos dedicados de sincronización y scraper, **When** transcurre un ciclo completo, **Then** cada licitación se sincroniza una sola vez, no dos.
2. **Given** el scraper ejecutándose como proceso dedicado en el entorno de producción, **When** intenta conectarse a la base de datos, **Then** se conecta exitosamente a la base de datos gestionada del entorno (no a un nombre de servidor que solo existe en desarrollo local).
3. **Given** el entorno de desarrollo local (Docker Compose), **When** se levanta el stack completo, **Then** la sincronización y el scraper siguen funcionando igual que hoy (sin regresión local).

---

### User Story 4 - El scraper distingue un cambio real del sitio de un cupo agotado, y avisa de inmediato si algo falla (Priority: P1)

Como responsable de operar el sistema, necesito que cuando Mercado Público cambie la estructura de su sitio (y no solo cuando se agote una cuota de uso), el scraper lo detecte y avise a una persona de inmediato en vez de reintentar silenciosamente durante horas; que el proceso del scraper nunca quede colgado por generar mucha salida de error; y que cualquier falla real (o un ciclo con cero resultados, que es anómalo) llegue a un canal donde alguien realmente lo vea.

**Why this priority**: Bloqueante crítico de continuidad operativa (BUG-003, Crítico; BUG-006 y BUG-007, Altos). Sin esto, un cambio de estructura del sitio puede detener silenciosamente la ingesta de licitaciones nuevas durante horas sin que nadie se entere, lo cual es indistinguible de "todo funciona bien" desde afuera.

**Independent Test**: Simular un cambio de estructura del sitio (ej. renombrar el selector que el scraper busca) y verificar que el scraper se detiene rápido y genera una alerta visible a una persona, en vez de reintentar durante horas. Simular un ciclo con salida de error abundante y verificar que el proceso siempre termina. Simular una falla o un ciclo con cero resultados y verificar que la alerta llega a un canal real (no a un buzón inexistente).

**Acceptance Scenarios**:

1. **Given** un cambio en la estructura del sitio de Mercado Público (elemento esperado ausente), **When** el scraper lo detecta, **Then** se detiene en un tiempo acotado (no ~3 horas de reintentos) y genera una alerta distinguible de "cupo agotado".
2. **Given** un ciclo del scraper con salida de error abundante por consola, **When** el proceso hijo se ejecuta, **Then** el ciclo siempre termina (no se cuelga) y toda la salida queda registrada.
3. **Given** una falla del scraper o un ciclo con cero licitaciones encontradas, **When** finaliza el ciclo, **Then** una persona del equipo recibe la alerta por un canal donde realmente la vea (no una notificación in-app dirigida a un usuario inexistente), y un ciclo con cero resultados NO se reporta como éxito.

---

### User Story 5 - La API solo acepta tráfico del frontend autorizado y nunca arranca con un secreto de sesión por defecto (Priority: P2)

Como responsable de seguridad, necesito que la API rechace peticiones con credenciales desde cualquier origen que no sea el frontend autorizado, y que nunca use un secreto de firma de sesión conocido/por defecto si falta la configuración — debe impedir el arranque en ese caso, no continuar con un valor inseguro.

**Why this priority**: Riesgo de seguridad confirmado por QA (BUG-011, Alto). Actualmente cualquier sitio web puede hacer peticiones autenticadas contra la API, y si el secreto de sesión no está configurado, el sistema usa un valor fijo conocido embebido en el código, lo cual permitiría falsificar sesiones.

**Independent Test**: Desde un origen web distinto al del frontend, intentar una petición autenticada contra la API y verificar que es rechazada. Desplegar sin configurar el secreto de sesión y verificar que el servicio no arranca.

**Acceptance Scenarios**:

1. **Given** una petición autenticada desde un origen que no es el frontend autorizado, **When** llega a la API, **Then** es rechazada.
2. **Given** el frontend autorizado, **When** realiza peticiones autenticadas, **Then** funcionan con normalidad (sin regresión).
3. **Given** un despliegue sin el secreto de sesión configurado, **When** el servicio intenta arrancar, **Then** el arranque falla de forma visible en vez de continuar con un valor por defecto inseguro.

---

### User Story 6 - El webhook de Telegram rechaza cualquier petición que no pueda verificar (Priority: P2)

Como responsable de seguridad, necesito que el endpoint que recibe actualizaciones de Telegram rechace cualquier petición que no incluya la credencial secreta esperada, incluyendo el caso en que esa credencial no esté configurada — nunca debe procesar una actualización sin poder verificarla.

**Why this priority**: Riesgo de seguridad confirmado por QA (BUG-009, Alto). Hoy, si el secreto no está configurado (como ocurre actualmente), el webhook omite la validación por completo y cualquiera que conozca la URL puede vincular chats de Telegram arbitrarios a cuentas del sistema.

**Independent Test**: Sin el secreto del webhook configurado, enviar una petición al endpoint del webhook sin la cabecera de credencial y verificar que es rechazada (no procesada).

**Acceptance Scenarios**:

1. **Given** el secreto del webhook sin configurar, **When** llega una petición al webhook sin la credencial esperada, **Then** es rechazada.
2. **Given** el secreto del webhook configurado correctamente, **When** Telegram envía una actualización con la credencial correcta, **Then** se procesa con normalidad (sin regresión).
3. **Given** una credencial incorrecta, **When** llega al webhook, **Then** es rechazada.

---

### User Story 7 - Se puede medir cuántas personas usan el sistema y con qué frecuencia (Priority: P2)

Como responsable de negocio, necesito poder consultar un registro de los inicios de sesión (quién, cuándo) para medir la adopción del sistema, ya que hoy no existe ninguna forma de saberlo.

**Why this priority**: Pedido de negocio confirmado por QA (BUG-010, Alto) con deadline propio (día 16 del mes). No bloquea el deploy del jueves pero es indispensable para el siguiente hito de negocio.

**Independent Test**: Iniciar sesión varias veces con distintos usuarios y verificar que existe un registro consultable de esos inicios de sesión.

**Acceptance Scenarios**:

1. **Given** un inicio de sesión exitoso, **When** ocurre, **Then** queda un registro consultable (usuario y fecha, como mínimo).
2. **Given** varios inicios de sesión de distintos usuarios en un período, **When** se consulta el registro, **Then** se puede determinar cuántos usuarios distintos iniciaron sesión y con qué frecuencia.

---

### User Story 8 - La búsqueda de licitaciones responde rápido aunque haya muchos registros (Priority: P2)

Como usuario, necesito que buscar licitaciones por texto siga respondiendo rápido a medida que la base de datos crece, en vez de degradarse porque la búsqueda no aprovecha el índice de texto ya existente.

**Why this priority**: Confirmado por QA (BUG-008, Alto) como riesgo de performance progresivo — no es un bloqueante inmediato del jueves, pero empeora con el tiempo y ya existe un mecanismo de búsqueda por texto correcto (usado en otro endpoint) que el listado principal no aprovecha.

**Independent Test**: Con la base de datos cargada con un volumen representativo de licitaciones, medir el tiempo de respuesta de una búsqueda por texto en el listado principal antes y después del cambio.

**Acceptance Scenarios**:

1. **Given** un volumen representativo de licitaciones, **When** el usuario busca por texto en el listado principal, **Then** el tiempo de respuesta no se degrada de forma notoria con el volumen.
2. **Given** una búsqueda por texto, **When** se ejecuta, **Then** los resultados son equivalentes en relevancia a los del comportamiento actual (sin pérdida de resultados esperados).

---

### User Story 9 - Las alertas de licitaciones se procesan de forma eficiente y siempre llegan a Telegram (Priority: P3)

Como responsable de negocio, necesito que el proceso de emparejar licitaciones nuevas con reglas de alerta no genere carga innecesaria en la base de datos, y que un mensaje de alerta se entregue por Telegram siempre, sin importar qué caracteres tenga el nombre de la licitación.

**Why this priority**: Confirmado por QA como hallazgos de severidad media (BUG-012 y BUG-013). No bloquean el deploy ni representan un riesgo de seguridad, pero degradan la confiabilidad de una funcionalidad que ya está en producción (Fase 6).

**Independent Test**: Ejecutar un ciclo de sincronización con muchas licitaciones nuevas y varias reglas de alerta, y verificar la cantidad de consultas a la base de datos durante el proceso. Disparar una alerta cuya licitación tenga un nombre con guion bajo o asterisco y verificar que el mensaje llega a Telegram.

**Acceptance Scenarios**:

1. **Given** un ciclo de sincronización con múltiples licitaciones que generan alertas, **When** se procesan, **Then** la lista de destinatarios se consulta una sola vez para todo el ciclo, no una vez por licitación.
2. **Given** una licitación cuyo nombre contiene caracteres especiales (guion bajo, asterisco, u otros usados en formato de texto), **When** se dispara una alerta para esa licitación, **Then** el mensaje llega correctamente a Telegram sin fallar por formato.
3. **Given** un envío a Telegram que no responde, **When** transcurre un tiempo razonable de espera, **Then** el intento se da por fallido y se registra, sin bloquear el resto del proceso por un tiempo excesivo.

---

### Edge Cases

- ¿Qué pasa si una migración falla después de que otras ya se aplicaron correctamente en el mismo arranque? El sistema debe detenerse sin dejar el esquema en un estado ambiguo, y debe quedar claro cuál fue la última migración aplicada con éxito.
- ¿Qué pasa si dos instancias arrancan casi al mismo tiempo mientras hay migraciones pendientes? Una debe aplicar las migraciones mientras la otra espera; ninguna debe arrancar a medias.
- ¿Qué pasa si un análisis de documento falla por un error real del documento (no por pérdida de CPU)? El usuario debe ver un estado de error claro, no "procesando" indefinidamente.
- ¿Qué pasa si el scraper detecta un cambio de estructura pero en realidad era un problema transitorio de red? La alerta debe permitir a una persona confirmar o descartar manualmente, sin quedar el sistema bloqueado de forma permanente.
- ¿Qué pasa si el mismo cambio de configuración de CORS bloquea sin querer una integración legítima futura? Debe existir una forma documentada de agregar un nuevo origen autorizado sin reabrir la política a cualquier origen.
- ¿Qué pasa si se recibe una alerta de Telegram con la credencial correcta pero de un remitente inesperado? Sigue siendo válida mientras la credencial coincida (Telegram es la única fuente que la conoce); no se requiere validación adicional de origen.
- ¿Qué pasa con los inicios de sesión durante el período en que el registro aún no existía? No hay datos retroactivos; la medición de adopción comienza desde el despliegue de este cambio.
- ¿Qué pasa si una búsqueda de texto no coincide con ningún término indexado pero sí con el código de la licitación? La búsqueda debe seguir cubriendo ambos casos (nombre y código) tras el cambio.

## Requirements *(mandatory)*

### Functional Requirements

**Migraciones de base de datos (US1 / BUG-001)**

- **FR-001**: El sistema DEBE detener su arranque (no quedar disponible para servir tráfico) si una migración de base de datos falla al aplicarse.
- **FR-002**: El sistema DEBE impedir que dos o más instancias apliquen migraciones pendientes de forma concurrente y descoordinada.

**Análisis de documentos (US2 / BUG-002)**

- **FR-003**: El sistema DEBE garantizar que un análisis de documento iniciado se complete y su resultado quede disponible, sin depender de que la misma instancia que lo inició mantenga el proceso vivo ininterrumpidamente hasta el final.
- **FR-004**: El sistema DEBE poder recuperar y continuar análisis que quedaron incompletos por un reinicio o pérdida de proceso, sin intervención manual del usuario.

**Sincronización y scraper (US3 / BUG-004, BUG-005)**

- **FR-005**: El sistema DEBE ejecutar la sincronización de licitaciones y el scraping de adjuntos exactamente una vez por ciclo, sin duplicar la ejecución entre el servicio web y los procesos dedicados a esa tarea.
- **FR-006**: El scraper DEBE conectarse a la base de datos usando la configuración del entorno en el que se ejecuta, no un valor fijo que solo es válido en desarrollo local.

**Resiliencia y observabilidad del scraper (US4 / BUG-003, BUG-006, BUG-007)**

- **FR-007**: El scraper DEBE distinguir un cambio de estructura del sitio de Mercado Público de un cupo de uso agotado, y detenerse en un tiempo acotado ante lo primero (no reintentar durante horas).
- **FR-008**: El sistema DEBE alertar a una persona del equipo, por un canal donde efectivamente la vea, cuando el scraper detecte un posible cambio de estructura del sitio.
- **FR-009**: El proceso del scraper DEBE completarse siempre (sin quedar colgado) independientemente del volumen de salida de error que genere.
- **FR-010**: El sistema DEBE alertar a una persona del equipo, por un canal donde efectivamente la vea, cuando el scraper falle o cuando un ciclo termine con cero licitaciones encontradas (resultado anómalo).

**Seguridad de acceso a la API (US5 / BUG-011)**

- **FR-011**: La API DEBE rechazar peticiones autenticadas (con credenciales) que provengan de un origen distinto al del frontend autorizado.
- **FR-012**: El sistema DEBE impedir su arranque si el secreto usado para firmar sesiones no está configurado, en vez de continuar con un valor por defecto.

**Webhook de Telegram (US6 / BUG-009)**

- **FR-013**: El webhook de Telegram DEBE rechazar cualquier petición que no incluya la credencial secreta correcta, incluyendo el caso en que esa credencial no esté configurada en el sistema.

**Medición de adopción (US7 / BUG-010)**

- **FR-014**: El sistema DEBE registrar cada inicio de sesión exitoso (usuario y fecha, como mínimo) de forma consultable.

**Búsqueda de licitaciones (US8 / BUG-008)**

- **FR-015**: La búsqueda por texto en el listado principal de licitaciones DEBE aprovechar el mecanismo de indexación de texto ya existente en el sistema, de forma que el tiempo de respuesta no se degrade de forma notoria con el volumen de datos.

**Alertas y Telegram (US9 / BUG-012, BUG-013)**

- **FR-016**: El sistema DEBE consultar la lista de destinatarios de alertas una sola vez por ciclo de sincronización, no una vez por cada licitación con coincidencias.
- **FR-017**: El sistema DEBE entregar un mensaje de alerta por Telegram correctamente sin importar los caracteres de formato de texto que contenga el nombre de la licitación.
- **FR-018**: El sistema DEBE limitar el tiempo de espera de un envío a Telegram a un valor acotado y razonable, registrando el fallo si se excede.

### Key Entities

- **Migración de base de datos**: cambio versionado del esquema, aplicado en orden; su éxito o fallo determina si el sistema puede arrancar.
- **Análisis de documento**: proceso de evaluación por IA de un PDF subido a un workspace; tiene un estado (pendiente, procesando, completado, error) que debe reflejar la realidad incluso ante interrupciones.
- **Ciclo de sincronización/scraping**: ejecución periódica que trae licitaciones nuevas y sus adjuntos; debe ser exclusiva de un solo ejecutor por ciclo.
- **Alerta de estado del scraper**: aviso dirigido a una persona del equipo ante una falla, un cambio de estructura del sitio, o un resultado anómalo (cero licitaciones).
- **Origen autorizado**: dominio del frontend desde el cual la API acepta peticiones autenticadas.
- **Secreto de sesión / secreto de webhook**: credenciales que deben existir y ser válidas para que el sistema opere; su ausencia debe impedir el funcionamiento correspondiente, no degradarlo silenciosamente.
- **Evento de inicio de sesión**: registro de que un usuario específico inició sesión en una fecha determinada, usado para medir adopción.
- **Regla de alerta / destinatario**: configuración de palabras clave y la lista de personas (account managers) que deben recibir la alerta cuando una licitación coincide.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de los intentos de arranque con una migración fallida resultan en un servicio no disponible (en vez de "healthy" con esquema incompleto), verificado en pruebas de staging.
- **SC-002**: El 100% de los análisis de documentos de prueba se completan y muestran resultado, incluso simulando pérdida de CPU o reinicio de la instancia que los inició.
- **SC-003**: En un ciclo de sincronización de prueba, cada licitación se procesa exactamente una vez (cero duplicados detectables en logs o en base de datos).
- **SC-004**: El scraper, desplegado en el entorno de producción objetivo, se conecta exitosamente a la base de datos en el 100% de las ejecuciones de prueba.
- **SC-005**: Ante un cambio de estructura simulado del sitio, el scraper se detiene y genera una alerta visible en menos de 10 minutos (en vez de hasta 3 horas).
- **SC-006**: El 100% de las fallas del scraper (incluyendo ciclos con cero resultados) generan una alerta que llega a un canal monitoreado por el equipo, verificado en pruebas.
- **SC-007**: El 100% de las peticiones autenticadas desde un origen no autorizado son rechazadas en pruebas; el frontend legítimo mantiene 0% de fallas nuevas por CORS.
- **SC-008**: El 100% de las peticiones al webhook de Telegram sin la credencial correcta son rechazadas, incluyendo cuando la credencial no está configurada.
- **SC-009**: Existe un registro consultable con el 100% de los inicios de sesión exitosos ocurridos desde el despliegue de este cambio.
- **SC-010**: El tiempo de respuesta de una búsqueda por texto en el listado principal de licitaciones no aumenta más del 20% al duplicar el volumen de datos de prueba (frente a una degradación notoria hoy).
- **SC-011**: El número de consultas a la base de datos para resolver destinatarios de alerta en un ciclo de sincronización es independiente de la cantidad de licitaciones con coincidencias (una sola consulta por ciclo).
- **SC-012**: El 100% de las alertas de prueba con nombres de licitación que incluyen caracteres especiales de formato se entregan correctamente por Telegram.

## Assumptions

- Este feature agrupa exclusivamente los 13 hallazgos del informe QA técnico del 2026-07-07 (`QA/QA-Técnico-CU010 Mercado Publico.docx`); no incluye el documento QA adjunto por separado (`QA-CU0010 Mercado Público. (1).docx`), que corresponde a una plantilla de otro proyecto TIVIT (menciona Next.js, SSO Microsoft Entra ID y módulos que no existen en este sistema) y no es aplicable a MPM.
- Las User Stories 1 a 4 (P1) son las bloqueantes del deploy del jueves 9 de julio 2026, según la priorización explícita del QA (BUG-001, 002, 004, 005) más los hallazgos críticos/altos de continuidad del scraper (BUG-003, 006, 007) que comparten el mismo riesgo de "silencio operativo" en el entorno nuevo.
- Las User Stories 5 a 7 (P2) son necesarias antes de considerar el sistema listo para producción real (seguridad) o antes del deadline de negocio del día 16 (adopción), pero no impiden técnicamente el deploy del jueves si el entorno sigue siendo de acceso controlado.
- Las User Stories 8 y 9 (P3) son mejoras de confiabilidad y performance sobre funcionalidad ya en producción; se abordan después de los bloqueantes sin comprometer la fecha del jueves.
- Cada corrección debe preservar el comportamiento funcional actual para el usuario final (nadie pierde una capacidad existente); el objetivo es cerrar brechas de confiabilidad, seguridad y operación, no rediseñar funcionalidad.
- El detalle técnico de cada hallazgo (archivo, línea, snippet de código) vive en el informe QA original y en el hallazgo verificado de esta sesión; este spec describe el comportamiento requerido, no la implementación — la fase de planificación (`/speckit-plan`) decidirá el enfoque técnico concreto para cada corrección.
- La infraestructura pendiente de terceros (rol `roles/aiplatform.user` para Gemini/Vertex, Cloud SQL con IP privada, Memorystore — bloqueada en Nicolás Valdivia) queda fuera del alcance de este feature de código; se seguirá gestionando en `specs/002-fase5-deploy-gcp/solicitud-recursos-cloud-run.md`.
