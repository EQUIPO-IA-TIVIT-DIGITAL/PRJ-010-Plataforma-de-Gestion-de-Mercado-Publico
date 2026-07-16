# Feature Specification: Buscador Inteligente en Lenguaje Natural sobre Licitaciones

**Feature Branch**: `018-buscador-inteligente-nl`
**Created**: 2026-07-03
**Status**: **CERRADO** (2026-07-16) — implementado y validado en vivo contra Docker + Vertex AI real. Funcional de punta a punta (US1/US2/US3, incluida degradación FR-005 confirmada con un 429 real de Vertex). SC-001 (latencia <3s) no se cumplió en la medición local (mediana ~6s); decisión del usuario: se atribuye a latencia de red/cuota del entorno local de desarrollo contra `us-central1`, no representativa de producción — no bloquea el cierre. Sin acción de seguimiento pendiente salvo observar la latencia real una vez desplegado.
**Semana estimada**: Semana 3 (Julio 2026)
**Impacto**: Alto | **Complejidad**: Media (bajó de Media-Alta en planning — no requirió pgvector, ver research.md) | **Depende de**: Fase 5

## Estado de implementación (2026-07-16)

Implementadas y probadas en vivo (Docker real, ADC/Vertex AI activo) las 3 user stories (US1/US2 P1, US3 P2) vía `ConsultaSemanticaService` (Gemini 2.5 Flash-Lite + Vertex AI/ADC) enriqueciendo `usp_Licitaciones_BuscarNatural` (migración V107), con fallback a búsqueda literal si Vertex falla o no está configurado (FR-005 — confirmado en un caso real: un 429 de cuota de Vertex durante la prueba, la búsqueda igual respondió 200). Frontend: modo "Búsqueda inteligente" en `LicitacionFilterBar` + `NaturalSearchResults`, conectado a `useBuscarNatural`.

**Bug real encontrado y corregido durante la validación**: `usp_Licitaciones_BuscarNatural` fallaba con `42883` porque `LicitacionHandler.BuscarNaturalAsync` pasaba parámetros de fecha sin `DbType` explícito (mismo patrón que BUG-014) — corregido con `DynamicParameters`.

**Hallazgo abierto, no bloqueante para seguir usando la feature pero sí para cerrarla**: la latencia real (mediana ~6s, algunas sobre 10s) está muy por encima de SC-001 (<3s). El diagnóstico en vivo apunta a **cuota de Vertex AI del proyecto de desarrollo** (`tivit-cu010` chocó contra un 429 real durante la prueba), no a que el modelo `gemini-2.5-flash-lite` sea lento en sí (hubo respuestas de 828ms-1.6s). Antes de cambiar de modelo, revisar la cuota asignada. Ver detalle completo en `tasks.md` (sección Hallazgos).

**Fuera de alcance confirmado en esta iteración**: filtro por ubicación/región (no existe columna en `licitaciones` — ver research.md). Si se necesita a futuro, requiere una fase previa de extracción de región desde el organismo o las bases.

**Pendiente antes de dar la feature por cerrada**: confirmar con el equipo de infraestructura si la cuota de Vertex AI de `tivit-cu010` es representativa de producción o es un límite de desarrollo; correr el benchmark formal de recall (SC-002) con las 6 palabras clave del equipo si se quiere un número duro en vez de la validación cualitativa ya hecha.

**Origen**: Reunión "Revisión Alcance Mercado Público" (2026-06-09). Francisco Lopez Balart y Ariel Gonzalez Borges solicitaron priorizar esta herramienta sobre otros análisis: *"un sistema automatizado... que permita asociar conceptos y encontrar licitaciones relevantes en el mercado público, detectando oportunidades que los buscadores tradicionales omiten debido a la ambigüedad en las categorías"*. Reconfirmado en la reunión del 2026-07-01 como parte del flujo de detección de oportunidades, inmediatamente después del módulo de Alertas (Fase 6).

---

## Contexto

El equipo comercial revisa manualmente mercadopublico.cl porque el buscador oficial depende de rubros preseleccionados que no siempre corresponden a los servicios ofrecidos por TIVIT (cloud, ciberseguridad, SOC, data center, telecomunicaciones, cámaras), lo que provoca pérdida de oportunidades mal categorizadas. MPM ya sincroniza diariamente el universo de licitaciones vía API directa (Fase de extracción, `016-extraccion-documentos-api`) y expone un endpoint `buscar-natural`, pero éste solo hace *full-text search* literal (`plainto_tsquery`) — no interpreta conceptos, sinónimos ni filtros implícitos en la frase del usuario, y no está conectado a ninguna pantalla. Esta fase reemplaza esa base literal por una búsqueda semántica real, disponible como barra de búsqueda sobre licitaciones activas, cerradas y adjudicadas.

---

## User Stories

### User Story 1 — Consulta en lenguaje natural (Priority: P1)

Un analista comercial escribe una consulta como *"licitaciones de cloud computing en Santiago mayores a 10 millones"* o *"procesos de ciberseguridad cerrados el último mes"* y el sistema devuelve licitaciones relevantes aunque no contengan esas palabras exactas (p. ej. "SOC", "centro de datos", "nube").

**Why this priority**: Es el reemplazo directo de la revisión manual diaria por palabras clave que hoy hace una persona del equipo — el mayor pedido explícito del cliente en la reunión de junio.

**Independent Test**: Ingresar la consulta "ciberseguridad para el sector salud" en la barra de búsqueda y obtener licitaciones que mencionen "SOC", "seguridad de la información" o "protección de datos" sin usar el término literal "ciberseguridad", ordenadas por relevancia, en menos de 3 segundos.

**Acceptance Scenarios**:
1. **Given** el usuario está en la pantalla de licitaciones, **When** escribe una consulta en lenguaje natural con un monto o ubicación implícita ("mayores a 10 millones", "en Santiago"), **Then** el sistema extrae esos filtros automáticamente y los aplica junto con la búsqueda semántica.
2. **Given** una consulta con un término del dominio TIVIT (p. ej. "SOC"), **When** se ejecuta la búsqueda, **Then** el resultado incluye licitaciones que usan sinónimos o conceptos relacionados (p. ej. "centro de operaciones de seguridad"), no solo coincidencia literal.
3. **Given** una consulta ambigua o sin resultados relevantes, **When** el sistema no encuentra coincidencias de alta confianza, **Then** muestra un estado vacío claro en lugar de resultados irrelevantes.

---

### User Story 2 — Filtrado por estado de licitación (Priority: P1)

El usuario necesita acotar la búsqueda semántica a licitaciones activas, cerradas o adjudicadas según el objetivo (oportunidades nuevas vs. análisis histórico/competitivo).

**Why this priority**: Sin este filtro la búsqueda mezcla oportunidades vigentes con histórico, lo que impide usarla como herramienta operativa diaria.

**Independent Test**: Buscar "telecomunicaciones" filtrando solo por "adjudicadas" y confirmar que ningún resultado activo aparece en la lista.

**Acceptance Scenarios**:
1. **Given** una consulta en lenguaje natural, **When** el usuario selecciona el filtro de estado (activa/cerrada/adjudicada/todas), **Then** los resultados respetan ese filtro sin perder el ranking semántico.
2. **Given** el usuario no especifica estado, **When** ejecuta la búsqueda, **Then** el sistema busca sobre todas las licitaciones por defecto.

---

### User Story 3 — Resumen antes de descargar documentos (Priority: P2)

El usuario quiere ver un resumen de cada resultado (objeto, monto, organismo, fecha de cierre) sin tener que abrir ni descargar las bases completas, para decidir rápido cuáles vale la pena explorar en profundidad.

**Why this priority**: Evita el procesamiento y descarga innecesaria de documentos pesados cuando el usuario solo está explorando — pedido explícito en la reunión de junio para ahorrar recursos.

**Independent Test**: Ejecutar una búsqueda y verificar que los resultados muestran resumen en pantalla sin disparar ninguna descarga de PDF; solo al hacer clic en "ver detalle" se accede a la información completa/documentos.

**Acceptance Scenarios**:
1. **Given** una lista de resultados, **When** se renderiza cada tarjeta, **Then** muestra objeto, organismo, monto estimado y fecha de cierre sin descargar archivos adjuntos.
2. **Given** un resultado de interés, **When** el usuario hace clic en él, **Then** navega a la ficha completa de la licitación (ya existente en el módulo de Licitaciones).

---

### Edge Cases

- ¿Qué pasa si la consulta no tiene ninguna palabra reconocible del dominio de licitaciones (p. ej. texto aleatorio)? El sistema debe responder con resultados vacíos o de baja confianza, no con errores.
- ¿Cómo se comporta el sistema si el usuario combina múltiples conceptos contradictorios (p. ej. "activas y adjudicadas")? Debe priorizar el filtro más restrictivo o pedir aclaración en la UI.
- ¿Qué ocurre si el motor de interpretación de la consulta (IA) no está disponible momentáneamente? El sistema debe degradar a la búsqueda full-text existente (`buscar-natural`) en vez de fallar por completo.
- ¿Cómo se manejan consultas muy largas o con múltiples oraciones? Se debe extraer la intención principal sin exceder límites razonables de tiempo de respuesta.

---

## Requirements

### Functional Requirements

- **FR-001**: El sistema MUST exponer una barra de búsqueda en lenguaje natural, accesible desde la pantalla principal de licitaciones, conectada a un endpoint real (reemplaza el hook `useBuscarNatural` hoy desconectado del frontend).
- **FR-002**: El sistema MUST interpretar la consulta del usuario para identificar conceptos del dominio (rubro/tecnología), sinónimos y filtros implícitos (monto, ubicación, plazo), no solo coincidencia literal de texto.
- **FR-003**: El sistema MUST permitir acotar la búsqueda por estado de la licitación: activa, cerrada, adjudicada o todas.
- **FR-004**: El sistema MUST devolver resultados ordenados por relevancia semántica, mostrando un resumen (objeto, organismo, monto, fecha de cierre) sin requerir descarga de documentos adjuntos.
- **FR-005**: El sistema MUST degradar de forma controlada a búsqueda full-text literal si el componente de interpretación semántica falla o no está disponible, en vez de devolver un error al usuario.
- **FR-006**: El sistema MUST responder en un tiempo aceptable para uso interactivo (ver SC-001), incluso sobre el volumen histórico completo de licitaciones sincronizadas.
- **FR-007**: El sistema MUST permitir refinar o repetir la búsqueda sin perder el contexto de la consulta anterior (historial de la sesión de búsqueda).

### Key Entities

- **Consulta de búsqueda**: texto en lenguaje natural ingresado por el usuario, junto con los filtros estructurados que el sistema extrae de él (estado, monto, ubicación, rubro).
- **Resultado de búsqueda**: licitación con score de relevancia semántica, resumen mostrado en pantalla y referencia a la ficha completa existente en el módulo de Licitaciones.

## Success Criteria

### Measurable Outcomes

- **SC-001**: El 95% de las búsquedas devuelve resultados en menos de 3 segundos.
- **SC-002**: Ante consultas que usan sinónimos o conceptos relacionados (no la palabra literal), el sistema encuentra las licitaciones relevantes equivalentes en al menos el 80% de los casos evaluados contra el conjunto de palabras clave actual del equipo (cloud, ciberseguridad, SOC, data center, telecomunicaciones, cámaras).
- **SC-003**: El equipo comercial deja de requerir revisión manual diaria de nuevas licitaciones por palabras clave para su primer filtro de exploración.
- **SC-004**: Cero descargas de documentos adjuntos disparadas solo por explorar resultados de búsqueda (la descarga ocurre únicamente al abrir el detalle).

## Assumptions

- La base de licitaciones sincronizada (Fase 5 / extracción vía API) contiene información suficiente en texto (nombre, descripción, rubro) para soportar interpretación semántica sin depender de las bases PDF completas.
- La interpretación de la consulta puede apoyarse en el mismo proveedor de IA ya usado en el módulo de Análisis (Gemini), evaluando además alternativas de búsqueda vectorial/embeddings si el volumen de datos lo justifica — decisión técnica a definir en `/speckit-plan`.
- Esta fase no reemplaza el motor de Alertas (Fase 6): la búsqueda es una herramienta de exploración bajo demanda, mientras que Alertas es notificación proactiva basada en reglas guardadas. Ambas pueden compartir el mismo componente de interpretación de conceptos/sinónimos.
- Fuera de alcance en esta fase: búsqueda por voz, soporte multi-idioma, y ranking personalizado por usuario (aprendizaje de preferencias).
