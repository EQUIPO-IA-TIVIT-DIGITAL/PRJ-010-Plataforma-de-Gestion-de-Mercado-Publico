# Feature Specification: Definición de Fases — MPM CU010 (Mercado Público)

**Feature Branch**: `001-analisis-fases-sdd`

**Created**: 2026-06-23

**Status**: Draft

**Input**: Análisis de transcripción de reunión de alineamiento del 22/06/2026 (`info/transcript.md`), documentación de infraestructura (`docs/infraestructura-cu010.md`), especificaciones API existentes (`docs/api-first/`) y estado actual del código fuente.

---

## Contexto de Proyecto (Resumen del Análisis)

El proyecto MPM CU010 nació de una conversación directa entre Manuel Aliaga (tech lead original) y Matias Mendez Cabrejos (nuevo responsable). Los objetivos centrales declarados por Manuel son:

1. **Eliminar la descarga manual de PDFs**: El agente debe navegar Mercado Público, identificar licitaciones adjudicadas en las que TIVIT participó y extraer automáticamente el "acta de evaluación" sin intervención humana.
2. **Análisis de pérdidas**: Entender por qué TIVIT perdió licitaciones — comparando puntajes, montos y factores frente a competidores como Sonda.
3. **Responder preguntas ejecutivas de alto impacto**: Francisco (Gerente Chile) necesita datos como "¿por qué Sonda vendió $80M más que nosotros en 2025?".
4. **Seguimiento de licitaciones activas**: Saber en qué licitaciones estamos participando y recibir alertas cuando se solicite aclaración.
5. **Demostraciones semanales de excelencia**: Presentaciones los jueves 8 AM con directivos (Leonardo, Pablo, Fernando, Francesco).

**Estado detectado del código base:**
- Auth (login, recuperación de contraseña) → **COMPLETADO**
- Sincronización de licitaciones (API Mercado Público, sync diario) → **COMPLETADO**
- Catálogo (estados, tipos) → **COMPLETADO**
- Mensajería en tiempo real (SignalR) → **COMPLETADO**
- Módulo Análisis (workspace + upload PDF + Gemini AI + dashboard + chat) → **COMPLETADO**
- Scraper de adjuntos (background service) → **PARCIAL** (infraestructura lista, automatización de identificación y trigger falta)
- Notificaciones → **MÓDULO CREADO** (estado funcional pendiente de verificación)
- Dashboard ejecutivo comparativo (ganadas vs. perdidas, análisis de competidores) → **PENDIENTE**
- CI/CD y despliegue productivo en GCP → **PENDIENTE**

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Análisis Automático de Licitación Perdida (Priority: P1)

Un analista de TIVIT necesita entender por qué perdimos una licitación específica sin tener que descargar ningún archivo manualmente.

**Why this priority**: Es el requerimiento principal declarado por Manuel Aliaga. Sin esto, el CU010 no está cerrado y el equipo corre riesgo.

**Independent Test**: Dado un código de licitación adjudicada en la que TIVIT participó, el sistema debe entregar un análisis completo sin que el usuario descargue nada.

**Acceptance Scenarios**:

1. **Given** el sistema tiene licitaciones sincronizadas con estado "adjudicada", **When** el usuario selecciona una licitación, **Then** el sistema muestra un workspace de análisis con el acta de evaluación ya descargada y analizada por IA.
2. **Given** el workspace tiene un análisis completado, **When** el usuario accede al dashboard, **Then** ve: motivo principal de pérdida, puntajes comparativos de todos los oferentes y la conclusión ejecutiva.
3. **Given** el análisis está disponible, **When** el usuario escribe "¿por qué perdimos?", **Then** el chat contextual responde con datos específicos del acta.

---

### User Story 2 — Dashboard Comparativo Ejecutivo (Priority: P1)

Francisco (Gerente Chile) necesita visualizar en un solo lugar cuánto vendió TIVIT vs. competidores y dónde está la brecha económica.

**Why this priority**: Es la pregunta recurrente del gerente. Responderla de forma automatizada es el mayor diferenciador de valor del sistema.

**Independent Test**: El dashboard ejecutivo puede responder "¿por qué Sonda vendió $80M más que nosotros en 2025?" con datos concretos.

**Acceptance Scenarios**:

1. **Given** el sistema tiene análisis de múltiples licitaciones del 2025, **When** un ejecutivo abre el dashboard comparativo, **Then** ve el ranking de competidores, brechas económicas y patrones de factores de pérdida más frecuentes.
2. **Given** hay licitaciones ganadas y perdidas analizadas, **When** se filtran por año y competidor, **Then** el sistema muestra la diferencia de monto adjudicado y los factores diferenciadores.
3. **Given** el dashboard está cargado, **When** el ejecutivo hace clic en un competidor (ej. Sonda), **Then** ve todas las licitaciones en que compitió con TIVIT y los resultados.

---

### User Story 3 — Seguimiento de Licitaciones Activas con Alertas (Priority: P2)

Un usuario de TIVIT necesita saber en tiempo real cuándo una licitación activa (en la que estamos participando) recibe una solicitud de aclaración del organismo comprador.

**Why this priority**: Las aclaraciones tienen plazos cortos. Perder una notificación puede descalificar la oferta.

**Independent Test**: Cuando Mercado Público registra una solicitud de aclaración, el usuario recibe una notificación en la aplicación antes de que pase 1 hora.

**Acceptance Scenarios**:

1. **Given** el sistema monitorea licitaciones activas, **When** se detecta una solicitud de aclaración nueva, **Then** el usuario recibe una notificación in-app con el detalle y un link directo a la licitación.
2. **Given** el usuario tiene notificaciones pendientes, **When** abre la sección de notificaciones, **Then** ve el historial ordenado cronológicamente con estado leído/no leído.

---

### User Story 4 — Demo Ejecutiva Funcional los Jueves (Priority: P1)

Matias necesita que el sistema esté estable y demostrable en cada sesión semanal con alta gerencia, sin fallos en producción.

**Why this priority**: La continuidad del equipo depende de la calidad y consistencia de estas demostraciones.

**Independent Test**: En el ambiente de demo, el flujo completo (login → licitaciones → análisis → dashboard → pregunta al chat) funciona sin errores en menos de 5 minutos.

**Acceptance Scenarios**:

1. **Given** el ambiente de demo está levantado, **When** se ejecuta el flujo completo, **Then** todos los módulos responden sin errores y los datos son coherentes.
2. **Given** hay nuevas licitaciones sincronizadas desde la última demo, **When** el ejecutivo pregunta "¿qué hay nuevo esta semana?", **Then** el sistema muestra los análisis generados en los últimos 7 días.

---

### Edge Cases

- ¿Qué pasa si el "acta de evaluación" no existe en los adjuntos de una licitación adjudicada? El sistema debe indicarlo claramente sin generar análisis vacío.
- ¿Qué pasa si la sesión web del scraper (`MP_RUT`/`MP_PASSWORD`) expira durante el ciclo de scraping de adjuntos? Debe loguearse el error y reintentarse automáticamente en el próximo ciclo sin afectar el resto del sistema. Nota: `MP_TICKET` es exclusivo de la API REST de sincronización de licitaciones (FR-001); el scraping de adjuntos (FR-002) usa credenciales web `MP_RUT`/`MP_PASSWORD`.
- ¿Qué pasa si Gemini retorna un análisis estructuralmente inválido? El dashboard no debe romperse; debe mostrar el error y permitir reintentar.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE sincronizar automáticamente las licitaciones en las que TIVIT ha participado, filtrando por estado "adjudicada" y desde fecha de publicación 2025-01-01 en adelante.
- **FR-002**: El sistema DEBE identificar y descargar automáticamente el PDF denominado "acta de evaluación" de entre los adjuntos de cada licitación adjudicada, sin intervención del usuario.
- **FR-003**: El sistema DEBE disparar automáticamente el análisis de IA sobre el acta de evaluación descargada y almacenar el resultado estructurado.
- **FR-004**: El sistema DEBE presentar un dashboard por workspace con: motivo principal de pérdida, puntajes de todos los oferentes, comparativa con el adjudicatario, fortalezas y debilidades identificadas, y conclusión ejecutiva.
- **FR-005**: El sistema DEBE ofrecer un chat contextual por workspace que responda preguntas en lenguaje natural sobre el contenido del análisis.
- **FR-006** `[Diferido: Fase 3]`: El sistema DEBE mantener un dashboard agregado de análisis histórico que muestre: total de licitaciones ganadas vs. perdidas, ranking de competidores por frecuencia y monto adjudicado, y diferencial económico por período.
- **FR-007** `[Diferido: Fase 4]`: El sistema DEBE notificar en tiempo real a los usuarios cuando una licitación activa registre una nueva solicitud de aclaración.
- **FR-008**: El sistema DEBE ser accesible mediante credenciales con roles diferenciados (Admin, Analista) con autenticación JWT.

### Key Entities

- **Licitación**: Proceso de compra pública; tiene código externo, estado, organismo, fechas, items y adjuntos.
- **Adjunto / Acta de Evaluación**: Documento PDF descargado desde Mercado Público; el de nombre "acta de evaluación" es el documento clave para análisis.
- **Workspace de Análisis**: Contenedor que asocia una licitación a sus documentos analizados y resultados de IA.
- **Resultado de Análisis**: JSON estructurado generado por Gemini con KPIs, comparativas y conclusión ejecutiva.
- **Notificación**: Alerta in-app generada por eventos en licitaciones activas (aclaraciones, cambios de estado).

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un analista puede visualizar el dashboard de análisis de una licitación adjudicada en menos de 2 minutos contados desde que accede al workspace (el análisis inicial de IA puede tomar hasta 30 segundos en primer acceso).
- **SC-002**: El sistema identifica y extrae el "acta de evaluación" correcto en al menos el 85% de licitaciones con adjuntos disponibles, sin intervención manual.
- **SC-003**: El dashboard ejecutivo histórico responde a la pregunta "¿cuánto vendió Sonda vs. TIVIT en 2025?" con datos numéricos reales extraídos de los análisis.
- **SC-004**: Una demostración completa del flujo (login → licitaciones → análisis → dashboard → chat) tarda menos de 5 minutos y no produce errores visibles para el espectador.
- **SC-005**: El módulo de notificaciones entrega alertas de aclaración dentro de los 60 minutos de que aparecen en Mercado Público.

---

## Fases del SDD — Hoja de Ruta

### Fase 0 — Foundation (COMPLETADA)
Módulos base operativos: Auth, Catálogo, Licitaciones (sync API), Mensajería (SignalR), estructura de DB con migraciones SQL. Demo: login funcional, lista de licitaciones visible.

### Fase 1 — Pipeline de Análisis Manual (COMPLETADA)
Módulo Análisis: workspace, upload manual de PDF, análisis con Gemini 2.5, dashboard estructurado, chat contextual. Demo: subir un acta manualmente y ver el análisis.

### Fase 2 — Automatización del Scraping (EN CURSO / PRIORITARIA)
Completar el `ScraperBackgroundService` para que:
- Identifique automáticamente el archivo "acta de evaluación" entre los adjuntos de una licitación.
- Dispare el análisis de Gemini automáticamente al detectar un acta no analizada.
- Sin intervención del usuario.
Demo objetivo: abrir una licitación adjudicada y ya tener el análisis listo.

### Fase 3 — Dashboard Ejecutivo Histórico (PENDIENTE)
Nuevo módulo o extensión del Análisis:
- Vista agregada de todos los análisis: ganadas vs. perdidas, ranking de competidores, diferencial económico.
- Filtros por período, organismo, tipo de licitación.
Demo objetivo: responder la pregunta de Francisco sobre Sonda con datos reales.

### Fase 4 — Notificaciones y Seguimiento Activo (PENDIENTE)
Completar el módulo de Notificaciones:
- Detección de solicitudes de aclaración en licitaciones activas.
- Alertas in-app en tiempo real.
Demo objetivo: mostrar notificación de aclaración llegando en vivo.

### Fase 5 — Despliegue Productivo GCP (PENDIENTE)
Configurar y desplegar en GCP según `docs/infraestructura-cu010.md`:
- Cloud Run (API + Web), Cloud SQL, Memorystore Redis, GCS, Secret Manager.
- Migración del repositorio a GitLab con pipeline CI/CD.
- Ambientes: Desarrollo/QA y Producción.

---

## Assumptions

- El sistema usa tres mecanismos de autenticación diferenciados: (a) `MP_TICKET` — token para la API REST pública de Mercado Público (sincronización de licitaciones, FR-001); (b) `MP_RUT`/`MP_PASSWORD` — credenciales web para el scraper Playwright que navega la interfaz de usuario de Mercado Público (descarga de adjuntos, FR-002); (c) `JWT_SECRET`/`JWT_ISSUER`/`JWT_AUDIENCE` — autenticación interna del MPM entre el scraper y la API propia. Los tres son independientes.
- El análisis con Gemini AI aplica únicamente a licitaciones con estado "adjudicada" en las que TIVIT fue oferente; no a todo el universo de Mercado Público.
- CU009 está excluido del alcance; solo se trabaja en CU010 (Caso 01).
- La infraestructura de despliegue es GCP (Escenario A del documento de infraestructura), dado que GCS y Gemini API ya son dependencias activas del sistema.
- La credencial de demo (admin@tivit.cl / test123) es válida para las presentaciones del jueves.
- El análisis inicial de licitaciones históricas (desde 2025-01-01) se ejecuta en background sin bloquear la UI; los resultados se van mostrando progresivamente.
