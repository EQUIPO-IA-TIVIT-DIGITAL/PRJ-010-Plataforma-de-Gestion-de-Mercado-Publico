# Research: Fase 5 — Despliegue en GCP

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)
**Fecha**: 2026-07-03 | **Reescrito**: 2026-07-06 (pivote de Compute Engine a Cloud Run + Cloud Run Jobs)

Resuelve las decisiones técnicas marcadas como abiertas en `plan.md`. Cada decisión está fundamentada en evidencia real del repositorio (Dockerfiles, `docker-compose.yml`, background services existentes) y en restricciones explícitas del equipo de infraestructura de TIVIT (Nicolás Valdivia, 2026-07-06), no en supuestos genéricos de "mejores prácticas de GCP".

> **Nota de versión**: este documento reemplaza la versión del 2026-07-03 (que elegía Compute Engine). Se conserva el razonamiento original donde sigue siendo relevante (por qué Cloud Run parecía mal fit) y se explica qué cambió para revertir esa decisión.

---

## 1. Cómputo: Compute Engine vs. Cloud Run

**Decisión (REVERTIDA 2026-07-06)**: **Cloud Run**, no Compute Engine.

### Por qué se había descartado Cloud Run el 2026-07-03

- `src/MPM.Api/Dockerfile` instala Node.js 20 + Playwright Chromium dentro del contenedor de la API, porque `ScraperBackgroundService` ejecuta `tools/scraper-mp/agente-mp.js` con un navegador headless real, de forma continua (login + navegación por cada licitación). Cloud Run throttlea el CPU del contenedor a cero fuera de un request HTTP activo — un navegador corriendo en un `Timer` sin request asociado se congela.
- `SyncEngineService`, `ScraperBackgroundService` y `AnalisisBackgroundService` corrían como `IHostedService` embebidos en el mismo proceso ASP.NET Core — el mismo problema de throttling aplica a cualquiera de los tres, no solo al que usa Chromium.
- SignalR con backplane Redis mantiene conexiones abiertas por horas — parecía un mal fit para instancias que se reciclan.

### Qué cambió (2026-07-06) — y qué se revirtió el mismo día tras el spike

1. ~~**`016-extraccion-documentos-api`** reemplaza la descarga de Actas por HTTP directo, dejando el uso de Chromium reducido a una renovación de sesión corta cada ~6h~~ **REVERTIDO tras ejecutar el spike en vivo el 2026-07-06** (con credenciales reales, ver `specs/016-extraccion-documentos-api/contracts/internal-api.md` §1.3): el paso del listado de adjuntos (`ViewAttachment.aspx`) está protegido por **Google reCAPTCHA Enterprise** ejecutado client-side — un `HttpClient` sin motor JS no puede resolverlo. Un navegador real sí lo resuelve de forma transparente. Esto significa que **Chromium sigue siendo necesario por cada licitación**, no solo para renovar sesión — 016 no logra el objetivo que motivó este punto del pivote.
**Corrección de matiz (mismo día)**: lo anterior no significa que `scraper-job` sea inviable — significa que no es "corto". Cloud Run **Jobs** (a diferencia de Cloud Run **Services**) no throttlean CPU por inactividad de requests: corren hasta completarse con recursos completos asignados, igual que un proceso batch en una VM, con timeout configurable hasta 24h. El argumento original en contra de Cloud Run (sección de abajo, "Por qué se había descartado") aplicaba al modelo de **Service** request/response — nunca aplicó del mismo modo a Jobs. Así que `scraper-job` sigue siendo viable como Cloud Run Job disparado por Cloud Scheduler, ejecutando el mismo ciclo completo con Chromium que hoy corre `ScraperBackgroundService` — solo que fuera del proceso web, sin el beneficio adicional de "ejecución corta" que se esperaba de 016.

2. El equipo de infraestructura de TIVIT (Nicolás Valdivia) bloqueó **IP pública en cualquier VM** — exponer un servicio solo se permite vía Load Balancer. Eso ya obligaba a introducir un componente adicional (LB) para Compute Engine; Cloud Run expone HTTPS gestionado por defecto en su URL `*.run.app`, sin necesitar Load Balancer para tener el sistema accesible.
3. Los tres background services ya eran naturalmente "ejecutar un ciclo y salir" (Sync verifica última sync antes de correr; el scraper Node ya soporta `--incremental`; Análisis procesa una cola) — el patrón encaja con **Cloud Run Jobs** disparados por Cloud Scheduler/Pub-Sub en vez de un `Timer` infinito dentro del proceso del API. Esto es trabajo de re-ingeniería real (ver `plan.md` "Cambios de código requeridos"), no gratis, pero no es una limitación de la plataforma.
4. SignalR es viable en Cloud Run: soporta WebSockets con requests de hasta 60 minutos y *session affinity*; como MPM ya usa Redis (ahora Memorystore) como backplane, el broadcast entre instancias no depende de afinidad de sesión. Con `min-instances >= 1` en el servicio del API se evita que un cold-start corte sesiones de chat activas.

**Rationale de la decisión final**: con 016 implementada y los background services separados en Jobs, Cloud Run elimina la necesidad de administrar una VM (parches de SO, Docker, red, SSH/IAP) y el Load Balancer que Compute Engine iba a requerir de todas formas por la restricción de IP pública. El costo de la re-ingeniería (separar los 3 `IHostedService` en Jobs) se paga una sola vez; el costo operativo de una VM persistente se paga indefinidamente.

**Alternatives considered**:
- **Compute Engine** (decisión original del 2026-07-03): descartada — ver arriba. Se documenta como fallback si la separación en Jobs resulta más costosa de lo estimado durante la implementación.
- **GKE (Kubernetes)**: descartado por complejidad operativa desproporcionada para un sistema de un solo tenant interno con un equipo de desarrollo pequeño.

---

## 1b. Background services: `IHostedService` embebido vs. Cloud Run Jobs — NUEVO 2026-07-06

**Decisión**: tres Cloud Run Jobs independientes — `sync-job`, `scraper-job`, `analisis-job` — reemplazando a `SyncEngineService`, `ScraperBackgroundService` y `AnalisisBackgroundService` como `IHostedService`.

**Disparo**:
- `sync-job`: Cloud Scheduler, 1 vez/día (mismo intervalo que hoy).
- `scraper-job`: Cloud Scheduler, cada ~6h (alineado con el TTL de sesión de 016) — **depende de que 016 esté implementada**; hasta entonces no es viable como Job de ejecución corta.
- `analisis-job`: Pub/Sub push en vez de polling — cada solicitud de análisis publica un mensaje que dispara una ejecución del Job (más eficiente que el loop de cola actual, y más nativo del modelo serverless).

**Rationale**: Cloud Run Jobs tienen CPU asignado durante toda su ejecución (no dependen de servir requests), por lo que no sufren el throttling que bloquea a Cloud Run Services para este tipo de trabajo. Reutilizan el mismo código de dominio (`MPM.Modules.Licitaciones`, `MPM.Modules.Analisis`) — el cambio es de "quién invoca el ciclo" (Timer interno → Cloud Scheduler/Pub-Sub), no de la lógica de negocio.

**Alternatives considered**: mantener un único Cloud Run Service "worker" con `min-instances=1` y CPU-always-allocated corriendo los tres `Timer` como hoy — descartado porque anula gran parte del ahorro de Cloud Run (CPU siempre facturado) sin ganar nada frente a simplemente mantener Compute Engine.

### Riesgo NUEVO 2026-07-06 — Mercado Público puede mostrar reCAPTCHA solo en modo headless

**Hallazgo (validado en vivo, no solo teoría)**: el 2026-07-06 se corrió el scraper varias veces contra el portal real de Mercado Público (cuenta TIVIT) en modo headless (`MP_HEADLESS=true`, como corre hoy dentro del contenedor Docker y como correría `scraper-job` en Cloud Run): el paso de "Ver Adjuntos" fallo con `403` en el 100% de los intentos, en 3 corridas completas seguidas (30 intentos en total). Al correr exactamente el mismo código en la misma máquina pero con `MP_HEADLESS=false` (Chromium visible), el mismo paso funcionó al segundo intento — el usuario observó visualmente un reCAPTCHA con desafío numérico aparecer durante los intentos headless, algo que "no pasaba antes" según su experiencia previa con el portal.

**Hipótesis de trabajo** (razonable pero no 100% confirmada — un solo dato de éxito en modo visible): Chromium headless tiene una huella digital detectable (comportamiento de renderizado, `navigator.webdriver`, etc.) incluso con `--disable-blink-features=AutomationControlled`, y el sistema anti-bot de Mercado Público probablemente puntúa el tráfico headless con mayor riesgo, disparando reCAPTCHA con más frecuencia que al tráfico headed. No parece ser un bloqueo permanente de la cuenta TIVIT (el modo visible funcionó de inmediato sin esperar).

**Por qué esto es un riesgo real para `scraper-job` en Cloud Run**: Cloud Run Jobs no tienen pantalla — cualquier proceso ahí es inherentemente "headless" en el sentido de no tener un display físico. Si el diagnóstico de arriba es correcto, `scraper-job` podría toparse con reCAPTCHA sistemáticamente en producción, bloqueando la extracción de adjuntos (aunque el login y la búsqueda sí funcionan en headless — solo el paso de adjuntos fue observado fallando).

**Mitigación validada en vivo el mismo día (2026-07-06)**: correr Chromium en modo "headed" (`MP_HEADLESS=false`) dentro de un framebuffer virtual (`Xvfb`) — técnica estándar para que un proceso sin monitor físico obtenga el mismo perfil de renderizado que un navegador visible, sin necesitar una pantalla real. Se probó directamente dentro del contenedor `mpm-api` (que no tiene ninguna pantalla real, igual que tendría `scraper-job` en Cloud Run): se inició `Xvfb :99` manualmente, se corrió el scraper con `DISPLAY=:99` y `MP_HEADLESS=false`, y el paso de "Ver Adjuntos" pasó exitosamente (con reintento, igual que en el entorno con pantalla real) — confirma que el diagnóstico de arriba es correcto y que Xvfb es una mitigación funcional, no solo teórica.

**Para producción falta**: hornear esto en el Dockerfile/entrypoint de `scraper-job` de forma permanente (`apt-get install -y xvfb xauth` — la prueba en vivo encontró que `xvfb-run` requiere `xauth`, que no viene instalado por defecto aunque `Xvfb` sí; se usó el enfoque manual `Xvfb :99 & DISPLAY=:99 node ...` como workaround, pero para producción conviene instalar `xauth` y usar `xvfb-run --auto-servernum --` que maneja el ciclo de vida del proceso Xvfb automáticamente). Complejidad real, confirmada: baja. Sin cambios de lógica de negocio, solo Dockerfile + como se invoca el proceso Node. Alternativas descartadas: reutilización de cookies de sesión pre-autenticada (frágil, requiere refresco manual periódico) y servicios de resolución de CAPTCHA de terceros (riesgo de términos de servicio, costo recurrente, complejidad de integración mayor) — ya no hacen falta dado que Xvfb funcionó.

---

## 2. Base de datos: Cloud SQL vs. PostgreSQL en contenedor

**Decisión**: Cloud SQL para PostgreSQL, como servicio gestionado, con **Private IP obligatorio** (revisado 2026-07-06 — ver hallazgo de seguridad abajo).

**Rationale**:
- `docker-compose.yml` (historial de commits) ya tenía comentado un bloque `db` con la nota *"Descomentar para usar PostgreSQL local en lugar de Cloud SQL"* — Cloud SQL ya era la intención original del equipo.
- FR-003 del spec exige que la base de datos sea recuperable ante falla del cómputo. Cloud SQL provee backups automáticos y point-in-time recovery.
- El acceso ya es 100% vía stored procedures + Dapper (Principio II) — Cloud SQL es un Postgres estándar, no requiere cambios de código, solo de connection string.
- **Cloud Run necesita un Serverless VPC Access Connector** para llegar a la IP privada de Cloud SQL — no hay Cloud SQL Auth Proxy como sidecar de VM (ese patrón era específico de Compute Engine); en Cloud Run, la conexión privada vía el connector es el mecanismo equivalente, o bien el conector Unix-socket de Cloud SQL para Cloud Run (`cloudsql-instance` en la config del servicio), que también requiere que la instancia sea alcanzable — con Private IP puro se usa el Connector VPC.

**Alternatives considered**: PostgreSQL en contenedor — no aplica en Cloud Run (no hay volumen persistente ni proceso de larga duración para sostener un Postgres propio); descartado también en la versión anterior de este documento por ser el mismo punto único de falla que el cómputo.

### Hallazgo de seguridad (2026-07-03, corregido en alcance 2026-07-06)

Inspección de solo lectura sobre `tivit-cu010` (autorizada por el cliente) mostró que `mpm-db` tenía `authorizedNetworks=0.0.0.0/0` (abierta a cualquier IP de internet) y SSL opcional. La corrección planeada originalmente (quitar solo el `0.0.0.0/0`, conectar vía Cloud SQL Auth Proxy) **no es suficiente** según el equipo de infraestructura de TIVIT: "tampoco los cloud sql debe tener nunca IP pública ni tampoco utilizar la red autorizada 0.0.0.0/0, esto es una brecha de seguridad gigantesca" (Nicolás Valdivia, 2026-07-06).

**Corrección real necesaria**:
- Habilitar **Private IP** en `mpm-db` y **deshabilitar la IP pública** de la instancia por completo.
- Requiere **Private Services Access** (peering) con un rango de IP dedicado, asignado por el equipo de infraestructura de TIVIT dentro de la VPC custom (ver §5b).
- Cloud Run se conecta a esa IP privada a través del Serverless VPC Access Connector — ningún tramo de la conexión pasa por internet.
- Esto es trabajo del equipo de infraestructura de TIVIT antes de poder migrar `mpm-db` de público a privado — **bloqueante para el deploy** hasta que exista el peering.

---

## 3. Redis: contenedor vs. Memorystore

**Decisión (REVISADA 2026-07-06)**: **Memorystore for Redis**, no contenedor.

**Rationale**: la decisión original (Redis en contenedor junto a la app, en la misma VM) ya no aplica porque Cloud Run no sostiene un contenedor con estado persistente de la misma forma que una VM — cada revisión de un servicio Cloud Run es efímera y puede tener múltiples instancias concurrentes, no hay "la" instancia donde vivir un Redis compartido. Memorystore es el reemplazo directo: mismo protocolo (`StackExchange.Redis`, sin cambios de código, solo el connection string), accesible desde Cloud Run vía el mismo Serverless VPC Access Connector que Cloud SQL (misma VPC).

**Alternatives considered**: Redis como sidecar/contenedor adicional en el propio servicio Cloud Run (Cloud Run soporta multi-contenedor) — descartado porque reintroduce el problema de estado no persistente entre instancias/revisiones y no resuelve el uso como backplane compartido entre múltiples instancias del servicio.

---

## 4. Storage de archivos

**Decisión**: GCS, bucket `tivit-cu010-mpm-adjuntos` (ya existente) vía `GcsStorageService` (ya implementado). Sin cambios por el pivote a Cloud Run — no requiere VPC privada, es tráfico a la API pública de GCS.

**Rationale**: ya resuelta en código e infraestructura existente (`GOOGLE_CLOUD_PROJECT=tivit-cu010`). Esta fase solo activa `Storage__Provider=gcs` en las variables de entorno del servicio/Jobs de Cloud Run.

---

## 5. TLS y exposición pública

**Decisión (REVISADA 2026-07-06)**: Cloud Run expone HTTPS gestionado automáticamente en su URL `*.run.app` — no requiere Load Balancer, certbot, ni gestión manual de certificados para que el sistema sea accesible.

**Historial de esta decisión**: la versión del 2026-07-03 elegía certbot delante de nginx en una VM. El 2026-07-06, al confirmarse que ninguna VM puede tener IP pública, se evaluó reemplazar certbot por un GCP HTTPS Load Balancer frente a la VM — pero al pivotar el cómputo completo a Cloud Run (ver §1), el problema desaparece: Cloud Run ya resuelve TLS gestionado sin componentes adicionales.

**Implicaciones**:
- Acceso inicial sin dominio propio: la URL `*.run.app` ya sirve HTTPS válido desde el día uno — cumple FR-001/FR-006 sin esperar a que el cliente defina un dominio.
- Cuando exista un dominio, se mapea con `gcloud run domain-mappings create` (Cloud Run gestiona el certificado del dominio automáticamente vía Google-managed certs) — cambio de configuración, no de arquitectura.
- No se necesita IAP para SSH ni reglas de firewall para health checks de Load Balancer — Cloud Run no expone una VM a administrar.

**Alternatives considered**: GCP HTTPS Load Balancer — necesario si el cómputo fuera Compute Engine (ver versión anterior de este documento), innecesario con Cloud Run.

---

## 5b. Red (VPC) — NUEVO 2026-07-06

**Decisión**: VPC custom por ambiente (no la VPC `default` del proyecto), con un **Serverless VPC Access Connector** para que Cloud Run (servicio y Jobs) alcance recursos con IP privada (Cloud SQL, Memorystore).

**Rationale**: requisito explícito del equipo de infraestructura de TIVIT: "cada ambiente que ustedes generen debe tener un segmento diferente [...] las VPCs default son solo de muestra, por favor al generar servicio no utilizarla". Aplica igual con Cloud Run que con Compute Engine — Cloud Run no elimina la necesidad de VPC/segmentación, solo elimina la necesidad de una VM y un Load Balancer.

**Segmentación propuesta** (pendiente de que el equipo de infraestructura la cree — ver `solicitud-segmentacion-red.md`):
- **CU010 PRD** — subnet `10.0.0.0/24` en `us-central1`, donde vive el Serverless VPC Access Connector.
- **CU010 QA** — subnet `10.0.1.0/24`, reservada a futuro (no hay ambiente QA planeado hoy).
- **Rango para Cloud SQL Private Services Access** — rango separado (ej. `10.0.8.0/24`, propuesta) para el peering privado de Cloud SQL — ver §2.
- **Rango para Memorystore** — Memorystore también requiere estar en la misma VPC; puede compartir el connector de PRD, a confirmar tamaño con el equipo de infraestructura según cuántas instancias se planeen.

---

## 6. Credenciales y secretos

**Decisión (REVISADA 2026-07-06)**: cada servicio/Job de Cloud Run corre con su propia **Service Account** de GCP con permisos mínimos (Storage Object Admin sobre `tivit-cu010-mpm-adjuntos`, Cloud SQL Client, `roles/run.invoker` para que Cloud Scheduler/Pub-Sub disparen los Jobs). Secretos de aplicación (`JWT_SECRET`, `GEMINI_API_KEY`, `MP_TICKET`, credenciales de BD) en **Secret Manager**, montados como variables de entorno del servicio/Job — ya no en un `.env` de una VM que ya no existe.

**Rationale**: Cumple FR-005 (no exponer credenciales en el repositorio). Secret Manager es el mecanismo nativo de Cloud Run para esto (integración directa al definir el servicio/Job), más simple que gestionar permisos de archivo en un `.env` sobre una VM.

**Alternatives considered**: `.env` fuera del repo con permisos de archivo restringidos — era la decisión original para Compute Engine; ya no aplica al no haber una VM cuyo filesystem gestionar.

---

## Actualización 2026-07-03 — Estado real verificado en la consola de GCP

Inspección de solo lectura sobre el proyecto `tivit-cu010` (autorizada por el cliente), vigente salvo lo corregido en las secciones de arriba:

- **Cloud SQL ya existe**: instancia `mpm-db`, PostgreSQL 16, `us-central1-a`, tier `db-f1-micro`, 10GB, con la base `mpm` ya creada y backups automáticos diarios (7 retenidos, 03:00). **No hay que crear Cloud SQL — solo migrarla a Private IP y conectar Cloud Run.**
- **Compute Engine**: 0 instancias — ya no se crea ninguna (pivote a Cloud Run).
- **Bucket** `tivit-cu010-mpm-adjuntos`: existe, sin bindings IAM a nivel de bucket para ninguna service account específica todavía.
- **Service accounts existentes**: `agente-mercado-publico` y `Gemini API Key` (sin roles de proyecto vinculados) y la SA default de Compute Engine (ya no relevante). Se crean Service Accounts dedicadas para el servicio Cloud Run y para los Jobs.
- **Firewall**: solo reglas default (SSH, RDP, interno, ICMP) — ya no relevante para tráfico web, Cloud Run no usa reglas de firewall de VPC de la misma forma que una VM; si aplica, es para el tráfico saliente del Connector VPC hacia Cloud SQL/Memorystore.
- **Billing**: habilitado y activo en el proyecto.

## Resumen de decisiones

| Decisión | Elegido | Pendiente de confirmación |
|---|---|---|
| Cómputo | **Cloud Run** (servicio web) — pivote 2026-07-06, reemplaza a Compute Engine | — |
| Background services | **Cloud Run Jobs** (`sync-job`, `scraper-job`, `analisis-job`) — nuevo 2026-07-06 | `scraper-job` bloqueado hasta implementar `016-extraccion-documentos-api` |
| Base de datos | Cloud SQL para PostgreSQL, **Private IP obligatorio, sin IP pública** | Rango de Private Services Access — lo asigna el equipo de infraestructura de TIVIT |
| Redis | **Memorystore** (revisado 2026-07-06, reemplaza a contenedor en VM) | Tamaño/tier de la instancia |
| Storage | GCS (`tivit-cu010-mpm-adjuntos`, ya existe) | — |
| Red (VPC) | VPC custom por ambiente + Serverless VPC Access Connector (nuevo 2026-07-06) | CIDR final de PRD/QA/Cloud SQL — propuesto en `solicitud-segmentacion-red.md`, a confirmar con el equipo de infraestructura |
| Exposición pública / TLS | **HTTPS gestionado nativo de Cloud Run** (revisado 2026-07-06, ya no requiere Load Balancer ni certbot) | Dominio propio — opcional, no bloqueante |
| Secretos | Secret Manager + Service Account por servicio/Job (revisado 2026-07-06) | — |
| Región | `us-central1` — confirmado 2026-07-03 (coincide con bucket y `mpm-db`) | — |
