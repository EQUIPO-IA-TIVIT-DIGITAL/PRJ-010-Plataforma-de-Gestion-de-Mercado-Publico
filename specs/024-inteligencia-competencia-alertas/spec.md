# Feature Specification: Inteligencia de competencia, alertas interactivas y canal de correo

**Feature Branch**: `024-inteligencia-competencia-alertas`

**Created**: 2026-07-09

**Status**: Draft

**Input**: User description: "Panel de inteligencia de competencia (buscar competidor, ver en qué ofertó, análisis IA bajo demanda por periodo, cacheado). Alerta interactiva por Telegram con botón 'Me interesa' que devuelve un resumen rápido sin Gemini. Canal de alertas por correo electrónico."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Panel de inteligencia de competencia (Priority: P1)

Un usuario de negocio busca un competidor por nombre (ej. "Sonda") y ve el listado completo de licitaciones donde ese competidor presentó una oferta — con el monto ofertado y si esa oferta fue aceptada o rechazada — sin necesitar que exista un análisis de IA previo sobre esa licitación. Sobre ese listado, el usuario elige un rango de fechas y pide explícitamente un análisis con IA que sintetice patrones: qué tipo de licitaciones persigue ese competidor, en qué organismos, montos típicos, y qué se puede inferir para competirle mejor. El análisis, una vez generado, queda guardado — si el usuario (u otro usuario) vuelve a consultar el mismo competidor con el mismo rango de fechas, ve el resultado ya guardado en vez de esperar un análisis nuevo.

**Why this priority**: Es el pedido de mayor valor estratégico salido de la reunión post-demo — permite anticipar movimientos de la competencia con datos reales (no solo de las licitaciones donde TIVIT ya participó), algo que hoy es completamente invisible en el sistema.

**Independent Test**: Con datos de al menos un competidor ya recolectados, buscarlo por nombre, ver su listado de ofertas, elegir un rango de fechas, pedir el análisis IA, confirmar que se genera y se muestra. Repetir la misma búsqueda (mismo competidor, mismo rango) y confirmar que el resultado aparece de inmediato sin volver a generar el análisis.

**Acceptance Scenarios**:

1. **Given** que existen licitaciones adjudicadas con datos de oferentes recolectados, **When** el usuario busca un competidor por nombre, **Then** ve el listado de licitaciones donde ese competidor ofertó, con monto y resultado (aceptada/rechazada) de cada oferta.
2. **Given** el listado de ofertas de un competidor, **When** el usuario elige un rango de fechas y confirma "Analizar con IA", **Then** el sistema genera un análisis de patrones (tipo de licitaciones, organismos frecuentes, montos típicos, recomendaciones) y lo muestra.
3. **Given** un análisis ya generado para un competidor y un rango de fechas específico, **When** cualquier usuario vuelve a pedir el análisis para exactamente ese mismo competidor y rango, **Then** el sistema muestra el resultado guardado sin generar un análisis nuevo.
4. **Given** el listado de ofertas de un competidor, **When** el usuario NO pide explícitamente el análisis IA, **Then** el sistema nunca dispara un análisis de IA por su cuenta — el listado de ofertas es visible y utilizable sin ningún costo de IA asociado.
5. **Given** un competidor sin ninguna oferta recolectada todavía, **When** el usuario lo busca, **Then** el sistema indica claramente que no hay datos, sin error confuso.

---

### User Story 2 - Alerta interactiva por Telegram con resumen bajo demanda (Priority: P2)

Un usuario recibe una alerta de Telegram por coincidencia de palabra clave. Junto al mensaje, ve un botón "Me interesa". Al presionarlo, recibe — en el mismo chat, casi de inmediato — un resumen de la licitación con la información relevante para decidir si perseguirla (descripción, organismo, monto, fechas clave, requisitos principales), sin que eso dispare ningún análisis de inteligencia artificial.

**Why this priority**: Pedido explícito de negocio (Manuel Aliaga) tras ver la demo — hoy la alerta solo confirma el match de palabra clave, sin dar información suficiente para decidir si vale la pena revisar la licitación.

**Independent Test**: Recibir una alerta de prueba con el botón, presionarlo, confirmar que llega un resumen legible con los datos clave de esa licitación específica, y que ningún otro proceso de IA se disparó por esa acción.

**Acceptance Scenarios**:

1. **Given** una alerta que llega por Telegram, **When** el usuario la recibe, **Then** el mensaje incluye un botón "Me interesa" además del texto de la alerta.
2. **Given** el botón "Me interesa" de una alerta, **When** el usuario lo presiona, **Then** recibe en el mismo chat un resumen de esa licitación con descripción, organismo, monto, fechas y requisitos principales.
3. **Given** que el usuario presiona "Me interesa", **When** el sistema arma el resumen, **Then** lo hace usando únicamente datos ya disponibles de Mercado Público (sin generar ni consumir un análisis de inteligencia artificial).
4. **Given** que el usuario presiona el botón dos veces sobre la misma alerta, **When** el segundo click ocurre, **Then** el sistema responde de forma consistente (reenvía el mismo resumen o indica que ya se envió), sin duplicar trabajo innecesario ni fallar.

---

### User Story 3 - Canal de alertas por correo electrónico (Priority: P3)

Un usuario configura una dirección de correo para recibir sus alertas, de la misma forma en que hoy configura su Chat ID de Telegram. Cuando se dispara una alerta que le corresponde, la recibe también (o alternativamente) por correo, con la misma información que recibiría por Telegram.

**Why this priority**: Menor complejidad (reusa infraestructura de correo ya existente en el proyecto) y menor urgencia relativa que los otros dos pedidos — pero es la alternativa natural para usuarios que prefieren no usar Telegram.

**Independent Test**: Configurar un correo de destino para alertas, disparar una alerta de prueba, confirmar que el correo llega con la información correspondiente.

**Acceptance Scenarios**:

1. **Given** un usuario sin correo de alertas configurado, **When** lo configura, **Then** el sistema lo guarda y lo confirma, igual que hoy pasa con el Chat ID de Telegram.
2. **Given** un usuario con correo de alertas configurado y una alerta que le corresponde, **When** la alerta se dispara (real o de prueba), **Then** recibe un correo con el contenido de esa alerta.
3. **Given** un usuario con Telegram Y correo configurados, **When** se dispara una alerta, **Then** la recibe por ambos canales sin que uno bloquee al otro (si un canal falla, el otro igual se intenta).

---

### Edge Cases

- ¿Qué pasa si dos usuarios piden el análisis IA del mismo competidor y rango de fechas al mismo tiempo, antes de que el primero termine? El sistema no debe disparar dos análisis duplicados para el mismo competidor+rango.
- ¿Qué pasa si el rango de fechas elegido para el análisis de competidor es enorme (varios años) y el volumen de licitaciones es muy grande? El sistema debe dejar claro el volumen antes de confirmar el gasto de IA (ver FR-006).
- ¿Qué pasa si el usuario presiona "Me interesa" sobre una licitación que ya cerró o fue removida de Mercado Público? El resumen debe indicarlo en vez de fallar silenciosamente.
- ¿Qué pasa si un mismo proveedor aparece con nombres ligeramente distintos entre licitaciones (ej. "Sonda S.A." vs "SONDA S.A."), afectando la búsqueda por competidor? Ver Assumptions.
- ¿Qué pasa si el correo configurado por el usuario es inválido o rebota? El sistema debe registrar el fallo sin bloquear la entrega por los demás canales.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE recolectar, para las licitaciones adjudicadas, el listado completo de oferentes (proveedor, monto ofertado, estado de la oferta) — no solo el adjudicatario final.
- **FR-002**: El sistema DEBE permitir buscar un competidor por nombre y ver el listado de licitaciones donde presentó oferta, con monto y resultado de cada una.
- **FR-003**: El sistema DEBE permitir al usuario elegir un rango de fechas sobre el listado de un competidor antes de solicitar un análisis con inteligencia artificial.
- **FR-004**: El sistema NUNCA DEBE disparar un análisis de inteligencia artificial sobre un competidor de forma automática — solo cuando el usuario lo solicita explícitamente para un competidor y rango de fechas específicos.
- **FR-005**: El sistema DEBE guardar el resultado de cada análisis de competidor asociado a su competidor y rango de fechas exactos, y DEBE reutilizar ese resultado guardado ante una consulta idéntica futura, en vez de generar un análisis nuevo.
- **FR-006**: El sistema DEBE mostrar al usuario cuántas licitaciones entrarían en el análisis antes de confirmar el gasto de inteligencia artificial, para rangos de fecha amplios.
- **FR-007**: El sistema DEBE agregar un control interactivo ("Me interesa") a los mensajes de alerta enviados por Telegram.
- **FR-008**: Al activarse ese control, el sistema DEBE responder con un resumen de la licitación (descripción, organismo, monto, fechas clave, requisitos principales) construido a partir de datos ya sincronizados de Mercado Público, sin invocar inteligencia artificial.
- **FR-009**: El sistema DEBE permitir a un usuario configurar una dirección de correo electrónico como destino de sus alertas.
- **FR-010**: El sistema DEBE poder entregar una alerta disparada por correo electrónico, con el mismo contenido informativo que la versión de Telegram.
- **FR-011**: Cuando un usuario tiene más de un canal de entrega configurado (Telegram y correo), el sistema DEBE intentar la entrega por todos los canales configurados de forma independiente — el fallo de un canal no debe impedir el intento en el otro.

### Key Entities *(include if feature involves data)*

- **Oferta**: una postulación de un proveedor a una licitación específica — proveedor (RUT, nombre), monto ofertado, resultado (aceptada/rechazada). Muchas ofertas por licitación, un adjudicatario entre ellas.
- **Análisis de Competidor**: resultado guardado de un análisis de inteligencia artificial sobre las ofertas de un competidor específico dentro de un rango de fechas específico — sirve de caché para no repetir el análisis ante la misma consulta.
- **Preferencia de Canal de Alertas**: la configuración de un usuario sobre por dónde quiere recibir sus alertas (Chat ID de Telegram, correo electrónico, o ambos).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un usuario puede ver el historial de ofertas de un competidor sin generar ningún costo de inteligencia artificial — el listado se basa 100% en datos ya recolectados.
- **SC-002**: Pedir el análisis de un competidor+rango de fechas ya consultado antes devuelve el resultado guardado en menos de 2 segundos, sin volver a invocar inteligencia artificial.
- **SC-003**: El 100% de los mensajes de alerta por Telegram incluyen el botón "Me interesa" desde el día del lanzamiento de esta feature.
- **SC-004**: El resumen bajo demanda ("Me interesa") llega al usuario en menos de 10 segundos desde que presiona el botón.
- **SC-005**: Un usuario puede recibir la misma alerta por Telegram y por correo simultáneamente, sin que la falla de un canal afecte al otro.

## Assumptions

- El "Cuadro de Ofertas" de Mercado Público (confirmado público, sin login, para licitaciones adjudicadas) es la fuente de datos para el listado de oferentes — se asume que esta sección está disponible de forma consistente para licitaciones adjudicadas, aunque el nivel de detalle pueda variar entre licitaciones.
- La búsqueda de competidor por nombre usa coincidencia flexible (no exacta) dado que el mismo proveedor puede aparecer con variaciones de formato entre licitaciones (mayúsculas, razón social completa vs. abreviada) — normalizar esto queda a criterio de implementación, no bloquea el spec.
- El análisis de IA de un competidor reutiliza el mismo proveedor de modelo (Gemini vía Vertex AI) ya usado por el resto del sistema — no se evalúan proveedores alternativos en esta feature.
- El resumen "Me interesa" no requiere que la licitación tenga un análisis de evaluación (PDF) ya cargado — se arma solo con los datos que ya trae la sincronización/consulta directa a Mercado Público.
- El canal de correo es adicional a Telegram, no lo reemplaza — un usuario puede tener uno, otro, o ambos configurados.
- No hay requisito de "un correo genérico compartido para todo el equipo" en el alcance de este spec — cada usuario configura su propio correo de alertas, igual que hoy hace con su Chat ID de Telegram (si se necesita un correo compartido de equipo, es una decisión de negocio a resolver fuera de este spec, ej. dado de alta como un usuario más del sistema).
