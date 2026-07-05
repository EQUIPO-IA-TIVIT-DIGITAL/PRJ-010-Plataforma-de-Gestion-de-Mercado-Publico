# Feature Specification: Fase 6 — Alertas Inteligentes por Palabras Clave

**Feature Branch**: `003-fase6-alertas-keywords`
**Created**: 2026-06-24
**Updated**: 2026-07-03 (ampliación de alcance tras reunión de repriorización)
**Status**: Planned
**Semana estimada**: Semana 2 (Julio 2026)
**Impacto**: Alto | **Complejidad**: Media-Alta | **Depende de**: Fase 5

**Actualización 2026-07-03**: Reunión "[CU010] - Revisión de Alcance" (2026-07-01) confirmó este módulo como la siguiente prioridad inmediata tras cerrar el módulo de Análisis histórico (`017-ajustes-urgentes-cliente`, ya implementado), y amplió el alcance original de dos maneras pedidas explícitamente por Francisco Lopez Balart: (1) el matching debe usar sinónimos vía IA, no solo la palabra literal — *"la inteligencia artificial puede ir consultando por sinónimos de la palabra hasta encontrar licitaciones que capaz la persona humana no puede encontrar"*; (2) cada alerta debe enviarse con información enriquecida (requisitos, competidores, presupuesto, forma de pago, multas, si es renovación y quién es el proveedor actual) directamente a los dos account managers de gobierno, no solo un link a la licitación.

---

## Contexto

El equipo comercial de TIVIT actualmente revisa manualmente mercadopublico.cl para encontrar licitaciones relevantes, usando palabras clave como "cloud", "cybersecurity", "SOC", "data center", "telecomunicaciones" y "cámaras". Esta fase implementa un sistema de alertas configurable donde el usuario define palabras clave, rubros y rangos de monto, y recibe una notificación cuando entra una nueva licitación que coincide — sin tener que buscar. La versión ampliada (2026-07-03) además expande cada keyword a sus sinónimos/conceptos relacionados vía IA, y adjunta a la notificación un resumen accionable (requisitos, competidores, presupuesto, forma de pago, multas, señal de renovación/proveedor actual) para que el account manager pueda decidir "go/no-go" sin abrir las bases.

---

## User Stories

### User Story 1 — Configurar alertas personalizadas (Priority: P1)

Un analista comercial de TIVIT quiere recibir una notificación cada vez que aparezca una nueva licitación de servicios cloud o infraestructura, sin revisar manualmente el portal.

**Why this priority**: Es la primera capa de inteligencia proactiva. El sistema pasa de reactivo a proactivo.

**Independent Test**: Configurar alerta con keyword "cloud" y monto > $10M. Cuando el sync detecta una licitación nueva que coincide, el usuario recibe notificación en menos de 60 minutos.

**Acceptance Scenarios**:
1. **Given** el usuario está en la pantalla de alertas, **When** define una regla (keyword + monto mínimo + rubros opcionales), **Then** la alerta se guarda y aparece en su lista de alertas activas.
2. **Given** una alerta activa con keyword "datacenter", **When** el sync diario trae una licitación con ese término en nombre o descripción, **Then** el usuario recibe notificación in-app con link directo a la licitación.
3. **Given** el usuario tiene 3 alertas, **When** entra una licitación que coincide con 2 de ellas, **Then** recibe solo 1 notificación (deduplicada) con mención de ambas reglas.

---

### User Story 2 — Gestión del panel de alertas (Priority: P2)

El analista necesita ver, editar y desactivar sus alertas sin tener que recurrir a soporte técnico.

**Why this priority**: Sin autogestión, el sistema de alertas generaría tickets de soporte constantes.

**Independent Test**: El usuario activa, edita el monto mínimo y desactiva una alerta — todo desde el frontend sin intervención técnica.

**Acceptance Scenarios**:
1. **Given** la lista de alertas, **When** el usuario hace toggle en una alerta, **Then** queda pausada/activa sin perder su configuración.
2. **Given** el usuario edita una alerta existente, **When** guarda los cambios, **Then** las nuevas reglas aplican desde el siguiente ciclo de sync.

---

### User Story 3 — Expansión de keywords por sinónimos vía IA (Priority: P1)

Un analista configura la alerta con una sola palabra clave (p. ej. "SOC") y el sistema detecta también licitaciones que usan términos relacionados que un humano tardaría más en enumerar (p. ej. "centro de operaciones de seguridad", "monitoreo de seguridad 24/7").

**Why this priority**: Es el pedido explícito de la reunión del 2026-07-01 — sin esto, la alerta es equivalente a la búsqueda manual actual y no aporta el salto de cobertura que el cliente pidió.

**Independent Test**: Configurar una alerta con keyword "SOC" y confirmar que una licitación nueva que menciona únicamente "centro de operaciones de seguridad" (sin la sigla "SOC") dispara la alerta igualmente.

**Acceptance Scenarios**:
1. **Given** una regla con una keyword, **When** el motor de matching evalúa una licitación nueva, **Then** también considera sinónimos y conceptos relacionados generados por IA para esa keyword, no solo coincidencia literal.
2. **Given** una licitación que coincide solo por sinónimo (no por la palabra literal), **When** se genera la notificación, **Then** ésta indica qué término disparó el match para dar trazabilidad al usuario.

---

### User Story 4 — Notificación enriquecida a account managers (Priority: P1)

Cuando se detecta una licitación relevante, el sistema debe entregar a los dos account managers de gobierno la información necesaria para decidir "go/no-go" sin tener que abrir las bases: requisitos, competidores, presupuesto, forma de pago, multas, y si es un proceso de renovación (con el proveedor actual, si las bases lo indican).

**Why this priority**: Es el segundo pedido explícito de la reunión del 2026-07-01 — Francisco fue específico en que un link a la licitación no es suficiente para tomar la decisión rápido.

**Independent Test**: Disparar una alerta sobre una licitación cuyas bases mencionen "el proveedor actual del servicio, Sonda, lleva 6 años" y confirmar que la notificación generada incluye esa señal de renovación/proveedor incumbente sin que el usuario abra el PDF de bases.

**Acceptance Scenarios**:
1. **Given** una licitación que dispara una alerta, **When** se genera la notificación, **Then** incluye un resumen con requisitos, competidores conocidos, presupuesto, forma de pago y multas cuando esa información está disponible en los datos ya sincronizados/analizados de la licitación.
2. **Given** una licitación cuyas bases indican que es una renovación, **When** se genera la notificación, **Then** se marca explícitamente como "posible renovación" e indica el proveedor actual si está mencionado en el texto.
3. **Given** información no disponible para alguno de estos campos, **When** se genera la notificación, **Then** el campo se omite o se marca como "no determinado" en vez de inventar un valor.
4. **Given** una alerta disparada, **When** se enruta la notificación, **Then** llega a los usuarios configurados como account managers de gobierno (no solo al creador de la regla).

---

## Funcionalidades principales

- Página `/alertas` con tabla de reglas activas/pausadas
- Formulario de creación: keywords (múltiples), monto mínimo/máximo, tipos de licitación, organismos (opcional)
- Motor de matching que evalúa cada licitación del sync contra las reglas de todos los usuarios, expandiendo cada keyword a sinónimos/conceptos relacionados vía IA
- Deduplicación: una licitación no dispara la misma alerta dos veces al mismo usuario
- Generación de resumen enriquecido por licitación disparada (requisitos, competidores, presupuesto, forma de pago, multas, señal de renovación/proveedor actual), reutilizando en lo posible el análisis ya producido por `MPM.Modules.Analisis` cuando exista
- Enrutamiento de la notificación a los account managers configurados para el rubro/organismo, no solo al creador de la regla
- Integración con módulo de Notificaciones existente
- Nuevo módulo `MPM.Modules.Alertas`

## Definición de Hecho

- [ ] CRUD de alertas funcional en frontend
- [ ] Motor de matching ejecuta en cada ciclo de sync
- [ ] Expansión de keywords por sinónimos/conceptos vía IA, con trazabilidad del término que disparó el match
- [ ] Notificación generada con resumen enriquecido (requisitos, competidores, presupuesto, forma de pago, multas, señal de renovación/proveedor actual) y link a la licitación
- [ ] Notificación enrutada a los account managers de gobierno configurados
- [ ] Deduplicación correcta (no spam)
- [ ] Panel muestra historial de alertas disparadas
