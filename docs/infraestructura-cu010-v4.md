# Recursos e Infraestructura Tecnológica — CU010 Mercado Público

**Proyecto:** MPM — Mercado Público Management  
**Área:** Digital — TIVIT  
**Fecha:** Junio 2026  
**Versión:** 4.0 — Arquitectura híbrida On-Premise + Huawei Cloud

---

## 1. Descripción General del Sistema

MPM es una plataforma web para gestión y análisis de licitaciones públicas chilenas provenientes de [mercadopublico.cl](https://www.mercadopublico.cl). El sistema hoy corre completamente en el servidor on-premise de TIVIT mediante Docker Compose.

### Estado actual del código (lo que corre hoy)

| Componente | Tecnología actual |
|------------|-------------------|
| Backend API | .NET 8 (ASP.NET Core) — monolito modular, 6 módulos |
| Frontend | React 18 + TypeScript + Ant Design 5, servido por nginx |
| Base de datos | PostgreSQL 16 (contenedor Docker) |
| Caché / Pub-Sub | Redis 7 (contenedor Docker, backplane para SignalR) |
| Mensajería en tiempo real | ASP.NET Core SignalR |
| Almacenamiento de archivos | `LocalStorageService` — disco local del servidor en `/app/uploads` |
| Análisis IA | `GeminiService` — Google Gemini 2.5 Pro (estado transitorio, ver Sección 6) |
| Scraper | Node.js + Playwright Chromium (dentro del contenedor api) |
| Secretos | Variables de entorno en `.env` plano |
| Monitoreo | Logging básico vía `ILogger<T>` (.NET) |
| Contenedores | Docker Compose — 4 servicios: `db`, `redis`, `api`, `web` |

### Principio de gobierno tecnológico

> **La arquitectura objetivo de MPM utiliza exclusivamente servicios del ecosistema Huawei Cloud para sus componentes de infraestructura gestionada, almacenamiento, seguridad y servicios de IA.** Esta decisión busca homogeneizar la administración, simplificar la gestión de seguridad, optimizar la integración entre servicios y reducir dependencias con plataformas externas como Google Cloud o AWS.

---

## 2. Estado Actual vs Arquitectura Objetivo (Fase 5)

Esta sección es el punto de referencia principal para entender qué ya existe en el código y qué se implementará en Fase 5 (deploy en producción).

| Componente | Estado actual (en código hoy) | Objetivo Fase 5 (a implementar) |
|---|---|---|
| **Almacenamiento archivos** | `LocalStorageService` — `/app/uploads` en disco local | `ObsStorageService` — Huawei OBS (bucket `mpm-licitaciones-prod`) |
| **Backups de BD** | Sin script de backup automatizado | `pg_dump` + gzip + upload a OBS bucket `mpm-backups-bd` |
| **Servicio de IA** | `GeminiService` (`gemini-2.5-pro` vía Google API) | `PanguService` (Pangu/DeepSeek vía Huawei MaaS) |
| **Gestión de secretos** | `.env` plano en el servidor | Huawei CSMS — secretos fuera del sistema de archivos |
| **Monitoreo / métricas** | `ILogger<T>` básico, sin dashboards | Prometheus + Grafana + exporters (Docker on-premise) |
| **Logs centralizados** | Logs en stdout/stderr de contenedores | Huawei LTS (vía ICAgent en el servidor) |
| **Alertas operativas** | Sin alertas configuradas | Alertmanager → Huawei SMN → email del equipo |
| **Certificado TLS** | Sin HTTPS (desarrollo local) | Let's Encrypt (certbot) o Huawei SSL Certificate Manager |
| **Firewall de aplicación** | Sin WAF | Huawei WAF (cloud-mode) |
| **Cifrado de archivos** | Sin cifrado en reposo | Huawei KMS sobre bucket OBS |
| **IP pública** | IP privada local | IP estática del ISP del datacenter TIVIT |
| **DNS** | Sin zona gestionada | Huawei DNS — zona del dominio corporativo |
| **Auditoría cloud** | Sin auditoría | Huawei CTS — log de operaciones sobre recursos Huawei |
| **Consola BD** | Cliente local (DBeaver/psql) | Huawei DAS — interfaz web sin cliente instalado |
| **Auto-restart** | Sin restart policy | `restart: unless-stopped` en docker-compose.prod.yml |

> **Interpretación**: todo lo de la columna "Estado actual" existe en el código fuente del repositorio. Todo lo de "Objetivo Fase 5" es trabajo planificado pero aún no implementado.

---

## 3. Catálogo de Servicios Huawei Cloud

Análisis de todos los servicios relevantes para MPM. Cada servicio indica su estado:

- `✅ Contratar en Fase 5` — servicios cloud a activar durante el primer deploy a producción
- `🔵 Fases futuras` — relevantes cuando el sistema crezca o el cómputo migre a nube
- `⚪ Opcional` — evaluación long-term

> **Restricción clave on-premise**: Los servicios de Huawei Cloud que realizan backups de bajo nivel, recolectan métricas del SO, o gestionan redes **solo funcionan sobre recursos dentro de Huawei Cloud** (ECS, EVS, RDS). Para el servidor on-premise funcionan únicamente si se instala el agente ICAgent de Huawei. Cada servicio indica esta restricción cuando aplica.

---

### 3.1 Cómputo

| Servicio Huawei | Descripción | Equivalente AWS | Uso en MPM | Rec. |
|---|---|---|---|---|
| **ECS** (Elastic Cloud Server) | VM Linux con vCPU, RAM y disco configurables. Equivalente a un servidor virtual | EC2 | Hospedar todos los contenedores Docker si se migra de on-premise a nube | 🔵 |
| **CCE** (Cloud Container Engine) | Kubernetes gestionado con auto-scaling y rolling deploys | EKS | Solo cuando el volumen de usuarios justifique Kubernetes y múltiples réplicas | ⚪ |
| **CCI** (Cloud Container Instance) | Contenedores serverless. Pago por CPU/RAM por segundo, sin gestionar nodos | Fargate | Ejecutar el scraper Node.js + Playwright como tarea puntual sin servidor siempre encendido | 🔵 |
| **FunctionGraph** | Funciones serverless (FaaS). Ejecución por evento o cron | Lambda | Ejecutar tareas de mantenimiento y webhooks sin servidor dedicado | 🔵 |

---

### 3.2 Base de Datos

| Servicio Huawei | Descripción | Equivalente AWS | Uso en MPM | Rec. |
|---|---|---|---|---|
| **RDS for PostgreSQL** | PostgreSQL gestionado con backups automáticos, failover HA y réplicas de lectura | RDS PostgreSQL | Reemplazar el contenedor PostgreSQL Docker when el servidor on-premise migre a nube | 🔵 |
| **DCS** (Distributed Cache Service) | Redis gestionado con modo maestro-réplica y Sentinel | ElastiCache | Reemplazar el contenedor Redis Docker junto con RDS | 🔵 |
| **GaussDB** | BD distribuida propia de Huawei compatible con PostgreSQL | Aurora | Solo si la BD supera los 5 TB o se requiere sharding | ⚪ |
| **DAS** (Data Admin Service) | Consola web para administrar BD: queries, métricas, gestión de usuarios | DBeaver Cloud | Interfaz web para consultas de BD sin necesitar cliente instalado en el equipo | ✅ |

---

### 3.3 Almacenamiento

| Servicio Huawei | Descripción | Equivalente AWS | Uso en MPM | Rec. |
|---|---|---|---|---|
| **OBS** (Object Storage Service) | Almacenamiento de objetos escalable. Ciclo de vida, versionado, pre-signed URLs | S3 | **Objetivo Fase 5.** Reemplaza `/app/uploads`. Dos buckets: `mpm-licitaciones-prod` (PDFs) y `mpm-backups-bd` (dumps PostgreSQL) | ✅ |
| **OBS Archive** (clase de almacenamiento) | Tier frío dentro de OBS. Hasta 60% más barato que Standard, para acceso infrecuente | Glacier | PDFs de licitaciones con más de 12 meses migran automáticamente a Archive | ✅ |
| **EVS** (Elastic Volume Service) | Disco de bloque SSD adjunto a una instancia ECS | EBS | Volumen persistente para PostgreSQL si el cómputo migra a ECS | 🔵 |
| **SFS** (Scalable File Service) | Sistema de archivos NFS compartido montable en múltiples servidores | EFS | Compartir archivos temporales si el scraper escala horizontalmente en ECS | ⚪ |
| **CBR** (Cloud Backup and Recovery) | Backup automático de recursos **Huawei Cloud**: ECS, EVS, RDS. **No tiene capacidad de backup sobre servidores on-premise ni contenedores Docker locales.** | AWS Backup | Relevante solo cuando el cómputo migre a ECS o la BD a RDS dentro de Huawei Cloud | 🔵 |

> **Aclaración CBR**: CBR opera mediante snapshots de volúmenes EVS y backups de instancias RDS dentro de Huawei Cloud. En la arquitectura actual (PostgreSQL en Docker on-premise) no puede realizar respaldos de bajo nivel ni restauraciones directas. La estrategia de backup para el escenario on-premise se detalla en la Sección 5.

---

### 3.4 Redes y Conectividad

| Servicio Huawei | Descripción | Equivalente AWS | Uso en MPM | Rec. |
|---|---|---|---|---|
| **DNS** (Domain Name Service) | Zona DNS pública. Registros A, CNAME, gestión de dominio | Route53 | Apuntar el dominio de MPM a la IP estática del servidor on-premise proporcionada por el ISP de TIVIT | ✅ |
| **VPC** (Virtual Private Cloud) | Red privada aislada en Huawei Cloud. Subredes, tablas de rutas, ACLs | VPC | Necesaria cuando se desplieguen recursos ECS, RDS o ELB en Huawei Cloud | 🔵 |
| **EIP** (Elastic IP Address) | IP pública estática **asociable exclusivamente a recursos dentro de Huawei Cloud** (ECS, ELB, NAT Gateway). No puede asignarse a un servidor on-premise directamente. | EIP | Relevante cuando se despliegue ECS o ELB en nube. La IP del servidor on-premise la provee el ISP de TIVIT, no Huawei. | 🔵 |
| **ELB** (Elastic Load Balance) | Balanceador HTTP/HTTPS/TCP con terminación TLS y health checks | ALB/NLB | Distribuir tráfico entre múltiples instancias de la API cuando se escale horizontalmente en ECS | 🔵 |
| **NAT Gateway** | Permite salida a internet desde instancias ECS en subred privada de Huawei Cloud. **No aplica sobre servidores on-premise**, que usan la salida a internet del ISP de TIVIT directamente. | NAT Gateway | Aplica en Nivel 2, cuando existan instancias ECS en subred privada de Huawei Cloud | 🔵 |
| **VPN Gateway** | Túnel VPN IPsec entre el datacenter on-premise de TIVIT y Huawei Cloud | AWS VPN | Conexión privada segura para cuando se expanda a RDS/ECS en nube | 🔵 |
| **CDN** (Content Delivery Network) | Distribución de contenido estático desde edge locations | CloudFront | Servir el bundle React desde nodos CDN en Chile para menor latencia | 🔵 |
| **Direct Connect** | Fibra dedicada entre datacenter TIVIT y Huawei Cloud | Direct Connect | Solo si el volumen de transferencia a OBS requiere latencia garantizada y ancho de banda dedicado | ⚪ |

> **Aclaración EIP**: La IP pública del servidor on-premise es proporcionada por el ISP del datacenter TIVIT y es independiente de Huawei Cloud. La EIP de Huawei solo aplica a recursos desplegados dentro de la nube (ECS, ELB, NAT Gateway). Para que el servidor on-premise use una IP de Huawei Cloud como punto de entrada, se requeriría VPN Gateway o Direct Connect como intermediario.

> **Aclaración NAT Gateway**: El servidor on-premise utiliza la conexión a internet del datacenter TIVIT para toda la conectividad saliente (a OBS, MaaS, mercadopublico.cl, etc.). El NAT Gateway de Huawei solo es necesario en el momento en que existan instancias ECS en subredes privadas de Huawei Cloud sin IP pública propia.

---

### 3.5 Seguridad

| Servicio Huawei | Descripción | Equivalente AWS | Uso en MPM | Rec. |
|---|---|---|---|---|
| **IAM** (Identity and Access Management) | Control de acceso a recursos Huawei Cloud. Usuarios, roles, políticas, MFA | IAM | Gestionar quién puede acceder a OBS, CSMS y otros recursos. Credenciales separadas por servicio | ✅ |
| **CSMS** (Cloud Secret Management Service) | Almacén de secretos gestionado. Variables sensibles fuera del sistema de archivos del servidor | Secrets Manager | Almacenar `JWT_SECRET`, `PANGU_API_KEY`, `MP_TICKET`, `MP_PASSWORD`, `DB_PASSWORD`. Reemplaza `.env` plano en producción | ✅ |
| **KMS** (Key Management Service) | Claves de cifrado gestionadas. Cifra datos en OBS en reposo | KMS | Cifrado del bucket OBS que contiene PDFs de licitaciones | ✅ |
| **WAF** (Web Application Firewall) | Firewall de aplicación web. Bloquea SQLi, XSS, fuerza bruta, bots maliciosos | WAF / Cloud Armor | Proteger el endpoint de autenticación `/api/v1/auth/login` y la API pública | ✅ |
| **Anti-DDoS** | Protección automática contra ataques volumétricos | Shield | Se activa sobre recursos Huawei Cloud con IP pública (ELB, NAT). No aplica directamente sobre el servidor on-premise | 🔵 |
| **SSL Certificate Manager** | Provisión y renovación de certificados TLS | ACM | Alternativa a Let's Encrypt para el certificado HTTPS del sistema | ✅ |
| **HSS** (Host Security Service) | Detección de intrusos y análisis de vulnerabilidades en el servidor. Requiere instalación de agente | GuardDuty | Monitoreo de seguridad del servidor on-premise (instalar agente HSS en el SO) | 🔵 |
| **VSS** (Vulnerability Scan Service) | Escaneo de vulnerabilidades en el endpoint web público | Inspector | Escanear la URL del sistema antes de cada release | 🔵 |
| **CTS** (Cloud Trace Service) | Auditoría de todas las operaciones sobre recursos Huawei Cloud. Log inmutable | CloudTrail | Registro de quién accedió a OBS, modificó secretos en CSMS o cambió configuración de IAM | ✅ |

> **Nota WAF — estimación de costos**: El costo del servicio WAF en Huawei Cloud varía entre **USD 50–200/mes** en modo cloud-mode básico, y puede superar los USD 400/mes en planes dedicados o con múltiples dominios. **La estimación debe validarse en la consola de Huawei Cloud contra el tráfico real esperado antes de contratar.** Se recomienda iniciar con el plan cloud-mode mínimo y escalar según necesidad operativa.

---

### 3.6 Monitoreo y Observabilidad

La estrategia de observabilidad se divide en dos capas complementarias con tecnologías distintas según el alcance:

#### Capa on-premise — stack open source (Fase 5, a implementar en docker-compose.prod.yml)

Herramientas instaladas como contenedores Docker adicionales en el servidor. Costo cero. Sin dependencias de red cloud para funcionar.

| Herramienta | Función | Fuente de métricas |
|---|---|---|
| **Prometheus** | Recolección y almacenamiento de métricas con retención configurable (7–15 días recomendado) | Todos los exporters |
| **Grafana** | Visualización con dashboards. Interfaz web accesible en puerto interno del servidor | Prometheus |
| **Alertmanager** | Enrutamiento de alertas a email o webhook (puede integrar con Huawei SMN) | Prometheus |
| **node_exporter** | Métricas del SO Linux: CPU, RAM, disco, red, procesos | Sistema operativo |
| **cAdvisor** | Métricas por contenedor Docker: CPU/RAM por servicio, I/O | Docker Engine |
| **postgres_exporter** | Métricas de PostgreSQL: conexiones activas, queries lentas, tamaño de BD, locks | PostgreSQL |
| **redis_exporter** | Métricas de Redis: memoria usada, hit rate, comandos/segundo | Redis |
| **nginx_exporter** | Métricas del frontend: requests/s, códigos HTTP, latencia | nginx |

#### Capa Huawei Cloud — servicios gestionados

| Servicio Huawei | Descripción | Uso en MPM | Rec. |
|---|---|---|---|
| **LTS** (Log Tank Service) | Centralización de logs desde el servidor vía agente ICAgent. Búsqueda y retención configurable | Logs de la API, scraper y PostgreSQL centralizados en Huawei Cloud. Requiere instalar ICAgent en el SO del servidor | ✅ |
| **SMN** (Simple Message Notification) | Envío de notificaciones a email y SMS ante eventos críticos | Destino de alertas de Alertmanager y LTS. Alerta al equipo cuando el sistema cae o el disco supera el 90% | ✅ |
| **CES** (Cloud Eye Service) | Monitoreo de recursos **Huawei Cloud**: ECS, RDS, DCS. Requiere ICAgent para servidores on-premise | Aplica en Nivel 2 cuando el cómputo esté en ECS. Para métricas del servidor on-premise usar node_exporter + Prometheus | 🔵 |
| **AOM** (Application Operations Management) | Monitoreo de contenedores Docker en infraestructura **Huawei Cloud** | Aplica en Nivel 2. Para on-premise usar cAdvisor + Prometheus | 🔵 |
| **APM** (Application Performance Management) | Trazabilidad distribuida por request. Detecta endpoints lentos y queries problemáticas | Nivel 2+, cuando el volumen de usuarios justifique trazabilidad detallada | 🔵 |

> **Aclaración CES/AOM**: Estos servicios monitorizan métricas de instancias ECS, clústeres RDS y cachés DCS dentro de Huawei Cloud. Para operar sobre el servidor on-premise se requiere instalar el agente ICAgent de Huawei en el sistema operativo, lo que introduce una dependencia de gestión adicional. En Fase 5 se prioriza el stack Prometheus (costo cero, sin agentes externos, control total) y se reserva CES/AOM para cuando el cómputo migre a Huawei Cloud.

---

### 3.7 DevOps y CI/CD (CodeArts)

| Servicio Huawei | Descripción | Equivalente | Uso en MPM | Rec. |
|---|---|---|---|---|
| **CodeArts Repo** | Repositorio Git con merge requests y code review | GitLab | Hospedar el código fuente si se migra del repositorio actual | 🔵 |
| **CodeArts Build** | CI: compilación .NET 8, `dotnet build`, `dotnet test`, build imagen Docker | GitLab CI | Automatizar compilación y tests en cada push a `main` | 🔵 |
| **CodeArts Deploy** | CD: despliegue automático a ECS o CCE. Rolling, Blue/Green, Canary | GitLab CD | Despliegue automático tras cada build exitoso | 🔵 |
| **CodeArts Pipeline** | Orquestación completa: Build → Test → Scan → Deploy → Notify | GitLab Pipeline | Pipeline de entrega continua completo | 🔵 |
| **SWR** (Software Repository for Container) | Registro privado de imágenes Docker | ECR | Almacenar imágenes Docker para deploys reproducibles | 🔵 |

---

### 3.8 Inteligencia Artificial y ML

> **Política**: La arquitectura objetivo usa exclusivamente servicios de IA del ecosistema Huawei Cloud. El estado actual (Gemini) es transitorio y se migrará en Fase 5.

| Servicio Huawei | Descripción | Uso en MPM | Rec. |
|---|---|---|---|
| **ModelArts / MaaS** | Plataforma Huawei Cloud para acceso API a modelos LLM: Pangu, DeepSeek R1, GLM-4 y otros del catálogo | **Servicio de IA objetivo.** Reemplaza Google Gemini. Mismo propósito: análisis de PDFs de licitaciones, extracción de criterios, puntuaciones y chat Q&A | ✅ |
| **Pangu** (vía MaaS) | LLM propio de Huawei. Razonamiento estructurado, generación de JSON, análisis contextual | Análisis primario de documentos de evaluación (PDFs de licitaciones) | ✅ |
| **DeepSeek R1** (vía MaaS) | Modelo de razonamiento de alto rendimiento disponible en el catálogo MaaS | Alternativa a Pangu para bases de licitación complejas (>40 páginas, múltiples criterios) | 🔵 |
| **GLM-4 / Kimi** (vía MaaS) | Modelos LLM adicionales en el catálogo MaaS | Contingencia ante indisponibilidad de Pangu o DeepSeek. **Esta es la contingencia correcta para IA generativa**, no OCR. | 🔵 |
| **OCR** (Optical Character Recognition) | Extracción de texto desde imágenes y PDFs **escaneados sin capa de texto digital**. No realiza razonamiento ni análisis. | Preprocesamiento de PDFs escaneados antes de enviar el texto al modelo LLM. Es un paso previo al LLM, no un reemplazo. | 🔵 |

> **Aclaración OCR vs LLM**: OCR y los modelos de lenguaje cumplen funciones completamente distintas e incompatibles entre sí. OCR extrae texto de imágenes o documentos sin capa de texto digital. Un LLM (Pangu, DeepSeek, Gemini) realiza razonamiento, clasificación, análisis contextual y generación de contenido sobre texto ya estructurado. OCR es un **preprocesador**: convierte imágenes en texto legible para que luego el LLM pueda analizarlo. Si Pangu no está disponible, la contingencia son otros LLMs (DeepSeek R1, GLM-4, Kimi), nunca OCR.

---

### 3.9 Mensajería e Integración

| Servicio Huawei | Descripción | Equivalente | Uso en MPM | Rec. |
|---|---|---|---|---|
| **DMS for Kafka** | Apache Kafka gestionado. Cola durable de alta throughput | MSK | Comunicación asíncrona entre módulos en fases futuras | 🔵 |
| **DMS for RabbitMQ** | RabbitMQ gestionado con colas y routing | Amazon MQ | Cola de tareas para desacoplar el scraper del análisis IA | 🔵 |
| **APIG** (API Gateway) | Gateway con autenticación, throttling y observabilidad | API Gateway | Exposición controlada de la API para integraciones ERP (Fase 18) | 🔵 |
| **EventGrid** | Enrutamiento de eventos entre servicios Huawei Cloud | EventBridge | Disparar análisis IA automáticamente cuando el scraper sube un PDF nuevo a OBS | 🔵 |

---

## 4. Mapa de Adopción Recomendado

### Nivel 1 — Contratar en Fase 5 (primer deploy a producción) ✅

Servicios cloud + herramientas on-premise necesarios para que el sistema opere en producción de forma segura y observable.

#### Servicios Huawei Cloud a contratar

| Servicio | Propósito | Costo estimado |
|---|---|---|
| **OBS** — bucket `mpm-licitaciones-prod` | PDFs de licitaciones (reemplaza `/app/uploads`) | ~USD 2–5/mes |
| **OBS** — bucket `mpm-backups-bd` | Dumps de backup PostgreSQL | ~USD 1–3/mes |
| **OBS Archive** (ciclo de vida) | PDFs >12 meses a almacenamiento frío (-60%) | Reducción sobre OBS |
| **IAM** | Control de acceso a recursos Huawei por servicio | Incluido |
| **CSMS** | `JWT_SECRET`, `PANGU_API_KEY`, `MP_TICKET`, `MP_PASSWORD`, `DB_PASSWORD` | ~USD 1–2/mes |
| **KMS** | Cifrado del bucket OBS en reposo | ~USD 1–2/mes |
| **DNS** | Zona DNS del dominio — apunta a IP del ISP de TIVIT | ~USD 1/mes |
| **SSL Certificate Manager** | Certificado TLS HTTPS | Gratuito (DV) |
| **WAF** (cloud-mode básico) | Protección SQLi, XSS, brute force sobre la API pública | ~USD 50–200/mes* |
| **LTS** | Logs de API, scraper y BD centralizados (requiere ICAgent en servidor) | ~USD 3–5/mes |
| **SMN** | Alertas por email ante eventos críticos | ~USD 1/mes |
| **CTS** | Auditoría de cambios sobre recursos Huawei | Incluido |
| **DAS** | Consola web de administración de BD sin cliente instalado | ~USD 2/mes |
| **Huawei MaaS** (Pangu / DeepSeek) | Análisis IA de PDFs — reemplaza Google Gemini | Variable por tokens |

**Total servicios Huawei Cloud Nivel 1: USD 65–225/mes** (excluye MaaS, variable según uso)  
*El rango amplio de WAF refleja variabilidad según plan y volumen. Validar en consola antes de contratar.

#### Stack open source on-premise (parte de Fase 5, costo cero)

| Herramienta | Propósito | Costo |
|---|---|---|
| Prometheus + Alertmanager | Métricas e alertas del servidor y contenedores | USD 0 |
| Grafana | Dashboards de observabilidad | USD 0 |
| node_exporter, cAdvisor, postgres_exporter, redis_exporter, nginx_exporter | Fuentes de métricas | USD 0 |

---

### Nivel 2 — Incorporar cuando el cómputo migre a nube 🔵

| Servicio | Condición de adopción | Propósito |
|---|---|---|
| **ECS** | Al decidir migrar el servidor on-premise a nube | VM en Huawei Cloud reemplazando el servidor físico de TIVIT |
| **VPC** | Al desplegar ECS o RDS en nube | Red privada para aislar los recursos cloud |
| **EIP** | Al desplegar ECS o ELB | IP pública estática para recursos en Huawei Cloud |
| **NAT Gateway** | Junto con ECS en subred privada | Salida a internet desde ECS sin IP pública directa |
| **VPN Gateway** | Cuando haya recursos tanto on-premise como en nube | Conexión privada TIVIT ↔ Huawei Cloud |
| **RDS PostgreSQL** | Al migrar BD a nube | BD gestionada con HA y backups automáticos |
| **DCS Redis** | Junto con RDS | Redis gestionado con HA para el backplane SignalR |
| **CBR** | Cuando haya ECS/RDS/EVS en nube | Backup automático de recursos dentro de Huawei Cloud |
| **EVS** | Junto con ECS | Volumen de disco persistente para PostgreSQL en nube |
| **ELB** | Si se despliegan múltiples instancias de la API | Distribución de carga con TLS gestionado |
| **Anti-DDoS** | Cuando haya recursos Huawei Cloud con IP pública | Protección volumétrica sobre el ELB o NAT |
| **CDN** | Cuando haya usuarios en distintas ciudades de Chile | Frontend React más rápido desde edge nodes |
| **CES + AOM** | Cuando el cómputo esté en ECS/RDS/DCS | Métricas nativas de recursos en Huawei Cloud |
| **APM** | Fase 11+ (Inteligencia Competitiva) | Trazabilidad por request, detección de endpoints lentos |
| **CodeArts Pipeline + SWR** | Al reemplazar scripts de deploy manual | CI/CD automático push → build → deploy |
| **EventGrid + OBS triggers** | Fase 8 (Análisis de Bases) | Disparar análisis IA al subir PDF a OBS |
| **APIG** | Fase 18 (Integración ERP) | Gateway con autenticación para integraciones externas |
| **DMS Kafka** | Cuando los módulos requieran comunicación asíncrona | Desacoplar el scraper del análisis IA |
| **OCR** | Fase 8 — PDFs escaneados sin texto digital | Preprocesar imágenes antes de enviar texto al LLM |
| **HSS** | Cuando el sistema esté 3+ meses en producción | Detección de intrusos en el servidor |
| **DeepSeek R1 / GLM-4** | Si Pangu no cumple los requisitos de calidad | LLMs alternativos en catálogo Huawei MaaS |

---

### Nivel 3 — Opcional / Long-term ⚪

| Servicio | Condición |
|---|---|
| **CCE** (Kubernetes) | Solo si hay múltiples equipos con servicios independientes |
| **GaussDB** | Solo si PostgreSQL supera los 5 TB |
| **Direct Connect** | Solo si la latencia on-premise ↔ OBS es un problema medible y cuantificado |
| **DWS** (Data Warehouse) | Si el volumen de análisis supera los 50,000 registros históricos |

---

## 5. Estrategia de Backup On-Premise

PostgreSQL corre como contenedor Docker en el servidor on-premise. **CBR no puede realizar backups de bajo nivel sobre esta infraestructura.** La estrategia usa herramientas nativas de PostgreSQL con almacenamiento en Huawei OBS.

### Flujo de backup

```
┌──────────────────────────────────────────────────┐
│           SERVIDOR ON-PREMISE (cron 02:00)        │
│                                                   │
│  1. docker exec mpm-db pg_dump → dump.sql         │
│  2. gzip → dump_YYYY-MM-DD.sql.gz                 │
│  3. obsutil cp → OBS bucket mpm-backups-bd        │
│  4. Eliminar dump local                           │
│  5. SMN → email equipo (éxito o error)            │
└──────────────────────────────────────────────────┘
                      ↓
┌──────────────────────────────────────────────────┐
│             HUAWEI OBS — mpm-backups-bd           │
│                                                   │
│  backups/2026-06-25_backup.sql.gz   ← Standard   │
│  backups/2026-06-24_backup.sql.gz   ← Standard   │
│  ...                                              │
│  [Día 31+] → transición automática a Archive      │
│  [Día 366+] → eliminación automática              │
└──────────────────────────────────────────────────┘
```

### Script: `scripts/backup-db.sh`

```bash
#!/bin/bash
set -euo pipefail
DATE=$(date +%Y-%m-%d)
DUMP_FILE="/tmp/mpm_backup_${DATE}.sql.gz"

# pg_dump dentro del contenedor + compresión
docker exec mpm-db pg_dump -U "${DB_USER}" "${DB_NAME}" | gzip > "${DUMP_FILE}"

# Upload a Huawei OBS via obsutil
obsutil cp "${DUMP_FILE}" "obs://mpm-backups-bd/backups/${DATE}_backup.sql.gz" \
  -e "${STORAGE_ENDPOINT}" -ak "${STORAGE_ACCESS_KEY}" -sk "${STORAGE_SECRET_KEY}"

rm -f "${DUMP_FILE}"
echo "[OK] Backup ${DATE} completado"
```

### Script: `scripts/restore-db.sh`

```bash
#!/bin/bash
set -euo pipefail
BACKUP_DATE="${1:-$(date +%Y-%m-%d)}"
RESTORE_FILE="/tmp/restore_${BACKUP_DATE}.sql.gz"

# Descargar desde OBS
obsutil cp "obs://mpm-backups-bd/backups/${BACKUP_DATE}_backup.sql.gz" "${RESTORE_FILE}" \
  -e "${STORAGE_ENDPOINT}" -ak "${STORAGE_ACCESS_KEY}" -sk "${STORAGE_SECRET_KEY}"

# Restaurar en el contenedor PostgreSQL
gunzip -c "${RESTORE_FILE}" | docker exec -i mpm-db psql -U "${DB_USER}" "${DB_NAME}"

rm -f "${RESTORE_FILE}"
echo "[OK] Restauración desde ${BACKUP_DATE} completada"
```

### Política de retención OBS

| Período | Clase OBS | Costo relativo |
|---|---|---|
| Días 1–30 | Standard | 100% |
| Días 31–365 | Archive (ciclo de vida automático) | ~40% |
| Más de 365 días | Eliminación automática | — |

---

## 6. Estrategia de IA — Migración a Huawei MaaS

### Estado actual vs objetivo

| | Estado actual | Objetivo Fase 5 |
|---|---|---|
| Clase | `GeminiService.cs` | `PanguService.cs` (nueva) |
| Endpoint | `generativelanguage.googleapis.com` | `maas.<region>.myhuaweicloud.com` |
| Variable de entorno | `GEMINI_API_KEY` | `PANGU_API_KEY` (o `AI__ApiKey`) |
| Modelo | `gemini-2.5-pro` | Pangu / DeepSeek R1 |
| Interfaz .NET | Sin interfaz (`HttpClient` directo) | `IAnalisisIAService` (nueva) |

### Flujo de llamada objetivo

```
API MPM (.NET 8)
└── AnalisisBackgroundService
    └── AnalisisService
        └── IAnalisisIAService
            └── PanguService (nueva implementación)
                └── POST https://maas.<region>.myhuaweicloud.com/v1/infers/<model-id>
                    ├── Autenticación: CSMS → AI__ApiKey
                    ├── Input: texto extraído del PDF
                    └── Output: JSON con criterios, puntuaciones, resumen ejecutivo
```

### Modelos disponibles en Huawei MaaS

| Modelo | Fortaleza | Uso recomendado |
|---|---|---|
| **Pangu** | Razonamiento estructurado, generación JSON estricta | Análisis primario de PDFs de licitaciones |
| **DeepSeek R1** | Razonamiento de alta calidad, maneja documentos largos | Bases de licitación complejas (>40 páginas) |
| **GLM-4 / Kimi** | Velocidad y costo | Clasificación rápida y resúmenes ejecutivos |

### Plan de migración técnica (Fase 5)

1. Crear interfaz `IAnalisisIAService` en `MPM.Modules.Analisis/Services/`
2. Refactorizar `GeminiService` para implementar `IAnalisisIAService`
3. Crear `PanguService : IAnalisisIAService` con llamada a Huawei MaaS
4. En `Program.cs` agregar switch: `AI__Provider = "pangu"` → `PanguService`, `"gemini"` → `GeminiService`
5. Reemplazar `GEMINI_API_KEY` por `PANGU_API_KEY` en CSMS
6. El resto del sistema (controladores, background service, frontend) no requiere cambios

---

## 7. Arquitectura Híbrida — Vista Completa

```
┌──────────────────────────────────────────────────────────────────┐
│                    SERVIDOR ON-PREMISE TIVIT                      │
│                    IP estática del ISP de TIVIT                   │
│                                                                   │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │
│  │  API     │  │  Web     │  │PostgreSQL│  │  Redis   │        │
│  │ .NET 8   │  │  nginx   │  │   16     │  │   7      │        │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘        │
│                                                                   │
│  ┌──────────────┐  ┌──────────┐  ┌───────────────────────┐     │
│  │  Prometheus  │  │ Grafana  │  │ Exporters: node, pg,  │     │
│  │  Alertmanager│  │          │  │ redis, cadvisor, nginx │     │
│  └──────────────┘  └──────────┘  └───────────────────────┘     │
│                   Docker Compose (prod)                           │
└──────────────────────────┬───────────────────────────────────────┘
                           │ Salida a internet (ISP TIVIT)
           ┌───────────────┼──────────────────────────────────┐
           │               │                                  │
           ▼               ▼                                  ▼
┌──────────────────┐  ┌─────────────────────────┐  ┌──────────────────┐
│ mercadopublico   │  │     HUAWEI CLOUD         │  │  Huawei MaaS     │
│ .cl (API pública)│  │                         │  │  (objetivo F5)   │
│                  │  │  OBS ─── PDFs licitac.  │  │  Pangu LLM       │
│ Scraper → REST + │  │  OBS ─── Dumps BD       │  │  DeepSeek R1     │
│ Playwright       │  │  CSMS ── Secretos       │  │  GLM-4 / Kimi    │
└──────────────────┘  │  KMS ─── Cifrado OBS    │  └──────────────────┘
                       │  WAF ─── API pública    │
                       │  LTS ─── Logs central.  │
                       │  SMN ─── Alertas email  │
                       │  DNS ─── Dominio MPM    │
                       │  CTS ─── Auditoría      │
                       │  DAS ─── Admin BD web   │
                       └─────────────────────────┘

[ Estado actual: OBS y MaaS aún no conectados — se implementan en Fase 5 ]
```

---

## 8. Conectividad Saliente Requerida

### Estado actual del código

| Destino | Protocolo/Puerto | Servicio | Notas |
|---|---|---|---|
| `api.mercadopublico.cl` | HTTPS / 443 | Sync de licitaciones (REST API) | Diario |
| `www.mercadopublico.cl` | HTTPS / 443 | Scraper Playwright (web) | Diario |
| `generativelanguage.googleapis.com` | HTTPS / 443 | Gemini File API + inferencia | Por análisis |

### Objetivo Fase 5 (reemplaza y amplía la lista anterior)

| Destino | Protocolo/Puerto | Servicio | Obligatorio |
|---|---|---|---|
| `obs.<region>.myhuaweicloud.com` | HTTPS / 443 | OBS — PDFs y dumps de BD | Sí |
| `csms.<region>.myhuaweicloud.com` | HTTPS / 443 | CSMS — lectura de secretos al arrancar | Sí |
| `kms.<region>.myhuaweicloud.com` | HTTPS / 443 | KMS — validación de claves de cifrado | Sí |
| `lts.<region>.myhuaweicloud.com` | HTTPS / 443 | LTS — envío de logs centralizados | Sí |
| `smn.<region>.myhuaweicloud.com` | HTTPS / 443 | SMN — envío de alertas | Sí |
| `maas.<region>.myhuaweicloud.com` | HTTPS / 443 | MaaS — Pangu / DeepSeek inferencia | Sí |
| `iam.myhuaweicloud.com` | HTTPS / 443 | IAM — autenticación de credenciales cloud | Sí |
| `dns.myhuaweicloud.com` | HTTPS / 443 | DNS — gestión de zona | Sí |
| `api.mercadopublico.cl` | HTTPS / 443 | Sync de licitaciones | Sí |
| `www.mercadopublico.cl` | HTTPS / 443 | Scraper Playwright | Sí |
| NTP server (ntp.shoa.cl u otro) | UDP / 123 | Sincronización de tiempo del servidor | Sí |
| `*.letsencrypt.org` | HTTPS / 443 | Renovación de certificado TLS | Sí |

> `<region>` corresponde a la región Huawei Cloud seleccionada. Preferir `la-south-2` (Santiago de Chile) para menor latencia desde el datacenter TIVIT. Si algún servicio no está disponible en esa región, usar `la-north-2` (México) como alternativa.

> **Acción requerida**: Esta tabla debe entregarse al equipo de redes de TIVIT para configurar las reglas de firewall del servidor antes del deploy a producción.

---

## 9. Recursos On-Premise — Especificaciones

### Desglose de consumo por servicio Docker (Fase 5, con stack de monitoreo)

| Servicio | vCPU | RAM | Disco |
|---|---|---|---|
| API .NET 8 (+ scraper Node.js interno) | 1–2 vCPU | 2–4 GB | ~2 GB |
| Frontend nginx | < 0.5 vCPU | 256 MB | ~500 MB |
| PostgreSQL 16 | 1–2 vCPU | 4–8 GB | 200–500 GB |
| Redis 7 | < 0.5 vCPU | 512 MB–1 GB | < 1 GB |
| Prometheus + Alertmanager | 0.3 vCPU | 512 MB | ~10 GB (retención 7d) |
| Grafana | 0.2 vCPU | 256 MB | < 1 GB |
| Exporters (5 contenedores) | 0.2 vCPU total | 256 MB total | < 1 GB |
| SO + Docker overhead | 0.5 vCPU | 1–2 GB | 20–30 GB |
| **Total** | **~5–6 vCPU** | **~9–16 GB** | **~235–545 GB** |

### Especificaciones del servidor

| Recurso | Mínimo | Recomendado |
|---|---|---|
| **vCPU** | 6 vCPU | 8 vCPU |
| **RAM** | 12 GB | 16 GB |
| **Disco** | 300 GB | 600 GB |
| **SO** | Ubuntu 22.04 LTS o RHEL 8+ | — |
| **Internet (salida)** | 20 Mbps dedicados | 50 Mbps |

> **Nota sobre IP pública**: La IP pública del servidor es proporcionada por el ISP del datacenter TIVIT. Es independiente de los servicios Huawei Cloud. Solicitar IP estática al ISP para evitar cambios de DNS. Si el ISP solo provee IP dinámica, configurar DDNS como contingencia.

---

## 10. Variables de Entorno

### Variables actuales del sistema (del código fuente)

| Variable | Ejemplo / Default | Propósito | Criticidad |
|---|---|---|---|
| `DB_HOST` | `db` | PostgreSQL host | Crítica |
| `DB_PORT` | `5432` | PostgreSQL port | Crítica |
| `DB_USER` | `mpm` | Usuario de BD | Crítica |
| `DB_PASSWORD` | — | Contraseña de BD | Crítica |
| `DB_NAME` | `mpm` | Nombre de BD | Crítica |
| `REDIS_PASSWORD` | — | Contraseña Redis | Crítica |
| `JWT_SECRET` | — (40+ chars) | Clave de firma JWT | Crítica |
| `JWT_ISSUER` | `TIVIT.MPM` | Claim issuer del token | Alta |
| `JWT_AUDIENCE` | `MPM.Users` | Claim audience del token | Alta |
| `MP_TICKET` | — (UUID) | Ticket autenticación Mercado Público API | Crítica |
| `MP_RUT` | `73058136` | RUT para login scraper web | Crítica |
| `MP_PASSWORD` | — | Contraseña login scraper web | Crítica |
| `MP_HEADLESS` | `true` | Playwright sin interfaz visual | Media |
| `MP_DELAY_MS` | `2000` | Delay entre requests del scraper | Media |
| `MP_MAX_REINTENTOS` | `3` | Reintentos en scraper | Media |
| `MP_ANALISIS_IA` | `true` | Activar análisis IA al subir PDF | Media |
| `MP_FECHA_DESDE` | `01-01-2026` | Fecha de inicio del scraper | Media |
| `SCRAPER_ENABLED` | `true` | Habilitar scraper background | Media |
| `SCRAPER_INTERVAL_HOURS` | `12` | Intervalo de ejecución del scraper | Media |
| `API_BASE_URL` | `http://localhost:80` | URL base para llamadas internas del scraper | Media |
| `MONITOR_ENABLED` | `true` | Monitorear aclaraciones activas | Media |
| `MONITOR_INTERVAL_MINUTES` | `30` | Intervalo del monitor de aclaraciones | Media |
| `GEMINI_API_KEY` | — | Google Gemini API key (estado actual, transitorio) | Crítica |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Entorno de .NET | Alta |
| `Storage__Provider` | `local` | Proveedor de storage (`local` \| `gcs` \| `obs`) | Alta |
| `Storage__Bucket` | `tivit-cu010-mpm-adjuntos` | Bucket OBS (cuando `obs`) | Alta |

### Variables a agregar en Fase 5

| Variable nueva | Valor objetivo | Reemplaza / Acción |
|---|---|---|
| `Storage__Provider` | `obs` | Cambiar de `local` a `obs` |
| `Storage__Endpoint` | `https://obs.<region>.myhuaweicloud.com` | Nueva |
| `Storage__AccessKey` | `<desde CSMS>` | Nueva |
| `Storage__SecretKey` | `<desde CSMS>` | Nueva |
| `AI__Provider` | `pangu` | Nueva (controla qué LLM usar) |
| `AI__Endpoint` | `https://maas.<region>.myhuaweicloud.com/v1/infers/<model-id>` | Nueva |
| `AI__ApiKey` | `<desde CSMS>` | Reemplaza `GEMINI_API_KEY` |
| `GEMINI_API_KEY` | — | Eliminar en producción cuando migración sea estable |

> En producción, todas las variables marcadas como "Crítica" deben almacenarse en **Huawei CSMS** y cargarse al iniciar el contenedor, no en un archivo `.env` en disco. El script de entrypoint consulta CSMS y exporta las variables antes de iniciar el proceso .NET.

---

## 11. Consideraciones Adicionales

1. **WAF — validación de costos previa**: El costo de WAF varía significativamente según el plan (cloud-mode vs dedicado), número de dominios y volumen de tráfico. Siempre validar la estimación en la consola de Huawei Cloud contra métricas reales de tráfico antes de contratar. Un plan mal dimensionado puede generar costos 5x superiores a la estimación.

2. **OBS ciclo de vida — configurar desde el inicio**: La política de ciclo de vida (Standard → Archive a los 31 días) se configura en la consola OBS al crear el bucket. Es gratuita de configurar y genera ahorros del 60% en almacenamiento de PDFs históricos sin cambios en el código.

3. **ICAgent para LTS**: Para enviar logs desde el servidor on-premise a LTS se requiere instalar el agente ICAgent de Huawei en el sistema operativo Linux del servidor. Documentar el proceso de instalación y configuración en el runbook de producción.

4. **Prometheus retención**: Configurar retención a 7 días para no agotar el disco del servidor. Los datos históricos se cubren con LTS (logs) y no requieren métricas de largo plazo en on-premise.

5. **MP_TICKET e IP fija**: El ticket de autenticación de Mercado Público puede estar ligado a la IP de origen. La IP estática del ISP de TIVIT debe ser siempre la misma. Si el ISP cambia la IP, actualizar el ticket con Mercado Público.

6. **Secretos en arranque del contenedor**: La API .NET 8 usa `IConfiguration` con múltiples proveedores. En producción, el script de entrypoint del contenedor debe consultar CSMS vía `huaweicloud-sdk-dotnet` antes de iniciar el proceso, exportando las variables como variables de entorno del proceso. Esto evita tener un `.env` con credenciales en texto plano en el servidor.

7. **Región Huawei Cloud**: Preferir `la-south-2` (Santiago de Chile) para minimizar latencia desde el datacenter TIVIT. Si algún servicio requerido no está disponible en esa región, `la-north-2` (Ciudad de México) como alternativa regional.

8. **Gemini como estado transitorio**: El código actual usa `GeminiService` con Google Gemini 2.5 Pro. Este es un estado transitorio documentado. La migración a Huawei MaaS (Pangu/DeepSeek) se implementa en Fase 5 como parte del principio de exclusividad Huawei Cloud. Hasta que la migración sea estable en producción, `GEMINI_API_KEY` se mantiene en CSMS como variable de rollback.

---

*Documento preparado por el equipo de Digital — TIVIT. Versión 4.0.*  
*Basado en análisis del código fuente del repositorio y observaciones de gerencia.*  
*El documento v3 (`infraestructura-cu010.md`) se preserva como referencia histórica.*
