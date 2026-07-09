-- Prod se presenta hoy por primera vez: los datos existentes en licitaciones (63k, todos con
-- fecha_publicacion NULL) vienen de una migracion previa incompleta, no de uso real de usuarios.
-- Se vacian estas tablas para recargarlas completas desde el dataset local (126k licitaciones,
-- 43 analisis de TIVIT reales) via `gcloud sql import csv`, preservando los IDs originales para
-- que las FK de analisis_workspaces -> licitaciones sigan siendo validas tras el import.
TRUNCATE TABLE analisis_resultados, analisis_documentos, analisis_workspaces, licitaciones RESTART IDENTITY CASCADE;
