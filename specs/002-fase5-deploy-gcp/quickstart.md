# Quickstart: Validación de Fase 5 — Despliegue en GCP

**Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

Escenarios para validar que el despliegue cumple los criterios de éxito del spec (SC-001 a SC-004) antes de dar la fase por cerrada.

> **Reescrito 2026-07-06** para el pivote a Cloud Run + Cloud Run Jobs (reemplaza la versión anterior, que asumía una VM de Compute Engine con IP externa y certbot).

## Prerrequisitos

- `016-extraccion-documentos-api` implementada y desplegada (bloqueante — sin esto `scraper-job` no puede crearse como Job de ejecución corta, ver spec.md)
- VPC custom + Serverless VPC Access Connector creados en `us-central1` (ver `research.md` §5b)
- Instancia Cloud SQL `mpm-db` migrada a Private IP, sin IP pública
- Instancia Memorystore para Redis, en la misma VPC
- Bucket GCS `tivit-cu010-mpm-adjuntos` accesible desde la Service Account del servicio Cloud Run
- Secretos (`JWT_SECRET`, `GEMINI_API_KEY`, `MP_TICKET`, credenciales de BD) cargados en Secret Manager
- Servicio Cloud Run `mpm-api` desplegado (API + frontend), con `min-instances >= 1` para SignalR
- Tres Cloud Run Jobs desplegados: `sync-job`, `scraper-job`, `analisis-job`
- Cloud Scheduler configurado para `sync-job` (diario) y `scraper-job` (cada ~6h); Pub/Sub configurado para disparar `analisis-job`

## Escenario 1 — Acceso público (SC-001, FR-001, FR-006)

```bash
# Desde cualquier red fuera de la VPC del equipo
curl -I https://<servicio>-<hash>-<region>.a.run.app/health
# Esperado: 200 OK, certificado válido (gestionado automáticamente por Cloud Run, sin warnings de curl sobre TLS)
```

1. Abrir la URL `*.run.app` del servicio (o el dominio mapeado, si ya existe) en un navegador desde una red distinta a la del equipo de desarrollo.
2. Iniciar sesión con un usuario existente.
3. Confirmar que el dashboard carga sin errores de consola relacionados a `/api` o `/hubs`.

**Pasa si**: el login funciona y no hay advertencias de certificado (Cloud Run gestiona el certificado automáticamente, no requiere pasos adicionales).

## Escenario 2 — Servicio y Jobs saludables (FR-001, FR-008, User Story 1)

```bash
gcloud run services describe mpm-api --region us-central1 --format="value(status.conditions)"
# Esperado: condición "Ready" = True

gcloud run jobs executions list --job sync-job --region us-central1 --limit 5
gcloud run jobs executions list --job scraper-job --region us-central1 --limit 5
gcloud run jobs executions list --job analisis-job --region us-central1 --limit 5
# Esperado: últimas ejecuciones con estado "Succeeded"
```

## Escenario 3 — Recuperación sin intervención manual (FR-004, User Story 1)

```bash
# Forzar una nueva revisión del servicio (equivalente a un "reinicio" en el modelo Cloud Run)
gcloud run services update mpm-api --region us-central1 --no-traffic
gcloud run services update-traffic mpm-api --region us-central1 --to-latest
```

**Pasa si**: la nueva revisión sirve tráfico sin intervención manual adicional y sin pérdida de datos (Cloud Run maneja el ciclo de vida de instancias automáticamente — no hay "instancia" que reiniciar como en una VM).

## Escenario 4 — Archivos en GCS, no en disco local (SC-002, FR-002, User Story 2)

1. Subir un documento nuevo a través del flujo de Análisis en la aplicación en producción.
2. Verificar en la consola de GCP (o `gsutil ls gs://tivit-cu010-mpm-adjuntos/...`) que el archivo aparece en el bucket.
3. Confirmar que no existe ningún volumen persistente de disco local en la configuración del servicio Cloud Run (Cloud Run no lo soporta de todas formas — el filesystem del contenedor es efímero por diseño, lo cual refuerza que todo archivo debe ir a GCS).

**Pasa si**: el archivo está en GCS.

## Escenario 5 — Restore de base de datos (SC-004, FR-003, User Story 2)

1. Tomar un snapshot/backup de Cloud SQL (automático o manual).
2. En una instancia Cloud SQL de prueba (o mediante clone), restaurar ese backup — con Private IP, igual que la instancia productiva.
3. Apuntar temporalmente el Serverless VPC Access Connector (o una copia del servicio Cloud Run en un ambiente de prueba) a la instancia restaurada y confirmar que responde con datos consistentes.
4. Medir el tiempo total desde que se inicia el restore hasta que la API responde correctamente.

**Pasa si**: el tiempo total es menor a 30 minutos (SC-004).

## Escenario 6 — Deploy de una nueva versión (SC-003, FR-007, User Story 3)

```bash
# En CI o localmente, siguiendo el runbook (docs/runbook-produccion.md)
./scripts/deploy.sh prod api
```

1. Medir el tiempo desde que se ejecuta `deploy.sh` hasta que la nueva revisión de Cloud Run sirve el 100% del tráfico y responde en `/health`.
2. Confirmar que no fue necesario ningún paso manual fuera de lo documentado en el runbook.
3. Confirmar que, si el deploy falla (revisión no llega a "Ready"), Cloud Run no enruta tráfico a la revisión rota — la anterior sigue respondiendo (comportamiento nativo de Cloud Run, no requiere lógica propia de rollback).

**Pasa si**: el tiempo total es menor a 15 minutos (SC-003) y no hubo pasos manuales no documentados.

## Escenario 7 — Fallo controlado (Edge case, FR-005)

1. Revocar temporalmente el permiso de la Service Account sobre el bucket GCS (en un entorno de prueba, no en producción).
2. Intentar subir un documento.

**Pasa si**: el sistema muestra un error claro al usuario en vez de fallar silenciosamente o corromper el registro en base de datos.

## Escenario 8 — Cloud Run Jobs no dependen de un proceso continuo (NUEVO, FR-008)

```bash
gcloud run jobs execute scraper-job --region us-central1 --wait
```

**Pasa si**: la ejecución termina (estado `Succeeded`) en vez de quedar corriendo indefinidamente — confirma que 016 realmente desacopló el scraper de un navegador persistente y que el Job es de ejecución corta, no un proceso de fondo disfrazado de Job.
