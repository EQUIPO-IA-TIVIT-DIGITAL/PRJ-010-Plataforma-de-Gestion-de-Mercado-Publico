# Feature Specification: Mejora de Alertas por Correo

**Feature Branch**: `032-mejora-alertas-correo`

**Created**: 2026-08-07

**Status**: Draft

**Input**: User description: "Mejoras al sistema de alertas de licitaciones por correo, basado en feedback directo del usuario dueño de MPM tras usar el sistema en producción: (1) horario de envío molesto (3am), preferencia por 8am + 3pm; (2) falsos positivos en el matching de keywords cortas (ej. 'TI' matchea 'parTIcipantes'); (3) contenido del correo demasiado básico (solo nombre + código + presupuesto opcional)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Alertas sin falsos positivos (Priority: P1)

Un usuario de negocio configura una alerta con una palabra clave corta o común (por ejemplo "TI", "obras", "red") para no perderse licitaciones relevantes de su área. Hoy, esa alerta le trae licitaciones que no tienen nada que ver — el sistema encontró la palabra clave como una simple secuencia de letras dentro de otra palabra ("par**TI**cipantes"), no como la palabra o sigla real que el usuario quiso decir. Esto erosiona la confianza en el sistema de alertas al punto de que el usuario deja de prestarles atención.

**Why this priority**: Es el problema más grave de los tres — una alerta que trae ruido constante es peor que no tener alerta, porque el usuario aprende a ignorarla y se pierde también las coincidencias reales. Corregirlo es la base para que el resto de las mejoras (horario, contenido) tengan sentido.

**Independent Test**: Se puede probar de forma aislada creando una alerta con la keyword "TI" contra un set de licitaciones que incluya tanto casos reales con "TI" como palabra/sigla independiente, como casos con "TI" incrustado dentro de otra palabra (ej. "participantes", "certificación") — y verificando que solo las primeras generan una alerta.

**Acceptance Scenarios**:

1. **Given** una alerta configurada con la keyword "TI", **When** se publica una licitación llamada "Servicio de soporte TI para oficinas regionales", **Then** el usuario recibe la alerta (coincidencia real de la sigla como palabra independiente).
2. **Given** una alerta configurada con la keyword "TI", **When** se publica una licitación llamada "Producción evento mujeres participantes", **Then** el usuario NO recibe la alerta (la coincidencia es un fragmento de "participantes", no la sigla).
3. **Given** una alerta configurada con una keyword compuesta de varias palabras (ej. "mesa de ayuda"), **When** se publica una licitación cuyo nombre contiene esa frase completa, **Then** el usuario recibe la alerta (el fix de límites de palabra no debe romper el matching de frases existentes).

---

### User Story 2 - Correo de alerta más informativo (Priority: P2)

Cuando le llega una alerta, el usuario hoy solo ve el nombre de la licitación, su código, y a veces un presupuesto — tiene que entrar al sistema (o a Mercado Público directamente) para saber de qué organismo es, cuándo cierra, y si vale la pena revisarla con más calma. El usuario quiere poder decidir de un vistazo, desde el correo mismo, si esa licitación merece atención inmediata.

**Why this priority**: Mejora directa de la utilidad de cada alerta que ya llega correctamente (depende de que US1 esté resuelto para que valga la pena invertir en un correo más rico) — segunda en impacto porque no arregla ruido, pero sí acelera la decisión del usuario en cada caso real.

**Independent Test**: Se puede probar disparando una alerta de prueba y verificando que el correo recibido incluye, además de lo que ya trae hoy, el organismo comprador, la fecha de cierre, y un enlace directo a la ficha de la licitación en Mercado Público — sin necesidad de que el usuario abra MPM.

**Acceptance Scenarios**:

1. **Given** una licitación que coincide con una alerta y tiene organismo, fecha de cierre, y presupuesto estimado registrados, **When** se envía el correo de alerta, **Then** el correo muestra el organismo, la fecha de cierre y el presupuesto, además del nombre y código ya existentes.
2. **Given** una licitación que coincide con una alerta y tiene un enlace directo disponible hacia su ficha en Mercado Público, **When** se envía el correo de alerta, **Then** el correo incluye ese enlace como acceso directo.
3. **Given** una licitación que coincide con una alerta pero le falta algún dato opcional (ej. sin fecha de cierre informada todavía), **When** se envía el correo de alerta, **Then** el correo se envía igual, omitiendo prolijamente el campo faltante en vez de mostrar un valor vacío o roto.

---

### User Story 3 - Horario de envío alineado a la jornada laboral (Priority: P3)

El usuario recibe hoy su primera alerta del día a las 3am, lo cual lo interrumpe fuera de horario sin ningún beneficio (no va a revisarla hasta la mañana de todas formas). Prefiere que el primer envío del día ocurra a las 8am, al iniciar su jornada laboral, y confirma que el segundo horario del día (3pm) ya funciona bien como una "recarga" de novedades a media tarde.

**Why this priority**: Es la mejora de menor impacto funcional de las tres — no cambia qué información llega ni su calidad, solo cuándo. Se prioriza último porque es un cambio de configuración de infraestructura, no de comportamiento del sistema, y no bloquea ni depende de las otras dos mejoras.

**Independent Test**: Se puede verificar consultando la configuración del disparador programado y confirmando que sus horarios son las 8am y las 3pm (hora de Santiago) en vez de las 3am y 3pm actuales — sin necesidad de esperar a que ocurra un envío real para validarlo.

**Acceptance Scenarios**:

1. **Given** el sistema configurado con el nuevo horario, **When** llega el primer disparo programado del día, **Then** ocurre a las 8:00am hora de Santiago, no a las 3:00am.
2. **Given** el sistema configurado con el nuevo horario, **When** llega el segundo disparo programado del día, **Then** sigue ocurriendo a las 3:00pm hora de Santiago, sin cambios.

---

### Edge Cases

- ¿Qué pasa si una keyword de alerta es ella misma una palabra que aparece como sub-cadena de muchas otras palabras comunes en español (ej. "sol", "mar")? El fix de límites de palabra debe seguir respetando que "sol" no matchee "solicitud", igual que "TI" no debe matchear "participantes".
- ¿Qué pasa si la keyword tiene tildes o mayúsculas distintas a como aparece en el nombre de la licitación (ej. keyword "informática" vs. licitación "Informatica")? El comportamiento de coincidencia insensible a mayúsculas ya existente debe mantenerse; normalización de tildes queda fuera de alcance de esta mejora salvo que ya estuviera cubierta antes.
- ¿Qué pasa si el cambio de horario del disparador coincide con una ejecución ya en curso del ciclo de sync anterior? Debe comportarse igual que hoy ante solapamientos (fuera de alcance de esta mejora, no se está tocando esa lógica).
- ¿Qué pasa si una licitación coincide con más de una alerta del mismo usuario a la vez? El agrupamiento de notificaciones ya existente no debe romperse por los cambios de contenido del correo.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE evaluar la coincidencia de una keyword de alerta contra el texto de una licitación respetando límites de palabra — una keyword corta no debe coincidir cuando aparece como fragmento interno de otra palabra distinta.
- **FR-002**: El sistema DEBE seguir soportando keywords compuestas por varias palabras (frases), coincidiendo cuando la frase completa aparece en el texto de la licitación, tal como funciona hoy.
- **FR-003**: El sistema DEBE mantener el comportamiento insensible a mayúsculas/minúsculas ya existente en el matching de keywords.
- **FR-004**: El correo de alerta DEBE incluir, cuando estén disponibles, el organismo comprador y la fecha de cierre de la licitación, además de los campos que ya incluye hoy (keyword, nombre, código externo, presupuesto).
- **FR-005**: El correo de alerta DEBE incluir un enlace directo a la ficha de la licitación cuando ese enlace esté disponible en el sistema.
- **FR-006**: El correo de alerta DEBE omitir de forma prolija cualquier campo enriquecido que no tenga dato disponible para esa licitación en particular, sin mostrar valores vacíos, nulos o textos de error.
- **FR-007**: El primer envío diario de alertas DEBE ocurrir a las 8:00am hora de Santiago de Chile, en vez de las 3:00am actuales.
- **FR-008**: El segundo envío diario de alertas DEBE mantenerse a las 3:00pm hora de Santiago de Chile, sin cambios respecto al comportamiento actual.
- **FR-009**: El cambio de horario NO DEBE requerir ni implicar cambios en la lógica de negocio de evaluación o envío de alertas — es exclusivamente un cambio de cuándo se dispara el ciclo existente.

### Key Entities *(include if feature involves data)*

- **Alerta (regla)**: configuración de un usuario que define una keyword y criterios opcionales (monto, tipo, organismo) contra los que se evalúan las licitaciones nuevas o modificadas.
- **Licitación**: entidad ya existente en el sistema; para esta mejora interesan en particular los atributos organismo, fecha de cierre, presupuesto estimado y enlace a su ficha pública, todos ya presentes en el modelo actual de licitación.
- **Notificación de alerta (correo)**: el mensaje enviado al usuario cuando una licitación coincide con una de sus alertas; hoy contiene keyword + nombre + código + presupuesto opcional, y pasa a incluir organismo, fecha de cierre y enlace directo.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Cero alertas con la keyword "TI" u otras keywords cortas similares se disparan por coincidencias parciales dentro de otras palabras, verificado contra el histórico real de licitaciones que hoy genera falsos positivos.
- **SC-002**: El 100% de las alertas que hoy coinciden correctamente (frases completas, keywords largas) siguen coincidiendo igual después del cambio — cero regresiones en coincidencias válidas existentes.
- **SC-003**: Un usuario puede decidir si una licitación alertada merece atención inmediata leyendo únicamente el correo recibido, sin necesidad de abrir el sistema, en los casos donde la licitación tiene los datos enriquecidos disponibles.
- **SC-004**: El primer correo del día llega a partir de las 8:00am hora de Santiago, nunca antes, verificado en la configuración del disparador.

## Assumptions

- El canal de notificación relevante para esta mejora es exclusivamente correo electrónico — el canal de Telegram está deprecado y no se reintroduce ni se toca como parte de este trabajo.
- Los datos de organismo, fecha de cierre, presupuesto estimado y enlace de la licitación ya están disponibles en el sistema al momento de evaluar una alerta (no requieren una consulta nueva a una fuente externa) — de no ser así durante la implementación, se documentará como hallazgo y se ajustará el alcance de US2 en consecuencia.
- El cambio de horario aplica al disparador que hoy también dispara la sincronización general de licitaciones (no existe hoy un disparador exclusivo para alertas) — mover ese horario afecta ambos procesos por igual, lo cual es aceptable porque las alertas dependen de que el sync ya haya corrido.
- La normalización de tildes/acentos en el matching de keywords no forma parte de esta mejora, salvo que ya estuviera resuelta previamente.
