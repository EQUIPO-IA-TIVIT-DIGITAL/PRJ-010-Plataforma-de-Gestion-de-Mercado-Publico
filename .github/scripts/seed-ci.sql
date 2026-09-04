-- Seed mínimo SOLO para CI (BD fresca): ~3000 licitaciones sintéticas para que los
-- tests de datos tengan sustancia (índice GIN, filtros por fecha, búsquedas).
-- Nunca se corre en prod: el workflow CI lo aplica tras migrate.sh en BD efímera.
INSERT INTO licitaciones
    (codigo_externo, nombre, descripcion, codigo_estado, tipo, organismo,
     monto_estimado, fecha_publicacion, fecha_cierre)
SELECT
    'TEST-CI-' || g,
    CASE WHEN g % 3 = 0
         THEN 'Construcción de puente sector norte ' || g
         ELSE 'Adquisición de insumos de oficina ' || g END,
    CASE WHEN g % 3 = 0
         THEN 'Obras de construcción vial y mantención de calzada'
         ELSE 'Compra de materiales y útiles de escritorio' END,
    1 + (g % 5),
    CASE WHEN g % 2 = 0 THEN 'LE' ELSE 'LP' END,
    'Municipalidad de Prueba CI',
    1000000 + g * 1000,
    TIMESTAMP '2024-01-01' + ((g % 900) || ' days')::interval,
    TIMESTAMP '2024-02-01' + ((g % 900) || ' days')::interval
FROM generate_series(1, 3000) AS g
ON CONFLICT DO NOTHING;

ANALYZE licitaciones;
