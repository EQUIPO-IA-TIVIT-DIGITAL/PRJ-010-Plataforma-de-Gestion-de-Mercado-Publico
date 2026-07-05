-- Migración del módulo Catálogo
-- Seed de tipos de licitación (idempotente)

INSERT INTO tipos_licitacion (codigo, nombre, slug, descripcion) VALUES
    (1, 'Licitación Pública', 'Licitacion', 'Proceso de compra pública completo'),
    (2, 'Trato Directo', 'TratoDirecto', 'Contratación directa con proveedor'),
    (3, 'Convenio Marco', 'ConvenioMarco', 'Acuerdo marco con proveedores'),
    (4, 'Compra Ágil', 'CompraAgil', 'Proceso simplificado de compra')
ON CONFLICT (codigo) DO NOTHING;