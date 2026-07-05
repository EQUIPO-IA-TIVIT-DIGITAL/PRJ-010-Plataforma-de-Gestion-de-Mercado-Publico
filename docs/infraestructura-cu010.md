# Recursos e Infraestructura Tecnológica — CU010 Mercado Público

**Proyecto:** MPM — Mercado Público Management  
**Área:** Digital — TIVIT  
**Fecha:** Junio 2026  
**Versión:** 3.0 — Análisis integral Huawei Cloud + On-Premise

---

## 1. Descripción General del Sistema

MPM es una plataforma web para gestión y análisis de licitaciones públicas chilenas provenientes de [mercadopublico.cl](https://www.mercadopublico.cl). El sistema está compuesto por:

| Componente | Tecnología |
|------------|-----------|
| Backend API | .NET 8 (ASP.NET Core) — monolito modular |
| Frontend | React 18 + TypeScript + Ant Design 5, servido por nginx |
| Base de datos | PostgreSQL 16 |
| Caché / Pub-Sub | Redis 7 (backplane para SignalR) |
| Mensajería en tiempo real | ASP.NET Core SignalR |
| Almacenamiento de archivos | Huawei OBS (Object Storage Service) |
| Análisis IA | Google Gemini 2.5 Pro (API externa) |
| Contenedores | Docker / Docker Compose |

---

## 2. Catálogo de Servicios Huawei Cloud — Mapa Completo

Análisis exhaustivo de todos los servicios Huawei Cloud relevantes para MPM, organizados por categoría. Cada servicio está evaluado en tres dimensiones:

- **Uso MPM**: para qué sirve específicamente en este proyecto
- **Equivalente**: servicio análogo en AWS / GCP
- **Recomendación**: `✅ Usar ahora` / `🔵 Fases futuras` / `⚪ Opcional`

---

### 2.1 Cómputo

| Servicio Huawei | Descripción | Equivalente AWS/GCP | Uso en MPM | Rec. |
|---|---|---|---|---|
| **ECS** (Elastic Cloud Server) | VM Linux/Windows con vCPU, RAM y disco configurables. Equivalente a una VM convencional | EC2 / GCE | Hospedar todos los contenedores Docker del sistema si se migra de on-premise a nube | 🔵 |
| **CCE** (Cloud Container Engine) | Kubernetes gestionado. Orquestación de contenedores con auto-scaling, rolling deploys y self-healing | EKS / GKE | Despliegue avanzado de API y Web cuando el volumen de usuarios justifique Kubernetes | ⚪ |
| **CCI** (Cloud Container Instance) | Contenedores serverless sin gestionar nodos. Pago por uso (CPU/RAM por segundo) | Fargate / Cloud Run | Ejecutar el scraper Node.js como tarea puntual sin mantener servidor encendido las 24h | 🔵 |
| **FunctionGraph** | Funciones serverless (FaaS). Ejecución por evento o por cron | Lambda / Cloud Functions | Ejecutar backups, tareas de mantenimiento, webhooks de notificación | 🔵 |

---

### 2.2 Base de Datos

| Servicio Huawei | Descripción | Equivalente AWS/GCP | Uso en MPM | Rec. |
|---|---|---|---|---|
| **RDS for PostgreSQL** | PostgreSQL gestionado con backups automáticos, parches, failover automático (modo HA) y réplicas de lectura | RDS / Cloud SQL | Reemplazar el PostgreSQL Docker on-premise. Alta disponibilidad sin gestión manual | 🔵 |
| **DCS** (Distributed Cache Service) | Redis y Memcached gestionados con modo maestro-réplica, Sentinel y Cluster. Backups automáticos | ElastiCache / Memorystore | Reemplazar el Redis Docker on-premise. HA para el backplane SignalR | 🔵 |
| **GaussDB** | Base de datos distribuida propia de Huawei. Compatible con PostgreSQL pero con capacidades de escalado horizontal | Aurora / Spanner | No aplicable en fase actual; considera si la BD supera los 5 TB | ⚪ |
| **DAS** (Data Admin Service) | Herramienta web para administrar bases de datos: ejecutar queries, ver estadísticas, gestionar usuarios | DBeaver Cloud | Interfaz web para consultas de BD sin necesitar cliente instalado | ✅ |

---

### 2.3 Almacenamiento

| Servicio Huawei | Descripción | Equivalente AWS/GCP | Uso en MPM | Rec. |
|---|---|---|---|---|
| **OBS** (Object Storage Service) | Almacenamiento de objetos escalable. Soporta ciclo de vida, versionado, pre-signed URLs, CORS | S3 / GCS | **Ya decidido.** PDFs de licitaciones, adjuntos y backups de BD. Buckets separados por tipo | ✅ |
| **EVS** (Elastic Volume Service) | Disco de bloque (SSD/HDD) que se adjunta a un ECS. Persistencia para contenedores Docker | EBS / Persistent Disk | Volumen persistente para PostgreSQL y logs si se usa ECS en nube | 🔵 |
| **CBR** (Cloud Backup and Recovery) | Backup automático de ECS, EVS y RDS. Políticas de retención, restore point-in-time, snapshot | AWS Backup / Cloud Backup | Backup automatizado y gestionado de BD y servidores. Más robusto que pg_dump manual | ✅ |
| **SFS** (Scalable File Service) | Sistema de archivos NFS compartido montable en múltiples servidores simultáneamente | EFS / Filestore | Compartir archivos temporales entre múltiples instancias del scraper si escala horizontalmente | ⚪ |
| **OBS Archive** (clase de almacenamiento) | Tier frío dentro de OBS para archivos con acceso infrecuente. Hasta 60% más barato que Standard | Glacier / Archive | PDFs de licitaciones con más de 12 meses pueden moverse automáticamente a Archive | ✅ |

---

### 2.4 Redes y Conectividad

| Servicio Huawei | Descripción | Equivalente AWS/GCP | Uso en MPM | Rec. |
|---|---|---|---|---|
| **VPC** (Virtual Private Cloud) | Red privada aislada en la nube. Control de subredes, tablas de rutas, ACLs | VPC / VPC | Contenedor de red para todos los recursos Huawei Cloud del proyecto | ✅ |
| **EIP** (Elastic IP Address) | IP pública estática asignable a ECS, NAT Gateway o ELB. Permanece aunque se reinicie el servidor | EIP / External IP | IP fija para el servidor on-premise (si usa IP dinámica del ISP) o para el ECS en nube | ✅ |
| **ELB** (Elastic Load Balance) | Balanceador de carga HTTP/HTTPS/TCP con terminación TLS, health checks y certificados gestionados | ALB/NLB / Cloud LB | Distribuir tráfico entre múltiples instancias de la API si se escala horizontalmente | 🔵 |
| **NAT Gateway** | Permite salida a internet desde instancias privadas sin IP pública propia | NAT Gateway / Cloud NAT | Salida a internet desde ECS en subred privada (para llamar a Gemini API y OBS) | 🔵 |
| **DNS** (Domain Name Service) | Resolución de DNS pública y privada. Gestión de zonas, registros A, CNAME, MX | Route53 / Cloud DNS | Apuntar el dominio de MPM a la IP del servidor. Failover DNS si hay dos servidores | ✅ |
| **CDN** (Content Delivery Network) | Distribución de contenido estático desde edge locations cercanas al usuario | CloudFront / Cloud CDN | Servir el bundle React (JS/CSS/imágenes) desde nodo CDN en Chile para menor latencia | 🔵 |
| **Direct Connect** | Conexión dedicada de fibra entre datacenter de TIVIT y Huawei Cloud. Baja latencia garantizada | Direct Connect / Cloud Interconnect | Si el servidor on-premise transfiere grandes volúmenes a OBS y la latencia de internet es inaceptable | ⚪ |
| **VPN Gateway** | Túnel VPN IPsec entre on-premise y Huawei Cloud | AWS VPN / Cloud VPN | Conexión privada y segura entre el servidor on-premise y los recursos Huawei Cloud | 🔵 |

---

### 2.5 Seguridad

| Servicio Huawei | Descripción | Equivalente AWS/GCP | Uso en MPM | Rec. |
|---|---|---|---|---|
| **IAM** (Identity and Access Management) | Control de acceso a recursos Huawei Cloud. Usuarios, roles, políticas, MFA | IAM / Cloud IAM | Gestionar quién puede acceder a OBS, CBR y otros recursos. Credenciales por servicio | ✅ |
| **CSMS** (Cloud Secret Management Service) | Almacén de secretos gestionado. Variables sensibles sin .env en el servidor | Secrets Manager / Secret Manager | Almacenar `JWT_SECRET`, `GEMINI_API_KEY`, `MP_TICKET`, `DB_PASSWORD` fuera del sistema de archivos | ✅ |
| **KMS** (Key Management Service) | Claves de cifrado gestionadas para cifrar datos en OBS, RDS y EVS en reposo | KMS / Cloud KMS | Cifrado de buckets OBS que contienen PDFs de licitaciones | ✅ |
| **WAF** (Web Application Firewall) | Firewall de aplicación web. Bloquea SQLi, XSS, ataques de fuerza bruta, bots maliciosos | WAF / Cloud Armor | Proteger el endpoint `/api/v1/auth/login` y el API en general ante ataques web | ✅ |
| **Anti-DDoS** | Protección automática contra ataques de denegación de servicio volumétricos | Shield / Cloud Armor | Protección básica incluida para EIP. Activar para la IP pública del sistema | ✅ |
| **HSS** (Host Security Service) | Detección de intrusos, análisis de vulnerabilidades y gestión de parches en el servidor | GuardDuty / Security Command Center | Monitoreo de seguridad del servidor on-premise o ECS en nube | 🔵 |
| **SSL Certificate Manager** | Provisión y renovación automática de certificados TLS desde autoridades reconocidas | ACM / Certificate Manager | Alternativa a Let's Encrypt para el certificado HTTPS del sistema | ✅ |
| **VSS** (Vulnerability Scan Service) | Escaneo automático de vulnerabilidades en aplicaciones web y servidores | Inspector / Security Scanner | Escanear el endpoint público del sistema en cada deploy | 🔵 |
| **CTS** (Cloud Trace Service) | Auditoría de todas las operaciones sobre recursos Huawei Cloud. Log inmutable | CloudTrail / Cloud Audit | Registro de quién accedió a OBS, cambió secretos o modificó configuración | ✅ |

---

### 2.6 Monitoreo y Observabilidad

| Servicio Huawei | Descripción | Equivalente AWS/GCP | Uso en MPM | Rec. |
|---|---|---|---|---|
| **CES** (Cloud Eye Service) | Monitoreo de métricas de infraestructura: CPU, RAM, disco, red. Alertas por umbral | CloudWatch / Cloud Monitoring | Alertas cuando el servidor supere 80% de CPU o disco | ✅ |
| **AOM** (Application Operations Management) | Monitoreo de aplicaciones y contenedores Docker. Métricas de JVM, .NET runtime, contenedores | Container Insights / Cloud Ops | Visibilidad de consumo de recursos por contenedor Docker en el servidor | ✅ |
| **LTS** (Log Tank Service) | Centralización de logs de aplicaciones, contenedores y sistemas. Búsqueda y alertas sobre logs | CloudWatch Logs / Cloud Logging | Centralizar logs de la API .NET 8, el scraper Node.js y PostgreSQL en un solo lugar | ✅ |
| **APM** (Application Performance Management) | Trazabilidad distribuida, tiempos de respuesta por endpoint, detección de cuellos de botella | X-Ray / Cloud Trace | Identificar endpoints lentos de la API y queries PostgreSQL que degradan el rendimiento | 🔵 |
| **SMN** (Simple Message Notification) | Envío de notificaciones a email, SMS, webhook y otros servicios cuando ocurre un evento | SNS / Pub/Sub | Enviar alerta al equipo cuando el sistema cae o el disco supera el 90% de uso | ✅ |

---

### 2.7 DevOps y CI/CD (CodeArts)

Huawei CodeArts es la suite DevOps integrada de Huawei Cloud. Equivalente a GitLab en un solo producto.

| Servicio Huawei | Descripción | Equivalente | Uso en MPM | Rec. |
|---|---|---|---|---|
| **CodeArts Repo** (CodeHub) | Repositorio Git en la nube con merge requests, code review, rama protegida | GitLab / GitHub | Hospedar el código fuente de MPM en Huawei Cloud si se migra desde el repo actual | 🔵 |
| **CodeArts Build** | CI: compilación de .NET 8, `dotnet build`, `dotnet test`, build de imagen Docker | GitLab CI / Cloud Build | Automatizar la compilación y tests en cada push a `main` | 🔵 |
| **CodeArts Deploy** | CD: despliegue automático a ECS o CCE. Rolling deploy, Blue/Green, Canary | GitLab CD / Cloud Deploy | Desplegar automáticamente la nueva versión de la API y Web tras cada build exitoso | 🔵 |
| **CodeArts Pipeline** | Orquestación del pipeline completo: Build → Test → Scan → Deploy → Notify | GitLab Pipeline / Cloud Build Pipelines | Pipeline completo: push → build → scan de seguridad → deploy → notificación | 🔵 |
| **CodeArts Artifact** | Registro privado de imágenes Docker (Container Registry) y paquetes NuGet | ECR / Artifact Registry | Almacenar las imágenes Docker de la API y Web para deploy reproducible | 🔵 |
| **SWR** (Software Repository for Container) | Registro de imágenes Docker independiente de CodeArts | ECR / Container Registry | Alternativa más simple a CodeArts Artifact solo para imágenes Docker | 🔵 |

---

### 2.8 Inteligencia Artificial y ML

| Servicio Huawei | Descripción | Equivalente AWS/GCP | Uso en MPM | Rec. |
|---|---|---|---|---|
| **ModelArts** | Plataforma de desarrollo y despliegue de modelos de ML e IA. Incluye acceso a Pangu (LLM propio de Huawei) | SageMaker / Vertex AI | Alternativa a Gemini si el costo de tokens Gemini escala o si se requiere modelo on-premise | ⚪ |
| **OCR** (Optical Character Recognition) | Extracción de texto de imágenes y PDFs escaneados. Soporte multiidioma incluyendo español | Textract / Document AI | Preprocesar PDFs de licitaciones escaneados (sin capa de texto) antes de enviar a Gemini | 🔵 |
| **NLP** (Natural Language Processing) | Análisis de sentimiento, extracción de entidades, clasificación de texto en español | Comprehend / Natural Language API | Clasificar automáticamente las licitaciones por categoría o rubro sin requerir Gemini | ⚪ |
| **Pangu** (via ModelArts) | LLM propio de Huawei entrenado en datos empresariales. Responde en chino y parcialmente en inglés/español | Bedrock / Vertex AI Gemini | Alternativa de contingencia a Gemini 2.5. **Limitación: menor capacidad en español que Gemini** | ⚪ |

---

### 2.9 Mensajería e Integración

| Servicio Huawei | Descripción | Equivalente AWS/GCP | Uso en MPM | Rec. |
|---|---|---|---|---|
| **DMS for Kafka** | Apache Kafka gestionado. Cola de mensajes durable, alta throughput, grupos de consumidores | MSK / Pub/Sub | Comunicación asíncrona entre módulos en fases futuras (ej: pipeline de análisis) | 🔵 |
| **DMS for RabbitMQ** | RabbitMQ gestionado con colas, exchanges y routing | Amazon MQ / — | Cola de tareas para el scraper o el análisis Gemini si se desacoplan del flujo síncrono | 🔵 |
| **APIG** (API Gateway) | Gateway para APIs REST con autenticación, throttling, transformación y observabilidad | API Gateway / API Gateway | Exposición controlada de la API con rate limiting y API keys para integraciones externas (Fase ERP) | 🔵 |
| **EventGrid** | Enrutamiento de eventos entre servicios Huawei Cloud. Modelo publisher/subscriber | EventBridge / Eventarc | Disparar análisis automáticamente cuando el scraper sube un nuevo PDF a OBS | 🔵 |

---

## 3. Mapa de Adopción Recomendado

Organiza los servicios Huawei en tres niveles según el momento de adopción:

### Nivel 1 — Usar desde Fase 5 (deploy inicial) ✅

Estos servicios tienen el mayor valor inmediato y bajo costo de implementación:

| Servicio | Propósito en MPM | Costo estimado |
|---|---|---|
| **OBS** (2 buckets: archivos + backups) | PDFs y backups de BD | ~USD 2–5/mes |
| **IAM** (usuarios y roles) | Control de acceso a recursos Huawei | Incluido |
| **CSMS** (secretos) | JWT_SECRET, GEMINI_API_KEY, DB_PASSWORD fuera del .env | ~USD 1/mes |
| **KMS** (cifrado OBS) | PDFs cifrados en reposo | ~USD 1/mes |
| **EIP** (IP estática) | IP fija para el servidor on-premise | ~USD 5/mes |
| **DNS** | Gestión de zona DNS del dominio | ~USD 1/mes |
| **SSL Certificate Manager** | Certificado TLS para HTTPS | Gratuito (DV) |
| **WAF** (modo básico) | Protección de la API pública | ~USD 15/mes |
| **Anti-DDoS** | Protección volumétrica básica | Incluido con EIP |
| **CES** (Cloud Eye) | Alertas de CPU/disco/RAM del servidor | ~USD 2/mes |
| **LTS** (Log Tank) | Logs centralizados de API y scraper | ~USD 3/mes |
| **SMN** | Alerta por email/SMS si el sistema cae | ~USD 1/mes |
| **CTS** (auditoría) | Log de cambios en recursos Huawei | Incluido |
| **CBR** (backup BD) | Snapshots automáticos del volumen de datos | ~USD 5/mes |
| **DAS** (admin BD) | Consola web para consultas sin cliente instalado | ~USD 2/mes |
| **OBS Archive** (ciclo de vida) | PDFs >12 meses a almacenamiento frío | Reducción 60% |

**Total Nivel 1 estimado: USD 40–60/mes**

---

### Nivel 2 — Incorporar en fases futuras 🔵

Cuando el sistema crece o se requiere mayor resiliencia:

| Servicio | Cuándo incorporar | Propósito |
|---|---|---|
| **RDS PostgreSQL** | Si el servidor on-premise falla frecuentemente o crece >5 usuarios concurrentes | BD gestionada con HA y backups automáticos |
| **DCS Redis** | Junto con RDS — si se migra la BD a nube | Redis gestionado para SignalR HA |
| **ECS** | Si se abandona el servidor on-premise | VM en nube que reemplaza el servidor físico |
| **CCI / FunctionGraph** | Fase 9 (Reportes) o cuando el scraper necesite escalar | Ejecutar scraper y generación de reportes sin servidor dedicado |
| **CDN** | Cuando haya usuarios en distintas ciudades de Chile | Servir el frontend React más rápido |
| **VPN Gateway** | Si los recursos Huawei se expanden a RDS/ECS | Conexión privada on-premise ↔ Huawei Cloud |
| **ELB** | Si se agregan instancias del API (Fase Pipeline o CCE) | Distribución de carga con TLS gestionado |
| **AOM** | Cuando haya más de 3 contenedores que monitorear | Métricas por contenedor Docker |
| **APM** | Fase 11+ cuando se requiere trazabilidad por request | Detectar endpoints lentos y queries problemáticas |
| **CodeArts Pipeline** | Cuando el deploy manual se vuelva un cuello de botella | CI/CD automático push → build → deploy |
| **SWR** (Container Registry) | Junto con CodeArts Pipeline | Imágenes Docker versionadas |
| **EventGrid + OBS triggers** | Fase 8 (Análisis de Bases) | Disparar análisis automáticamente al subir PDF a OBS |
| **APIG** | Fase 18 (Integración ERP) | Gateway con autenticación para integraciones externas |
| **DMS Kafka** | Cuando los módulos requieran comunicación asíncrona | Desacoplar el scraper del análisis Gemini |
| **OCR** | Fase 8 — PDFs escaneados sin capa de texto | Preprocesar PDFs antes de enviar a Gemini |
| **HSS** | Cuando el sistema esté en producción +3 meses | Detección de intrusos en el servidor |
| **VSS** | Antes de cada release importante | Escaneo de vulnerabilidades del endpoint público |

---

### Nivel 3 — Opcional / Long-term ⚪

| Servicio | Condición |
|---|---|
| **CCE** (Kubernetes) | Solo si hay múltiples equipos desplegando servicios independientes |
| **GaussDB** | Solo si PostgreSQL supera 5 TB o se requiere sharding |
| **Direct Connect** | Solo si la latencia on-premise ↔ OBS es problema medible |
| **ModelArts / Pangu** | Si el costo de Gemini se vuelve prohibitivo o se requiere modelo privado |
| **DWS** (Data Warehouse) | Fase Pricing Intelligence si el volumen de análisis supera 10k registros |
| **NLP** | Si se requiere clasificación automática de licitaciones a gran escala |

---

## 4. Arquitectura Híbrida — Vista Completa

```
┌─────────────────────────────────────────────────────────────────┐
│                    SERVIDOR ON-PREMISE TIVIT                    │
│                   (vCPU + RAM + Disco)                          │
│                                                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐      │
│  │  API     │  │  Web     │  │PostgreSQL│  │  Redis   │      │
│  │ .NET 8   │  │  nginx   │  │   16     │  │   7      │      │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘      │
│                   Docker Compose                                │
└───────────────────────┬─────────────────────────────────────────┘
                        │ Internet (20 Mbps+)
        ┌───────────────┼───────────────────────────┐
        │               │                           │
        ▼               ▼                           ▼
┌──────────────┐  ┌───────────────────────┐  ┌──────────────┐
│ Google Cloud │  │    HUAWEI CLOUD       │  │  Mercado     │
│              │  │                       │  │  Público     │
│  Gemini API  │  │  OBS ──── PDFs        │  │  API         │
│  (IA)        │  │  OBS ──── Backups BD  │  │  (licitac.)  │
│              │  │  CSMS ─── Secretos    │  └──────────────┘
│              │  │  WAF ──── Protección  │
│              │  │  LTS ──── Logs        │
│              │  │  CES ──── Métricas    │
│              │  │  SMN ──── Alertas     │
│              │  │  CBR ──── Backups     │
│              │  │  DNS ──── Dominio     │
│              │  │  EIP ──── IP fija     │
└──────────────┘  └───────────────────────┘
```

---

## 5. Recursos On-Premise — Especificaciones

El servidor aporta únicamente **vCPU, RAM y disco**. No se asumen especializaciones de hardware adicional.

### Desglose de consumo por servicio Docker

| Servicio | vCPU | RAM | Disco |
|---|---|---|---|
| API .NET 8 | 1–2 vCPU | 2–4 GB | ~2 GB (binarios + logs) |
| Frontend nginx | < 0.5 vCPU | 256 MB | ~500 MB |
| PostgreSQL 16 | 1–2 vCPU | 4–8 GB | 200–500 GB (datos) |
| Redis 7 | < 0.5 vCPU | 512 MB–1 GB | < 1 GB |
| SO + Docker overhead | 0.5 vCPU | 1–2 GB | 20–30 GB |
| **Total** | **~4–5 vCPU** | **~8–14 GB** | **~225–535 GB** |

### Especificaciones mínimas y recomendadas

| Recurso | Mínimo | Recomendado |
|---|---|---|
| **vCPU** | 4 vCPU | 8 vCPU |
| **RAM** | 12 GB | 16 GB |
| **Disco** | 300 GB | 600 GB |
| **SO** | Ubuntu 22.04 LTS o RHEL 8+ | — |

> Con los mínimos el sistema opera correctamente para el volumen actual. Los 8 vCPU / 16 GB aplican si se suman usuarios concurrentes o se agregan fases futuras (Fase 7 Pipeline, Fase 11 Inteligencia Competitiva).

### Conectividad

- Salida a internet: **20 Mbps** mínimo dedicados
- Destinos de salida: `generativelanguage.googleapis.com` (Gemini), `obs.<region>.myhuaweicloud.com` (OBS)
- IP pública fija (o EIP de Huawei asignada vía NAT/VPN)
- Puertos de entrada: 443 (HTTPS), 80 (redirección a HTTPS), 5433 (opcional, acceso externo a BD — no recomendado)

---

## 6. Variables de Entorno — Producción

Cambios respecto al ambiente de desarrollo local:

```env
# Storage → Huawei OBS
Storage__Provider=obs
Storage__Endpoint=https://obs.<region>.myhuaweicloud.com
Storage__Bucket=mpm-licitaciones-prod
Storage__AccessKey=<desde-CSMS>
Storage__SecretKey=<desde-CSMS>

# Secretos → leer desde Huawei CSMS en producción
# JWT_SECRET, GEMINI_API_KEY, DB_PASSWORD, MP_TICKET
# (no en .env plano; el proceso los lee desde CSMS al arrancar)

# Monitor activado en producción
MONITOR_ENABLED=true
MONITOR_INTERVAL_MINUTES=30
SCRAPER_ENABLED=true
```

---

## 7. Consideraciones Adicionales

1. **OBS ciclo de vida:** Configurar política automática en el bucket de PDFs para mover objetos con más de 12 meses a `OBS Archive` (60% más barato). Los análisis ya completados no necesitan acceso frecuente al PDF original.

2. **CSMS vs .env:** En producción, reemplazar el `.env` con lectura de secretos desde Huawei CSMS. Elimina el riesgo de secrets en texto plano en el servidor. El API .NET ya usa `IConfiguration` y soporta múltiples proveedores.

3. **WAF — reglas iniciales recomendadas:** Activar protección contra SQLi, XSS, fuerza bruta en `/api/v1/auth/login` (máximo 10 intentos/minuto por IP), y bloqueo geográfico si aplica.

4. **LTS — retención de logs:** Retener logs de la API 90 días y del scraper 30 días. Los logs de error deben disparar alerta en SMN al email del administrador.

5. **CES — alertas críticas:** Configurar alarma cuando: CPU > 85% por 5 minutos, RAM disponible < 1 GB, disco > 90% de uso. Destino: SMN → email del equipo.

6. **CBR — política de backup:** Snapshot diario del volumen de datos PostgreSQL a las 02:00 Chile. Retención: 30 snapshots diarios + 12 mensuales. El restore se hace desde la consola Huawei sin intervención técnica en el servidor.

7. **MP_TICKET e IP fija:** El ticket de autenticación de Mercado Público puede estar ligado a IP. La EIP de Huawei Cloud asignada debe ser la misma IP siempre. Si el servidor usa IP dinámica del ISP, considerar VPN Gateway o NAT Gateway con EIP.

8. **Gemini API — contingencia:** Si la API de Gemini falla o sube de precio, el servicio Huawei OCR puede preprocesar PDFs escaneados mientras se evalúa alternativa. El módulo `AnalisisBackgroundService` soporta fácilmente un nuevo adaptador de IA.

---

*Documento preparado por el equipo de Digital — TIVIT. Versión 3.0 — Análisis integral Huawei Cloud + On-Premise.*
