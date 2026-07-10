-- Recupera las referencias a documentos de analisis que quedaron apuntando a un path local
-- (/app/uploads/...) tras la migracion masiva de datos locales a produccion del 2026-07-09
-- (ver V094/V095). Los PDFs originales seguian existiendo en el volumen Docker local -- se
-- subieron a gs://tivit-cu010-mpm-adjuntos/analisis/ preservando la misma estructura de
-- carpetas/nombres, asi que basta con reemplazar el prefijo del path viejo por el nuevo.
-- Sin esto, "Reanalizar" fallaba con DirectoryNotFoundException para todo documento anterior
-- a la migracion (confirmado en vivo el 2026-07-10 contra el workspace 50 en produccion).

UPDATE analisis_documentos
SET ruta_storage = REPLACE(ruta_storage, '/app/uploads/', 'gs://tivit-cu010-mpm-adjuntos/')
WHERE ruta_storage LIKE '/app/uploads/%';
