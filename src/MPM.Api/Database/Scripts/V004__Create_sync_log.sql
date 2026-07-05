CREATE TABLE sync_log (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tipo VARCHAR(10) NOT NULL,
    registros_procesados INT DEFAULT 0,
    creados INT DEFAULT 0,
    actualizados INT DEFAULT 0,
    eliminados INT DEFAULT 0,
    errores INT DEFAULT 0,
    detalle_errores JSONB,
    ejecutado_en TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    estado VARCHAR(10) NOT NULL
);
