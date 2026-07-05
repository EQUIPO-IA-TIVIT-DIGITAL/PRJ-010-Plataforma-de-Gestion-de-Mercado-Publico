INSERT INTO estados_licitacion (codigo, nombre, descripcion) VALUES
    (1, 'Publicada',  'Licitación publicada y en plazo de recepción'),
    (2, 'Modificada', 'Licitación modificada durante el proceso'),
    (3, 'Desierta',   'Sin oferentes o declarada desierta'),
    (4, 'Revocada',   'Revocada por el organismo'),
    (5, 'Adjudicada', 'Adjudicada a un proveedor'),
    (6, 'Cerrada',    'Proceso cerrado'),
    (7, 'Con Adjuntos','Requiere revisión de adjuntos'),
    (8, 'En Espera',  'Pendiente de evaluación')
ON CONFLICT (codigo) DO NOTHING;
