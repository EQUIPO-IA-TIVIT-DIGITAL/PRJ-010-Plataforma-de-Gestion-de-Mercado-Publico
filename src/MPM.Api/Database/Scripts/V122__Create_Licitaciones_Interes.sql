-- V122: US5 (spec 031) — flujo colaborativo go/no-go. Ver research.md §5 y
-- contracts/colaboracion-interes.md: no se crean tablas de comentarios/asignación
-- nuevas, se reutilizan conversaciones/conversacion_participantes/mensajes de
-- Mensajería (V013-V015) -- esta tabla solo persiste el vínculo entre licitación,
-- workspace de análisis y conversación.

CREATE TABLE IF NOT EXISTS licitaciones_interes (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    licitacion_id BIGINT NOT NULL UNIQUE REFERENCES licitaciones(id),
    workspace_id BIGINT NULL REFERENCES analisis_workspaces(id),
    conversacion_id BIGINT NULL REFERENCES conversaciones(id),
    marcado_por VARCHAR(100) NOT NULL,
    estado_licitacion_al_marcar SMALLINT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Idempotente: si ya existe una fila para esa licitación, la devuelve sin insertar
-- una segunda (FR-013 -- garantizado a nivel de esquema por el UNIQUE de arriba).
CREATE OR REPLACE FUNCTION usp_LicitacionesInteres_Marcar(
    p_licitacion_id BIGINT,
    p_marcado_por VARCHAR
)
RETURNS TABLE (
    Id BIGINT, LicitacionId BIGINT, WorkspaceId BIGINT, ConversacionId BIGINT,
    MarcadoPor VARCHAR, EstadoLicitacionAlMarcar SMALLINT, EstadoLicitacionActual SMALLINT,
    CreatedAt TIMESTAMP, UpdatedAt TIMESTAMP
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_estado_actual SMALLINT;
BEGIN
    -- "id" sin calificar es ambiguo aquí: RETURNS TABLE(Id BIGINT, ...) declara "Id" (folded a
    -- "id") como variable PL/pgSQL, que colisiona con licitaciones.id -- mismo bug ya documentado
    -- en V113 sobre usp_AnalisisWorkspaces_Listar (V052). Se califica explícitamente.
    SELECT codigo_estado INTO v_estado_actual FROM licitaciones WHERE licitaciones.id = p_licitacion_id;
    IF v_estado_actual IS NULL THEN
        RAISE EXCEPTION 'Licitación % no encontrada', p_licitacion_id;
    END IF;

    INSERT INTO licitaciones_interes (licitacion_id, marcado_por, estado_licitacion_al_marcar)
    VALUES (p_licitacion_id, p_marcado_por, v_estado_actual)
    ON CONFLICT (licitacion_id) DO NOTHING;

    RETURN QUERY
    SELECT li.id, li.licitacion_id, li.workspace_id, li.conversacion_id,
           li.marcado_por, li.estado_licitacion_al_marcar, l.codigo_estado,
           li.created_at, li.updated_at
    FROM licitaciones_interes li
    JOIN licitaciones l ON l.id = li.licitacion_id
    WHERE li.licitacion_id = p_licitacion_id;
END;
$$;

CREATE OR REPLACE FUNCTION usp_LicitacionesInteres_ObtenerPorLicitacion(p_licitacion_id BIGINT)
RETURNS TABLE (
    Id BIGINT, LicitacionId BIGINT, WorkspaceId BIGINT, ConversacionId BIGINT,
    MarcadoPor VARCHAR, EstadoLicitacionAlMarcar SMALLINT, EstadoLicitacionActual SMALLINT,
    CreatedAt TIMESTAMP, UpdatedAt TIMESTAMP
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT li.id, li.licitacion_id, li.workspace_id, li.conversacion_id,
           li.marcado_por, li.estado_licitacion_al_marcar, l.codigo_estado,
           li.created_at, li.updated_at
    FROM licitaciones_interes li
    JOIN licitaciones l ON l.id = li.licitacion_id
    WHERE li.licitacion_id = p_licitacion_id;
END;
$$;

CREATE OR REPLACE FUNCTION usp_LicitacionesInteres_VincularWorkspace(
    p_licitacion_id BIGINT, p_workspace_id BIGINT
)
RETURNS VOID
LANGUAGE sql
AS $$
    UPDATE licitaciones_interes
    SET workspace_id = p_workspace_id, updated_at = CURRENT_TIMESTAMP
    WHERE licitacion_id = p_licitacion_id;
$$;

CREATE OR REPLACE FUNCTION usp_LicitacionesInteres_VincularConversacion(
    p_licitacion_id BIGINT, p_conversacion_id BIGINT
)
RETURNS VOID
LANGUAGE sql
AS $$
    UPDATE licitaciones_interes
    SET conversacion_id = p_conversacion_id, updated_at = CURRENT_TIMESTAMP
    WHERE licitacion_id = p_licitacion_id;
$$;

CREATE OR REPLACE FUNCTION usp_LicitacionesInteres_Listar()
RETURNS TABLE (
    Id BIGINT, LicitacionId BIGINT, LicitacionNombre VARCHAR, WorkspaceId BIGINT, ConversacionId BIGINT,
    MarcadoPor VARCHAR, EstadoLicitacionAlMarcar SMALLINT, EstadoLicitacionActual SMALLINT,
    CreatedAt TIMESTAMP, UpdatedAt TIMESTAMP
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT li.id, li.licitacion_id, l.nombre, li.workspace_id, li.conversacion_id,
           li.marcado_por, li.estado_licitacion_al_marcar, l.codigo_estado,
           li.created_at, li.updated_at
    FROM licitaciones_interes li
    JOIN licitaciones l ON l.id = li.licitacion_id
    ORDER BY li.created_at DESC;
END;
$$;
