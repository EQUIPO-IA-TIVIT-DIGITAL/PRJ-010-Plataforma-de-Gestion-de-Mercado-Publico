# CU010 - Plataforma de Gestión de Licitaciones Públicas

---

## Slide 1 — Portada

**CU 010 - Plataforma de Gestión y Análisis de Licitaciones Públicas**

Caso de uso propuesto | Planificación de Sprints & Estado del Proyecto

**Matías Mendez / Equipo Digital TIVIT**

---

## Slide 2 — Contexto / Valor de Negocio

**CU 010 - Plataforma de Gestión y Análisis de Licitaciones Públicas**

**Contexto actual**

- **Análisis ineficiente:** Lectura manual de actas de evaluación densas y extracción lenta de datos clave.
- **Sin seguimiento activo:** Imposibilidad de monitorear aclaraciones en licitaciones en curso sin revisar manualmente el portal.
- **Pérdidas sin diagnóstico:** Licitaciones adjudicadas a competidores sin saber exactamente por qué ni en qué criterios se perdió.
- **Información dispersa:** Datos históricos de competidores, márgenes y organismos desconectados entre sí.

**Solución propuesta**

- **Ingesta automática:** Sincronización diaria desde la API oficial de Mercado Público con backfill histórico.
- **Scraping + IA (Gemini):** Descarga automática del acta de evaluación y análisis estructurado de criterios, puntajes y competidores.
- **Dashboard ejecutivo:** Vista histórica de ganadas vs. perdidas, ranking de competidores y diferencial económico.
- **Seguimiento activo:** Alertas automáticas cuando aparecen nuevas aclaraciones en licitaciones seguidas.
- **Roadmap IA:** Pipeline completo para análisis de bases, predictor de éxito, pricing intelligence e integración ERP.

---

## Slide 3 — Sprints (Parte 1)

**CU 010 - Plataforma de Gestión y Análisis de Licitaciones Públicas**

Fecha Inicio: 17/04/2026 | Fecha Fin estimada: 05/10/2026

---

**Setup Inicial (17/04/2026 – 18/04/2026)**

- Solicitud ticket API Mercado Público y configuración de credenciales .env → **Completado**
- Despliegue del entorno de desarrollo local con Docker Compose (PostgreSQL, Redis, Backend .NET, Frontend React) → **Completado**
- Ejecución de migraciones iniciales de base de datos → **Completado**

---

**Sprint 1 (18/04/2026 – 11/05/2026)**

- Worker de Ingesta: Sincronización diaria automatizada desde la API oficial de Mercado Público → **Completado**
- Procesamiento en Segundo Plano: Redis y background services para manejo asíncrono y backfill histórico → **Completado**
- API REST (.NET 8): Desarrollo de endpoints principales para Licitaciones y catálogos de referencia → **Completado**
- Optimización de Base de Datos: Índices en PostgreSQL para tiempos de respuesta rápidos → **Completado**

---

**Sprint 2 (11/05/2026 – 22/05/2026)**

- Dependencia de Terceros: Reuniones con BDM para confirmar servicios necesarios en esta fase → **Completado**
- Desarrollo del Dashboard principal (KPIs, tendencias mensuales, gráficos de distribución) → **Completado**
- Búsqueda Avanzada: Filtrado multicriterio por estado, tipo, fecha, monto, región y rubro → **Completado**
- Exportación de resultados (Excel) y habilitación de métricas de rendimiento → **Completado**

---

**Sprint 3 (23/05/2026 – 29/06/2026) — SPRINT ACTUAL**

- Módulo Auth: login, recuperación contraseña, JWT, roles → **Completado**
- Sincronización diaria licitaciones desde API Mercado Público → **Completado**
- Catálogo: estados, tipos, organismos → **Completado**
- Mensajería real-time con SignalR → **Completado**
- Upload manual de PDF acta de evaluación → **Completado**
- Análisis IA con Gemini 2.5 (criterios, puntajes, competidores, conclusión) → **Completado**
- Workspace de análisis con chat contextual → **Completado**
- Dashboard ejecutivo: KPIs ganadas/perdidas, ranking competidores, diferencial económico → **Completado**
- Módulo Notificaciones: tablas, stored procedures, bell en frontend → **Completado**
- Dockerfile: Node.js 20 + Playwright Chromium para scraping automático → **Completado**
- docker-compose: variables MP_RUT, MP_PASSWORD, SCRAPER_ENABLED → **Completado**
- Detección automática de "Acta de Evaluación" en adjuntos de licitación → **Completado**
- Disparo automático de análisis Gemini post-scraping → **Completado**
- Migración V072: tablas licitaciones_seguidas y licitaciones_aclaraciones → **Completado**
- Migración V073: 5 Stored Procedures de seguimiento activo → **Completado**
- AclaracionMonitorService: background service MP API cada 30 minutos → **Completado**
- Endpoints: POST seguir licitación, GET esSeguida, GET licitaciones seguidas → **Completado**
- Frontend: botón estrella toggle en tabla de licitaciones → **Completado**
- Notificaciones tipo aclaracion_detectada en frontend → **Completado**
- Migración y despliegue en infraestructura GCP → **Pendiente** *(en espera de infraestructura)*

---

## Slide 4 — Sprints (Parte 2)

**CU 010 - Plataforma de Gestión y Análisis de Licitaciones Públicas**

Fecha Inicio: 17/04/2026 | Fecha Fin estimada: 05/10/2026

---

**Sprint 4 (30/06/2026 – 06/07/2026)** — spec 002 Deploy GCP

- Infraestructura Cloud: Provisión de OBS, CSMS, LTS y Secret Manager en Huawei Cloud → **Pendiente**
- Migración de secretos: De .env plano hacia Huawei CSMS (eliminación de secretos en código) → **Pendiente**
- Backup automático: pg_dump diario cifrado con KMS hacia OBS → **Pendiente**
- Monitoreo y Logging: Prometheus + Grafana on-premise + Huawei LTS centralizado → **Pendiente**
- CI/CD: Pipeline GitLab para despliegue automático en entornos Dev/QA/Prod con HTTPS (Let's Encrypt) → **Pendiente**

---

**Sprint 5 (07/07/2026 – 13/07/2026)** — spec 003 Alertas Keywords

- Motor de Alertas por Palabras Clave: Detección de términos relevantes en nuevas licitaciones publicadas → **Pendiente**
- Configuración de keywords por usuario (panel de administración) → **Pendiente**
- Notificación in-app + email cuando una licitación nueva coincide con keywords configuradas → **Pendiente**

---

**Sprint 6 (14/07/2026 – 20/07/2026)** — spec 004 Pipeline Oportunidades Kanban

- Pipeline de Oportunidades: Vista Kanban con estados (Detectada → Evaluación → Cotización → Presentada → Resultado) → **Pendiente**
- Gestión de etapas por licitación con responsable asignado y fecha de cierre → **Pendiente**
- Integración con Dashboard Ejecutivo para trazabilidad de conversión → **Pendiente**

---

**Sprint 7 (21/07/2026 – 27/07/2026)** — spec 005 Análisis IA Bases Licitación

- Análisis IA de Bases de Licitación: Extracción automática de requisitos técnicos, documentos exigidos y criterios de evaluación → **Pendiente**
- Detección de riesgos contractuales y alertas de viabilidad "Go / No-Go" → **Pendiente**
- Visualización de resumen de bases con trazabilidad a la fuente del documento → **Pendiente**

---

**Sprint 8 (28/07/2026 – 03/08/2026)** — spec 006 Reportes Ejecutivos Automáticos

- Reportes Ejecutivos Automáticos: Generación de PDF/Excel semanal con KPIs, win rate y ranking competidores → **Pendiente**
- Resumen de licitaciones próximas a cerrar y alertas de plazos → **Pendiente**
- Distribución automática por email a stakeholders configurados → **Pendiente**

---

**Sprint 9 (04/08/2026 – 10/08/2026)** — spec 007 Notificaciones Multicanal Email+WhatsApp

- Notificaciones Multicanal: Integración con Email SMTP y WhatsApp Business API → **Pendiente**
- Motor anti-spam: agrupación horaria, umbral de frecuencia y configuración de preferencias por usuario → **Pendiente**
- Canal de notificación configurable por tipo de alerta → **Pendiente**

---

**Sprint 10 (11/08/2026 – 17/08/2026)** — spec 008 Inteligencia Competitiva Avanzada

- Inteligencia Competitiva Avanzada: Perfiles de competidores con historial de victorias, montos y frecuencia → **Pendiente**
- Análisis de enfrentamientos directos TIVIT vs. competidores por organismo y tipo de licitación → **Pendiente**
- Ranking dinámico actualizado con cada nuevo análisis de acta → **Pendiente**

---

**Sprint 11 (18/08/2026 – 24/08/2026)** — spec 009 Gestión de Garantías Bancarias

- Gestión de Garantías Bancarias: Registro de garantías con fechas de vencimiento y montos → **Pendiente**
- Alertas automáticas a 30/14/7 días antes del vencimiento → **Pendiente**
- Dashboard de garantías vigentes por licitación y estado → **Pendiente**

---

**Sprint 12 (25/08/2026 – 31/08/2026)** — spec 010 CRM Organismos Compradores

- CRM de Organismos Compradores: Fichas por organismo con contactos, historial de licitaciones y win rate → **Pendiente**
- Notas internas y seguimiento de relación comercial por organismo → **Pendiente**
- Vista de organismos con mayor potencial según histórico de adjudicaciones → **Pendiente**

---

**Sprint 13 (01/09/2026 – 07/09/2026)** — spec 011 Predictor de Éxito

- Predictor de Éxito: Score 0–100% de probabilidad de ganar una licitación basado en histórico → **Pendiente**
- Variables: precio estimado, organismo, tipo de licitación, competidores habituales → **Pendiente**
- Simulador de precio: "¿Qué pasa si cotizo X?" → **Pendiente**

---

**Sprint 14 (08/09/2026 – 14/09/2026)** — spec 012 Pricing Intelligence

- Pricing Intelligence: Benchmarking de precios ganadores por categoría y organismo → **Pendiente**
- Recomendador de precio óptimo basado en datos históricos de adjudicación → **Pendiente**
- Alertas cuando el precio propuesto supera el rango histórico de éxito → **Pendiente**

---

**Sprint 15 (15/09/2026 – 21/09/2026)** — spec 013 Portal de Colaboración Externa

- Portal de Revisión Externa: Links compartibles con expiración para revisores externos → **Pendiente**
- Acceso solo lectura a análisis de licitación específica sin login → **Pendiente**
- Registro de accesos y tiempo de visualización por link → **Pendiente**

---

**Sprint 16 (22/09/2026 – 28/09/2026)** — spec 014 Gestión Documental de Propuestas

- Gestión Documental de Propuestas: Repositorio de plantillas con versionado → **Pendiente**
- Checklist automático 48 horas antes del cierre de licitación → **Pendiente**
- Indexación de casos de éxito para reutilización en nuevas propuestas → **Pendiente**

---

**Sprint 17 (29/09/2026 – 05/10/2026)** — spec 015 Integración ERP SAP/Oracle

- Integración ERP (SAP/Oracle): Creación automática de proyecto en ERP al ganar licitación → **Pendiente**
- Sincronización de datos de licitación (monto, organismo, tipo) al proyecto ERP → **Pendiente**
- Webhook bidireccional para actualizar estado de licitación desde ERP → **Pendiente**

---

## Slide 5 — Sprint Actual

**CU 010 - Plataforma de Gestión y Análisis de Licitaciones Públicas**

**Status: EN CURSO | SPRINT 03 de 17**

**Fecha:** 23/05/2026 – 29/06/2026

---

**Descripción:**
Sprint consolidado del spec 001. Cubre la totalidad de funcionalidades del módulo de análisis: Auth, sincronización, scraping automático con Playwright, análisis Gemini, dashboard ejecutivo, notificaciones y seguimiento de licitaciones activas. Pendiente únicamente la migración y despliegue en infraestructura GCP, bloqueada por disponibilidad de infraestructura.

---

**Tareas del Sprint 3**

- Módulo Auth: login, recuperación contraseña, JWT, roles → **Completado**
- Sincronización diaria licitaciones desde API Mercado Público → **Completado**
- Catálogo, Mensajería SignalR, Upload manual PDF → **Completado**
- Análisis IA Gemini 2.5 + Workspace + Chat contextual → **Completado**
- Dashboard ejecutivo: KPIs, ranking competidores, diferencial → **Completado**
- Módulo Notificaciones completo (tablas, SPs, frontend) → **Completado**
- Scraping automático Playwright + Node.js + Docker → **Completado**
- Seguimiento activo: V072, V073, AclaracionMonitorService, endpoints, frontend ⭐ → **Completado**
- Migración y despliegue en infraestructura GCP → **Pendiente** *(en espera de infraestructura)*

---

**Dependencias**

| Dependencia | Tipo | Estado | Responsable |
|-------------|------|--------|-------------|
| Infraestructura GCP (Cloud Run, Cloud SQL, OBS, CSMS) | Infraestructura cloud | 🔴 Pendiente | Sprint 4 |
| API Mercado Público (endpoint aclaraciones) | API pública ChileCompra | ✅ Disponible | ChileCompra |
| Google Gemini 2.5 API Key | Servicio IA | ✅ Disponible | Google Cloud |
| Huawei Cloud (WAF, LTS, KMS) | Infraestructura producción | 📋 Sprint 4 | Huawei |

---

**Riesgos**

| # | Riesgo | Probabilidad | Impacto |
|---|--------|-------------|---------|
| R1 | Infraestructura GCP no disponible antes de Sprint 4 — retrasa el despliegue a producción | Media | Alto |
| R2 | Mercado Público cambia estructura web y rompe scraper Playwright | Media | Alto |
| R3 | Gemini retorna JSON inválido en análisis de actas complejas | Baja | Medio |
| R4 | Secretos en .env plano sin cifrado hasta Sprint 4 (Deploy GCP) | Alta | Alto |
| R5 | Rate limit MP API en monitor de aclaraciones (50 licitaciones × 1 req/s) | Baja | Medio |
| R6 | Cross-module entre Licitaciones y Notificaciones sin interfaz en Shared | Media | Alto |

---

## Slide 6 — Costos

**CU 010 - Costos de Infraestructura Cloud**

| Escenario | Uso |
|-----------|-----|
| **Mínimo (Piloto)** | 5 licitaciones analizadas/mes · 1–5 usuarios activos · Sin WAF |
| **Intermedio** | 25 licitaciones analizadas/mes · 6–20 usuarios activos · WAF básico |
| **Masivo (Validación Operativa)** | 100+ licitaciones/mes · 21–50 usuarios activos · WAF avanzado |

---

| Concepto | Mínimo (Piloto) | Intermedio | Masivo | Tipo |
|----------|-----------------|------------|--------|------|
| Cloud SQL (PostgreSQL 16) | $52.50 | $52.50 | $52.50 | Fijo |
| Compute / Cloud Run (API .NET 8) | $6.94 | $6.94 | $6.94 | Fijo |
| Cloud Run (Frontend React / nginx) | $2.00 | $4.00 | $8.00 | Variable |
| Redis (Memorystore — backplane SignalR) | $10.00 | $20.00 | $40.00 | Fijo |
| Google Gemini 2.5 Flash (Análisis PDF) | $1.50 | $6.00 | $22.00 | Variable |
| Google Gemini 2.5 Flash (Chat contextual) | $0.50 | $2.00 | $6.00 | Variable |
| Almacenamiento PDFs (GCS / OBS) | $3.00 | $8.00 | $20.00 | Variable |
| Logging + Monitoreo (LTS / Cloud Logging) | $3.00 | $5.00 | $8.00 | Variable |
| WAF (Cloud Armor / Huawei WAF) | — | $50.00 | $150.00 | Variable |
| SSL + DNS + Secretos (CSMS/Secret Manager) | $3.50 | $3.50 | $3.50 | Fijo |
| **Total mensual estimado** | **$83.44** | **$157.94** | **$316.94** | |
| **Total anual estimado** | **~$1,001/año** | **~$1,895/año** | **~$3,803/año** | |

---

Infraestructura base (Fijo para todos los módulos):

- Inversión acumulada en desarrollo: A confirmar con equipo
- Consumo mes anterior: —
- Consumo mes actual: —

> *Nota: Los costos de mano de obra / horas-hombre no están documentados en el SDD. Se recomienda completar esta tabla con la estimación del equipo antes de presentar a stakeholders.*
