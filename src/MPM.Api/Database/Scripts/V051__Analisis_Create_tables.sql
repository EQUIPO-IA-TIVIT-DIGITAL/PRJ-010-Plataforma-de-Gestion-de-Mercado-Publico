CREATE TABLE IF NOT EXISTS analisis_workspaces (
    id BIGSERIAL PRIMARY KEY,
    licitacion_id BIGINT NOT NULL REFERENCES licitaciones(id),
    nombre VARCHAR(200) NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'pendiente'
        CHECK (estado IN ('pendiente', 'listo', 'analizando', 'completado', 'error')),
    last_analyzed_at TIMESTAMP,
    user_id VARCHAR(50) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    record_status SMALLINT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS analisis_documentos (
    id BIGSERIAL PRIMARY KEY,
    workspace_id BIGINT NOT NULL REFERENCES analisis_workspaces(id),
    nombre_archivo VARCHAR(500) NOT NULL,
    mime_type VARCHAR(100) NOT NULL,
    tamanio_bytes BIGINT NOT NULL,
    ruta_storage TEXT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    record_status SMALLINT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS analisis_resultados (
    id BIGSERIAL PRIMARY KEY,
    workspace_id BIGINT NOT NULL REFERENCES analisis_workspaces(id),
    documento_id BIGINT NOT NULL REFERENCES analisis_documentos(id),
    contenido_json JSONB NOT NULL,
    modelo_usado VARCHAR(100) NOT NULL DEFAULT 'gemini-2.0-flash',
    tokens_entrada INTEGER DEFAULT 0,
    tokens_salida INTEGER DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS analisis_chat_conversaciones (
    id BIGSERIAL PRIMARY KEY,
    workspace_id BIGINT NOT NULL REFERENCES analisis_workspaces(id),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    record_status SMALLINT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS analisis_chat_mensajes (
    id BIGSERIAL PRIMARY KEY,
    conversacion_id BIGINT NOT NULL REFERENCES analisis_chat_conversaciones(id),
    rol VARCHAR(10) NOT NULL CHECK (rol IN ('user', 'assistant')),
    contenido TEXT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_analisis_workspaces_licitacion ON analisis_workspaces(licitacion_id);
CREATE INDEX IF NOT EXISTS idx_analisis_workspaces_estado ON analisis_workspaces(estado);
CREATE INDEX IF NOT EXISTS idx_analisis_workspaces_user ON analisis_workspaces(user_id);
CREATE INDEX IF NOT EXISTS idx_analisis_documentos_workspace ON analisis_documentos(workspace_id);
CREATE INDEX IF NOT EXISTS idx_analisis_resultados_workspace ON analisis_resultados(workspace_id);
CREATE INDEX IF NOT EXISTS idx_analisis_chat_conversaciones_workspace ON analisis_chat_conversaciones(workspace_id);
CREATE INDEX IF NOT EXISTS idx_analisis_chat_mensajes_conversacion ON analisis_chat_mensajes(conversacion_id);
