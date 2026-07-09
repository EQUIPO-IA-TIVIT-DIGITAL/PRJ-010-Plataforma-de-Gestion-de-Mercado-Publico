-- El import via gcloud sql import csv (V094) preservo los IDs originales de local, pero no
-- avanzo las secuencias de identity de cada tabla -- sin esto, el proximo INSERT (sync-job,
-- nueva licitacion, nuevo workspace de analisis) chocaria con un id ya existente.
SELECT setval(pg_get_serial_sequence('licitaciones', 'id'), COALESCE((SELECT max(id) FROM licitaciones), 1));
SELECT setval(pg_get_serial_sequence('analisis_workspaces', 'id'), COALESCE((SELECT max(id) FROM analisis_workspaces), 1));
SELECT setval(pg_get_serial_sequence('analisis_documentos', 'id'), COALESCE((SELECT max(id) FROM analisis_documentos), 1));
SELECT setval(pg_get_serial_sequence('analisis_resultados', 'id'), COALESCE((SELECT max(id) FROM analisis_resultados), 1));
