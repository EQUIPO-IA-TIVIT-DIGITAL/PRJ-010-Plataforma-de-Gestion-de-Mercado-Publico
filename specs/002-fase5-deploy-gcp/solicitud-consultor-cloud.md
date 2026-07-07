> ⚠️ **Superado en parte — respuesta de Nicolás del 2026-07-06**: este correo pedía VM con IP externa, firewall TCP:80 abierto a todo origen, y solo quitar `0.0.0.0/0` de Cloud SQL. Nicolás rechazó la VPC default, la IP pública en la VM, y el enfoque de Cloud SQL (exige Private IP total, no solo cerrar `0.0.0.0/0`). La respuesta corregida está en `solicitud-segmentacion-red.md`. Se deja este correo como registro histórico de la solicitud original.

Asunto: [MPM / CU010] Recursos a crear en tivit-cu010

Hola Nicolás, ¿cómo estás?

Te escribo porque necesitamos que nos ayudes a crear algunos recursos en el proyecto `tivit-cu010`, región `us-central1`, para dejar MPM funcionando en producción:

**1. Service Accounts**
- `mpm-prod@tivit-cu010.iam.gserviceaccount.com` (para la VM de producción)
- `mpm-cloudrun@tivit-cu010.iam.gserviceaccount.com` (para cuando migremos parte de la app a Cloud Run, no la usamos todavía)
- Ambas con los mismos roles: `roles/cloudsql.client` (proyecto) + `roles/storage.objectAdmin` (scopeado solo al bucket `tivit-cu010-mpm-adjuntos`)

**2. VM Compute Engine**
- Nombre: `mpm-prod` · Zona: `us-central1-a` · Tipo: `e2-medium` · Ubuntu 22.04 LTS · Disco 30GB pd-balanced · IP externa · Service Account `mpm-prod` adjunta

**3. Firewall**
- Regla permitiendo ingreso TCP:80 (todo origen)

**4. Cloud SQL `mpm-db`**
- Sacar `0.0.0.0/0` de authorized networks (vamos a conectarnos vía Cloud SQL Auth Proxy con IAM, no por IP pública)

El resto de lo necesario para Cloud Run (habilitar la API, Artifact Registry, el deploy en sí) lo hacemos nosotros — con la Service Account ya creada queda todo listo de este lado.

¿Me avisas cuando esté listo? Cualquier duda, quedo atento.

Muchas gracias por el apoyo,
Matías
