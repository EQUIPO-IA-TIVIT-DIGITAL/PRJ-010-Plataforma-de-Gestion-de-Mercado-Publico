# Feature Specification: Corrección urgente de bugs detectados en producción (Mensajería y Alertas)

**Feature Branch**: `023-fix-bugs-produccion`

**Created**: 2026-07-09

**Status**: Draft

**Input**: User description: "Feature urgente post-demo: corrección de bugs reales detectados en vivo durante el deploy a producción y la demo de Fase 6 (2026-07-09). BUG-014 (crear conversación falla por mismatch 'directa' vs 'directo'). BUG-015 (auto-vinculación de Telegram nunca habilita la entrega real de alertas porque no marca es_account_manager_gobierno=TRUE)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Crear una conversación nueva en Mensajería (Priority: P1)

Un usuario autenticado abre el módulo de Mensajes, hace clic en "Nueva conversación", elige un participante y confirma. Hoy esto falla siempre con un error genérico ("Error interno") sin explicación visible, porque el frontend envía un valor de tipo de conversación que la base de datos rechaza. Solo las conversaciones ya existentes (creadas antes de este bug o insertadas directamente en la base) son usables.

**Why this priority**: Bloquea por completo una función core visible al cliente (mensajería) en producción. Confirmado en vivo el 2026-07-09 durante la demo.

**Independent Test**: Iniciar sesión, ir a Mensajes, crear una conversación directa con otro usuario del mismo tenant, y confirmar que aparece en la lista y permite enviar mensajes — sin necesitar ningún otro cambio del sistema.

**Acceptance Scenarios**:

1. **Given** un usuario autenticado sin conversaciones previas, **When** crea una conversación directa (1 a 1) con otro usuario válido, **Then** la conversación se crea exitosamente y queda visible en la lista de Mensajes de ambos participantes.
2. **Given** un usuario autenticado, **When** intenta crear una conversación grupal con varios participantes, **Then** la conversación se crea exitosamente (mismo tipo de validación debe revisarse para el caso grupal).
3. **Given** una conversación recién creada, **When** el usuario envía un mensaje, **Then** el mensaje se persiste y se entrega en tiempo real al otro participante (comportamiento ya validado para conversaciones existentes).

---

### User Story 2 - Recibir alertas por Telegram tras vincular la cuenta (Priority: P1)

Un usuario configura una regla de alerta con notificación por Telegram activada, y vincula su cuenta de Telegram (vía el link de un clic "Conectar con Telegram" o pegando su Chat ID a mano). Hoy, ninguna de las dos vías habilita realmente el envío — el sistema guarda el chat_id, muestra un mensaje de éxito, y al dispararse una alerta (real o de prueba) no llega nada a Telegram, sin ningún error visible que explique por qué.

**Why this priority**: Es la funcionalidad central del entregable de la demo de Fase 6 (Alertas Inteligentes). Confirmado en vivo el 2026-07-09: se vinculó un chat_id real, se probó la alerta, y no llegó el mensaje — la causa raíz se rastreó hasta un flag de base de datos (`es_account_manager_gobierno`) que el flujo de auto-servicio nunca activa.

**Independent Test**: Como usuario nuevo, vincular Telegram (por cualquiera de las dos vías), crear una alerta con notificación por Telegram, usar "Probar alerta" sobre una licitación real, y confirmar que el mensaje llega al chat de Telegram vinculado — sin necesitar que un administrador intervenga manualmente en la base de datos.

**Acceptance Scenarios**:

1. **Given** un usuario que jamás vinculó Telegram, **When** usa el deep-link "Conectar con Telegram" y presiona "Iniciar" en su Telegram real, **Then** su chat queda vinculado Y habilitado para recibir alertas (no solo guardado).
2. **Given** un usuario que jamás vinculó Telegram, **When** pega su Chat ID manualmente y guarda, **Then** su chat queda vinculado Y habilitado para recibir alertas.
3. **Given** un usuario con Telegram vinculado y una alerta activa con `notificarTelegram=true`, **When** se dispara la alerta (real o vía "Probar"), **Then** el mensaje llega al chat de Telegram del usuario, y la respuesta de la API refleja `notificacionTelegramEnviada: true`.
4. **Given** un usuario que vinculó su Telegram ANTES de este fix (con el bug todavía presente), **When** se aplica la corrección, **Then** ese usuario también queda habilitado para recibir alertas sin tener que re-vincular manualmente (backfill).

---

### Edge Cases

- ¿Qué pasa si dos usuarios distintos vinculan el mismo Chat ID de Telegram por error? (fuera de alcance de este fix — comportamiento actual se mantiene)
- ¿Qué pasa con conversaciones grupales (`tipo='grupal'`)? Debe verificarse si el mismo mismatch de valores afecta ese camino también.
- ¿Qué pasa si el backfill de `es_account_manager_gobierno` se aplica sobre una fila que fue creada por un proceso administrativo distinto (no autoservicio) y que intencionalmente tenía el flag en `FALSE`? El backfill debe limitarse a filas con `telegram_chat_id` no nulo, que son inequívocamente el resultado de una auto-vinculación exitosa.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE aceptar la creación de una conversación directa (1 a 1) desde la interfaz de Mensajes sin error, usando el valor de tipo que la base de datos espera.
- **FR-002**: El sistema DEBE aceptar la creación de una conversación grupal desde la interfaz de Mensajes sin error (verificar si aplica el mismo mismatch).
- **FR-003**: Cuando falle la creación de una conversación por cualquier motivo, el sistema DEBE mostrar al usuario un mensaje de error específico y accionable, no un error genérico.
- **FR-004**: El sistema DEBE marcar a un usuario como receptor válido de alertas por Telegram inmediatamente después de que complete cualquiera de los dos flujos de auto-vinculación (deep-link o Chat ID manual).
- **FR-005**: El sistema DEBE aplicar retroactivamente la habilitación de entrega (FR-004) a todos los usuarios que ya tengan un `telegram_chat_id` guardado antes de este fix.
- **FR-006**: Cuando un envío de alerta a Telegram no se realice porque el usuario no está habilitado para recibir, el sistema DEBE seguir marcando `notificacionTelegramEnviada: false`, pero esto no debe ocurrir para usuarios que completaron la auto-vinculación (ver FR-004).
- **FR-007**: El sistema DEBE seguir permitiendo que la lista de "account managers de gobierno" (`es_account_manager_gobierno`) se gestione también por vías administrativas distintas al autoservicio, sin que este fix elimine esa capacidad.

### Key Entities *(include if feature involves data)*

- **Conversación**: registro de una conversación de mensajería (directa o grupal) entre usuarios; tiene un campo `tipo` restringido a un conjunto fijo de valores válidos en la base de datos.
- **Destinatario de Alertas**: vínculo entre un usuario y su chat de Telegram, con un indicador de si debe recibir alertas de licitaciones del gobierno; hoy ese indicador solo se activa por una vía distinta a la auto-vinculación del propio usuario.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de los intentos de crear una conversación directa desde la UI, con datos válidos, resultan en una conversación creada exitosamente (hoy: 0%).
- **SC-002**: El 100% de los usuarios que completan la auto-vinculación de Telegram (por cualquiera de las dos vías) reciben efectivamente el siguiente mensaje de alerta de prueba o real que les corresponda (hoy: 0%, falla silenciosamente).
- **SC-003**: Los usuarios que vincularon Telegram antes del fix quedan habilitados sin necesitar ninguna acción adicional de su parte.
- **SC-004**: Ningún flujo de auto-servicio (mensajería, alertas) devuelve un error silencioso o genérico que oculte la causa real de un fallo — verificado por prueba manual reproduciendo ambos bugs originales y confirmando que, si volvieran a ocurrir, el error sería visible y diagnosticable.

## Assumptions

- Ambos bugs son exclusivos del código de aplicación (frontend y/o stored procedures) y no requieren cambios de infraestructura.
- El fix de BUG-015 se implementa modificando el stored procedure `usp_AlertasDestinatarios_GuardarChatId` (o el código que lo invoca) más una migración de backfill de una sola vez — no requiere tocar la lógica de matching de alertas en sí.
- El fix de BUG-014 se implementa alineando el valor enviado por el frontend con el valor aceptado por la base de datos (`'directo'`/`'grupal'`) — se asume que el valor correcto es el que ya usa la base de datos, no al revés, para no romper conversaciones existentes.
- No se requiere notificar activamente a los usuarios que fueron afectados por estos bugs durante la ventana en que estuvieron presentes en producción (2026-07-09, día del deploy inicial).
- Ambos bugs se corrigen en el mismo ciclo de trabajo por su severidad (P1) y porque comparten el mismo origen temporal (deploy inicial a producción), pero son técnicamente independientes entre sí.
