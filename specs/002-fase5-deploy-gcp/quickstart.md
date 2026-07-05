# Quickstart: Validación de Fase 5 — Despliegue en GCP

**Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

Escenarios para validar que el despliegue cumple los criterios de éxito del spec (SC-001 a SC-004) antes de dar la fase por cerrada.

## Prerrequisitos

- VM de Compute Engine provisionada, con Docker + Docker Compose instalados
- IP externa estática asignada y DNS apuntando a ella
- Instancia Cloud SQL para PostgreSQL creada, con la base de datos MPM restaurada o migrada
- Bucket GCS `tivit-cu010-mpm-adjuntos` accesible desde la Service Account de la VM
- `.env` de producción presente en la VM (fuera del repositorio) con todas las variables de `.env.example` completadas
- Certificado TLS emitido (certbot) para el dominio asignado

## Escenario 1 — Acceso público (SC-001, FR-001, FR-006)

```bash
# Desde cualquier red fuera de la VPC del equipo
curl -I https://<dominio-asignado>/health
# Esperado: 200 OK, certificado válido (sin warnings de curl sobre TLS)
```

1. Abrir `https://<dominio-asignado>` en un navegador desde una red distinta a la del equipo de desarrollo (p. ej. datos móviles).
2. Iniciar sesión con un usuario existente.
3. Confirmar que el dashboard carga sin errores de consola relacionados a `/api` o `/hubs`.

**Pasa si**: el login funciona y no hay advertencias de certificado.

## Escenario 2 — Servicios saludables (FR-001, User Story 1)

```bash
# En la VM
docker compose -f docker-compose.prod.yml ps
# Esperado: api, web, redis en estado "healthy" o "running"; db no aplica si es Cloud SQL
```

## Escenario 3 — Reinicio sin intervención manual (FR-004, User Story 1)

```bash
# En la VM
sudo reboot
# Esperar a que la VM vuelva a estar accesible por SSH
docker compose -f docker-compose.prod.yml ps
```

**Pasa si**: todos los servicios vuelven a `healthy`/`running` sin ejecutar ningún comando manual adicional.

## Escenario 4 — Archivos en GCS, no en disco local (SC-002, FR-002, User Story 2)

1. Subir un documento nuevo a través del flujo de Análisis en la aplicación en producción.
2. Verificar en la consola de GCP (o `gsutil ls gs://tivit-cu010-mpm-adjuntos/...`) que el archivo aparece en el bucket.
3. Verificar que `docker exec` al contenedor `api` y revisar `/app/uploads` **no** contiene el archivo recién subido.

**Pasa si**: el archivo está en GCS y no en disco local del contenedor.

## Escenario 5 — Restore de base de datos (SC-004, FR-003, User Story 2)

1. Tomar un snapshot/backup de Cloud SQL (automático o manual).
2. En una instancia Cloud SQL de prueba (o mediante clone), restaurar ese backup.
3. Apuntar una copia de la API a la instancia restaurada y confirmar que responde con datos consistentes.
4. Medir el tiempo total desde que se inicia el restore hasta que la API responde correctamente.

**Pasa si**: el tiempo total es menor a 30 minutos (SC-004).

## Escenario 6 — Deploy de una nueva versión (SC-003, FR-007, User Story 3)

```bash
# En la VM, siguiendo el runbook (docs/runbook-produccion.md)
./scripts/deploy.sh
```

1. Medir el tiempo desde que se ejecuta `deploy.sh` hasta que el sistema vuelve a responder en `/health`.
2. Confirmar que no fue necesario ningún paso manual fuera de lo documentado en el runbook.

**Pasa si**: el tiempo total es menor a 15 minutos (SC-003) y no hubo pasos manuales no documentados.

## Escenario 7 — Fallo controlado (Edge case, FR-005)

1. Revocar temporalmente el permiso de la Service Account sobre el bucket GCS (en un entorno de prueba, no en producción).
2. Intentar subir un documento.

**Pasa si**: el sistema muestra un error claro al usuario en vez de fallar silenciosamente o corromper el registro en base de datos.
