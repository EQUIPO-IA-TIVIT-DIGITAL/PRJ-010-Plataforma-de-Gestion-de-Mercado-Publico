# Feature Specification: Migración del proveedor de IA — Gemini 2.5 Pro → Qwen 3.7 (G4)

**Feature Branch**: `033-migracion-qwen-g4`

**Created**: 2026-08-11

**Status**: Implemented — US1/US2/US3/US4 y documentación de US5 completas en código; pendientes de entorno/negocio: ejecución real del benchmark (URL Qwen + ADC), validación manual con stack completo (DB local) y drill de cutover/rollback en staging (US5). Ver `tasks.md`.

**Input**: User description: "El sistema usa hoy Google Gemini 2.5 Pro (análisis de PDFs de licitaciones, chat, búsqueda semántica y sinónimos). Se está a punto de decidir migrar el análisis IA a Qwen 3.7 cuantizado (G4). El sistema debe poder cambiar de proveedor de modelo de IA sin tocar código, validar que la calidad de extracción sigue siendo aceptable y permitir rollback. El super admin (admin@tivit.cl) tendrá un switch en la interfaz para mover entre gcloud y qwen, porque el sistema se mudará a otra infraestructura privada. No habrá uso de Google a partir de la migración; la URL del servidor Qwen será entregada por el equipo."

## User Scenarios & Testing

### User Story 1 — El sistema puede cambiar de proveedor de IA por configuración, sin recompilar (Priority: P1)

El sistema de análisis de licitaciones no depende de un proveedor de IA específico. La operación puede elegir qué proveedor de modelo se usa (hoy Gemini, mañana Qwen) cambiando una configuración, sin modificar código ni interrumpir el servicio. El proveedor actual (Gemini) queda como opción por defecto y debe seguir funcionando exactamente igual que hoy.

**Why this priority**: Es la base de todo. Sin esta abstracción, migrar a Qwen significa tocar código en producción. Con ella, la migración y el rollback son decisiones operativas, no desarrollos.

**Independent Test**: Levantar el sistema completo con la configuración por defecto y verificar que el análisis de PDFs, la búsqueda semántica, los sinónimos y el análisis de competidores producen exactamente los mismos resultados que antes del cambio (regresión funcional completa). Cero cambios visibles para el usuario.

**Acceptance Scenarios**:

1. **Given** el sistema desplegado con la configuración por defecto, **When** se ejecuta la suite de análisis de una licitación completa (documentos → análisis → puntuaciones → chat), **Then** todos los pasos funcionan igual que antes y la base de datos registra el mismo nombre de modelo que hoy.
2. **Given** un ambiente de pruebas, **When** se cambia la configuración de proveedor a una opción no disponible, **Then** el sistema arranca sin fallar y reporta un error claro al intentar usarla, sin afectar al proveedor por defecto.

---

### User Story 2 — El equipo decide con datos si Qwen 3.7 G4 cumple la calidad (Priority: P1)

Antes de tocar producción, el equipo ejecuta una evaluación comparativa entre el modelo actual y Qwen 3.7 cuantizado, sobre documentos reales de licitaciones. La evaluación mide qué tan bien extrae cada modelo los campos críticos (fechas, montos, criterios, puntuaciones), cuánto tarda, y si produce JSON válido. El resultado es un informe de go/no-go documentado. **Umbral acordado: ≥ 90% de campos críticos idénticos, con revisión manual de discrepancias (montos y criterios con prioridad de revisión).**

**Why this priority**: La decisión de migrar (o no) debe tomarse con evidencia. Un modelo cuantizado puede degradar la extracción estructurada sin que se note en un análisis casual; medirlo evita un regreso silencioso de calidad en producción.

**Independent Test**: Ejecutar la evaluación sobre un conjunto acotado de documentos reales y obtener un informe con métricas comparativas (paridad de campos, tasa de JSON válido, latencia) para los dos modelos. No requiere cambios en producción.

**Acceptance Scenarios**:

1. **Given** un conjunto de documentos reales de licitaciones con análisis ya completados por el modelo actual, **When** se ejecuta la evaluación comparativa con Qwen, **Then** el informe muestra la paridad campo a campo entre ambos modelos y una recomendación go/no-go explícita contra el umbral del 90%.
2. **Given** una discrepancia en campos críticos entre modelos, **When** se revisa el informe, **Then** cada discrepancia queda identificada con el documento y el campo afectado, para decisión humana (montos y criterios primero).
3. **Given** el informe generado, **When** se decide la migración, **Then** el informe queda guardado como evidencia de la decisión (go/no-go + métricas).

---

### User Story 3 — El sistema analiza licitaciones usando Qwen 3.7 cuantizado (Priority: P2)

El sistema puede ejecutar el análisis de PDFs (y el resto de usos de IA) contra Qwen 3.7 cuantizado G4, servido como API compatible con el estándar OpenAI. La URL del servidor la entrega el equipo proveedor. El contrato de salida es idéntico al actual: JSON estructurado con los mismos campos, guardado en la misma base de datos.

**Why this priority**: Es la materialización de la migración, pero depende de que US1 y US2 hayan pasado (abstracción lista + calidad validada).

**Independent Test**: Configurar un ambiente de pruebas con Qwen servido en la URL provista, ejecutar el análisis de una licitación completa y verificar que el resultado JSON se guarda correctamente y se muestra en el frontend sin cambios.

**Acceptance Scenarios**:

1. **Given** la URL del servidor Qwen entregada por el equipo, **When** se configura el sistema para usarlo y se analiza una licitación con PDFs reales, **Then** el análisis se completa y el JSON extraído se persiste con el nombre del nuevo modelo registrado.
2. **Given** el análisis completado con Qwen, **When** el usuario abre la licitación en el frontend, **Then** ve los mismos campos, secciones y puntuaciones que vería con el modelo anterior.
3. **Given** Qwen responde con JSON mal formado o se cae el servicio, **When** se intenta el análisis, **Then** el error se maneja con el mismo contrato de errores de hoy y el estado del análisis queda consistente (no corrupto).

---

### User Story 4 — El super admin alterna entre gcloud y qwen desde la interfaz (Priority: P2)

El super administrador (admin@tivit.cl, rol SuperAdmin) tiene un switch en la interfaz que le permite cambiar el proveedor de IA activo entre Google (Gemini) y Qwen, sin intervención técnica ni reinicio del servicio. El cambio se registra (quién y cuándo) y aplica a los análisis siguientes. Esto es lo que permite la mudanza a infraestructura privada: se alterna según dónde corra el sistema.

**Why this priority**: Es el mecanismo operativo que hace la migración y la reversa una decisión de negocio, no de desarrollo. Sin él, cada cambio de infraestructura requiere despliegue.

**Independent Test**: Iniciar sesión como super admin, cambiar el switch de gcloud a qwen y verificar que el análisis siguiente usa el nuevo proveedor sin reiniciar nada; luego volver a gcloud y verificar la reversa. Un usuario sin rol SuperAdmin no ve ni puede usar el switch.

**Acceptance Scenarios**:

1. **Given** un super admin autenticado, **When** cambia el proveedor de gcloud a qwen, **Then** el análisis siguiente se ejecuta con Qwen, sin reinicio del servicio, y el cambio queda registrado con usuario y fecha.
2. **Given** un usuario sin rol SuperAdmin, **When** intenta acceder al switch o a la API correspondiente, **Then** no tiene acceso (403) y no ve el control en la interfaz.
3. **Given** el sistema apuntando al proveedor seleccionado, **When** se reinicia el servicio, **Then** el sistema recuerda el proveedor seleccionado (no vuelve al anterior por defecto).
4. **Given** un cambio de proveedor en curso, **When** se inicia un análisis, **Then** el análisis usa el proveedor activo al momento de su ejecución y persiste ese modelo.

---

### User Story 5 — La operación migra a Qwen en producción con rollback garantizado (Priority: P2)

Producción pasa a usar Qwen 3.7 G4 como proveedor principal del análisis (sin uso de Google a partir de la migración, salvo contingencia). La migración se hace con el switch del super admin y queda documentada como procedimiento; el rollback a Gemini se puede ejecutar en cualquier momento con el mismo switch (o por configuración si la interfaz no está disponible). La documentación de infraestructura se actualiza.

**Why this priority**: Cierra el ciclo y materializa "no habrá uso de Google", pero solo tiene sentido después de US3 y US4 validadas.

**Independent Test**: Ejecutar el runbook de migración en un ambiente que replica producción: cambiar el proveedor con el switch a Qwen, verificar operación, y ejecutar el rollback (switch + fallback por configuración) verificando que el servicio vuelve a operar con Gemini sin pérdida de datos.

**Acceptance Scenarios**:

1. **Given** producción operando con Qwen, **When** se ejecuta el procedimiento de rollback documentado (switch o configuración), **Then** el sistema vuelve a operar con Gemini en menos de 30 minutos, sin pérdida de análisis pendientes.
2. **Given** la migración completada, **When** se revisa la documentación de infraestructura, **Then** refleja el nuevo proveedor, la URL, las variables de entorno y los pasos de rollback.
3. **Given** un análisis realizado durante la ventana de migración, **When** se consulta su historial, **Then** queda registrado con qué modelo se ejecutó realmente.

---

### Edge Cases

- El servicio que sirve Qwen (infraestructura privada) está caído o inaccesible: el sistema debe fallar limpio con el mismo contrato de errores de hoy y dejar el análisis en estado consistente (reintentable), nunca corrupto.
- Qwen devuelve JSON válido pero con campos fuera del contrato esperado (nulos, formatos distintos de fecha/monto): el parser existente debe aplicar las mismas reglas de tolerancia/normalización que con Gemini.
- Respuestas truncadas por límite de tokens: Qwen con contexto largo cuantizado puede truncar; debe detectarse y registrarse igual que se hizo con Gemini (maxOutputTokens ya se subió a 65536 por un bug real de truncamiento).
- Documentos PDF muy grandes o escaneados: el mecanismo de referencia a archivo en GCS que usa Gemini no existe en el formato OpenAI-compatible; el equivalente (base64 inline o extracción de texto) debe cubrir el mismo rango de documentos sin romper el análisis multi-documento.
- Cambio de proveedor con análisis en curso: los análisis que empezaron con un modelo deben persistir el modelo que realmente los ejecutó, no el activo al momento de guardar.
- Latencia mayor de lo esperado en Qwen G4: el análisis síncrono actual debe mantener tiempos razonables o el runbook debe contemplar el impacto antes del cutover.
- Dos administradores cambian el proveedor casi al mismo tiempo: el último cambio gana y ambos cambios quedan registrados (el sistema no se corrompe).
- La configuración persistida del proveedor no está disponible (tabla vacía o error de BD al arrancar): el sistema usa la configuración de entorno como respaldo y sigue operando.
- El switch cambia el proveedor mientras hay análisis pendientes en cola: los análisis pendientes se ejecutan con el proveedor activo en el momento de su ejecución, y cada uno registra su modelo real.

## Requirements

### Functional Requirements

- **FR-001**: El sistema DEBE permitir seleccionar el proveedor de modelo de IA mediante configuración, sin cambios de código.
- **FR-002**: El proveedor actual (Gemini) DEBE seguir funcionando como opción por defecto con comportamiento idéntico al actual.
- **FR-003**: El sistema DEBE registrar en la base de datos el modelo que realmente ejecutó cada análisis.
- **FR-004**: El contrato de salida de IA (JSON estructurado de análisis) NO DEBE cambiar para los consumidores (frontend, reportes, chat).
- **FR-005**: El sistema DEBE poder ejecutar una evaluación comparativa de calidad entre dos modelos sobre documentos reales y producir un informe de go/no-go.
- **FR-006**: El nuevo proveedor DEBE ser accesible mediante una API estándar de chat/completions (formato compatible con OpenAI), con la URL provista por el equipo.
- **FR-007**: El sistema DEBE manejar fallos del nuevo proveedor (timeout, caída, JSON inválido) con el mismo contrato de errores de hoy y sin corromper el estado del análisis.
- **FR-008**: El rollback a Gemini DEBE ser posible sin cambios de código ni de base de datos (switch o configuración).
- **FR-009**: La migración NO DEBE requerir cambios en la interfaz de usuario ni en los contratos HTTP existentes (excepto el nuevo control de administración de US4).
- **FR-010**: La decisión de migrar DEBE basarse en un informe comparativo que cumpla un umbral de paridad de ≥ 90% de campos críticos idénticos, con revisión manual de discrepancias (montos y criterios con prioridad).
- **FR-011**: Los cuatro usos actuales de IA (análisis de PDFs, chat, búsqueda semántica, sinónimos de alertas) DEBEN migrar al nuevo proveedor; el sistema no debe depender de Google a partir de la migración.
- **FR-012**: El endpoint del nuevo proveedor DEBE ser configurable (URL entregada por el equipo proveedor), sin código hardcodeado.
- **FR-013**: El super administrador (rol SuperAdmin) DEBE poder cambiar el proveedor activo desde la interfaz, con efecto inmediato y sin reinicio.
- **FR-014**: El cambio de proveedor desde la interfaz DEBE quedar registrado (usuario, fecha/hora, proveedor anterior y nuevo).
- **FR-015**: El proveedor seleccionado desde la interfaz DEBE persistir entre reinicios del servicio.
- **FR-016**: Solo usuarios con rol SuperAdmin DEBEN poder ver y usar el switch; cualquier otro acceso debe ser denegado.
- **FR-017**: La configuración persistida DEBE tener precedencia sobre la configuración de entorno, y la de entorno sobre el valor por defecto (gemini).
- **FR-018**: El proveedor activo DEBE poder consultarse en cualquier momento desde la interfaz (estado actual y último cambio).

### Key Entities

- **Análisis (existente)**: Registro de análisis de una licitación; ya persiste `modelo_usado` (varchar) — no requiere cambios de esquema.
- **Configuración de proveedor IA (nueva)**: Entidad persistida con el proveedor activo, endpoint, modelo, quién lo cambió y cuándo. Global (no por tenant) — es infraestructura del sistema.
- **Configuración de entorno (existente)**: Variables `AI:Provider`, `AI:Endpoint`, `AI:Model`, `AI:ApiKey` — actúan como valor inicial/respaldo.

## Success Criteria

### Measurable Outcomes

- **SC-001**: El cambio de proveedor de IA se realiza solo con configuración o con el switch del super admin, sin despliegue.
- **SC-002**: El cambio de proveedor vía switch tiene efecto en el análisis siguiente, en menos de 1 minuto, sin reinicio del servicio.
- **SC-003**: El rollback desde Qwen a Gemini se completa en menos de 30 minutos sin pérdida de datos.
- **SC-004**: La regresión del proveedor por defecto no introduce cambios visibles en ninguna pantalla del sistema ni en los contratos HTTP.
- **SC-005**: La evaluación comparativa cubre al menos 10 documentos reales y entrega paridad campo a campo, tasa de JSON válido, latencia y recomendación go/no-go contra el umbral del 90%.
- **SC-006**: El 100% de los análisis registran el modelo que realmente los ejecutó.
- **SC-007**: El 100% de los cambios de proveedor por el switch quedan auditados (usuario, fecha, valores).
- **SC-008**: Ningún usuario sin rol SuperAdmin puede ver ni usar el switch (verificado en API y UI).

## Assumptions

- Qwen 3.7 G4 se refiere a una cuantización de 4 bits (formato GGUF Q4 o equivalente) servida vía API compatible con OpenAI (vLLM, Ollama o llama.cpp); el identificador exacto del modelo se confirma con quien provea el servicio.
- La URL del servidor Qwen será entregada por el equipo proveedor antes de implementar US3; hasta entonces se usa un placeholder configurable.
- La infraestructura privada (mudanza) se está evaluando en paralelo; esta feature cubre el software del sistema, no la provisión de hardware.
- Gemini queda implementado como proveedor de rollback y como opción "gcloud" del switch, incluso después de la migración definitiva a Qwen.
- El contrato de errores, los estados de análisis y el pipeline de persistencia actuales se reutilizan sin cambios.
- El benchmark se ejecuta con documentos reales de licitaciones, sin subirlos al repositorio (lección ya aprendida: un `benchmark/` con documentos reales se removió del repo en 2026).
- No se requieren cambios en las tablas de análisis para esta migración; solo se agrega la tabla de configuración del proveedor.
