CREATE TABLE licitaciones_items (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    licitacion_id BIGINT NOT NULL REFERENCES licitaciones(id) ON DELETE CASCADE,
    codigo INT NOT NULL,
    nombre VARCHAR(500) NOT NULL,
    cantidad INT,
    unidad_medida VARCHAR(50),
    precio_estimado DECIMAL(18,2),
    categoria VARCHAR(100)
);

CREATE INDEX idx_items_licitacion ON licitaciones_items(licitacion_id);
