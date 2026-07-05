# Data Model: Fase 5 — Despliegue en GCP

**Spec**: [spec.md](./spec.md)

## N/A

Esta fase es puramente de infraestructura/despliegue: no introduce entidades de negocio nuevas ni cambia el modelo de datos existente de MPM. La base de datos se traslada a Cloud SQL (ver `research.md` §2) sin alterar esquema, stored procedures ni migraciones — es un cambio de dónde corre Postgres, no de qué contiene.

No se genera `contracts/` por la misma razón: no se expone ni consume ninguna interfaz nueva de cara a usuarios u otros sistemas. El único "contrato" relevante es operativo y vive en `quickstart.md` (comandos de validación) y en `docs/runbook-produccion.md` (a crear durante la implementación).
