-- V079: Módulo de Alertas Inteligentes por Palabras Clave (spec 003-fase6-alertas-keywords)

CREATE TABLE IF NOT EXISTS alertas_reglas (
    id BIGSERIAL PRIMARY KEY,
    usuario_id VARCHAR(100) NOT NULL,
    keyword VARCHAR(200) NOT NULL,
    sinonimos_ia JSONB,
    monto_minimo NUMERIC,
    monto_maximo NUMERIC,
    tipos_licitacion TEXT[],
    organismos TEXT[],
    activa BOOLEAN NOT NULL DEFAULT TRUE,
    notificar_telegram BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    record_status SMALLINT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS alertas_disparadas (
    id BIGSERIAL PRIMARY KEY,
    regla_id BIGINT NOT NULL REFERENCES alertas_reglas(id),
    licitacion_id BIGINT NOT NULL REFERENCES licitaciones(id),
    termino_match VARCHAR(200),
    resumen_enriquecido JSONB,
    notificacion_inapp_id BIGINT,
    notificacion_telegram_enviada BOOLEAN NOT NULL DEFAULT FALSE,
    notificacion_telegram_error TEXT,
    es_prueba BOOLEAN NOT NULL DEFAULT FALSE,
    disparada_en TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (regla_id, licitacion_id)
);

CREATE TABLE IF NOT EXISTS alertas_destinatarios (
    id BIGSERIAL PRIMARY KEY,
    usuario_id VARCHAR(100) NOT NULL UNIQUE,
    telegram_chat_id VARCHAR(50),
    es_account_manager_gobierno BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_alertas_reglas_usuario ON alertas_reglas(usuario_id) WHERE record_status = 1;
CREATE INDEX IF NOT EXISTS idx_alertas_reglas_activa ON alertas_reglas(activa) WHERE record_status = 1;
CREATE INDEX IF NOT EXISTS idx_alertas_disparadas_regla ON alertas_disparadas(regla_id);
CREATE INDEX IF NOT EXISTS idx_alertas_disparadas_licitacion ON alertas_disparadas(licitacion_id);

-- ── CRUD de reglas ──────────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION usp_Alertas_Crear(
    p_usuario_id VARCHAR(100),
    p_keyword VARCHAR(200),
    p_monto_minimo NUMERIC,
    p_monto_maximo NUMERIC,
    p_tipos_licitacion TEXT[],
    p_organismos TEXT[],
    p_notificar_telegram BOOLEAN
)
RETURNS TABLE(p_id BIGINT) AS $$
BEGIN
    RETURN QUERY
    INSERT INTO alertas_reglas
        (usuario_id, keyword, monto_minimo, monto_maximo, tipos_licitacion, organismos, notificar_telegram)
    VALUES
        (p_usuario_id, p_keyword, p_monto_minimo, p_monto_maximo, p_tipos_licitacion, p_organismos, p_notificar_telegram)
    RETURNING id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_Alertas_GuardarSinonimos(
    p_id BIGINT,
    p_sinonimos_ia TEXT
)
RETURNS VOID AS $$
BEGIN
    UPDATE alertas_reglas SET sinonimos_ia = p_sinonimos_ia::jsonb, updated_at = CURRENT_TIMESTAMP WHERE id = p_id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_Alertas_Editar(
    p_id BIGINT,
    p_usuario_id VARCHAR(100),
    p_keyword VARCHAR(200),
    p_monto_minimo NUMERIC,
    p_monto_maximo NUMERIC,
    p_tipos_licitacion TEXT[],
    p_organismos TEXT[],
    p_notificar_telegram BOOLEAN
)
RETURNS TABLE(p_error_msg TEXT) AS $$
BEGIN
    UPDATE alertas_reglas
    SET keyword = p_keyword,
        monto_minimo = p_monto_minimo,
        monto_maximo = p_monto_maximo,
        tipos_licitacion = p_tipos_licitacion,
        organismos = p_organismos,
        notificar_telegram = p_notificar_telegram,
        updated_at = CURRENT_TIMESTAMP
    WHERE id = p_id AND usuario_id = p_usuario_id AND record_status = 1;

    IF NOT FOUND THEN
        RETURN QUERY SELECT 'ALT_001: Regla no encontrada o no pertenece al usuario'::TEXT;
    ELSE
        RETURN QUERY SELECT NULL::TEXT;
    END IF;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_Alertas_Listar(
    p_usuario_id VARCHAR(100)
)
RETURNS TABLE(
    p_id BIGINT, p_keyword VARCHAR(200), p_sinonimos_ia JSONB, p_monto_minimo NUMERIC,
    p_monto_maximo NUMERIC, p_tipos_licitacion TEXT[], p_organismos TEXT[],
    p_activa BOOLEAN, p_notificar_telegram BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    SELECT r.id, r.keyword, r.sinonimos_ia, r.monto_minimo, r.monto_maximo,
           r.tipos_licitacion, r.organismos, r.activa, r.notificar_telegram
    FROM alertas_reglas r
    WHERE r.usuario_id = p_usuario_id AND r.record_status = 1
    ORDER BY r.created_at DESC;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_Alertas_ListarActivas()
RETURNS TABLE(
    p_id BIGINT, p_usuario_id VARCHAR(100), p_keyword VARCHAR(200), p_sinonimos_ia JSONB,
    p_monto_minimo NUMERIC, p_monto_maximo NUMERIC, p_tipos_licitacion TEXT[], p_organismos TEXT[],
    p_notificar_telegram BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    SELECT r.id, r.usuario_id, r.keyword, r.sinonimos_ia, r.monto_minimo, r.monto_maximo,
           r.tipos_licitacion, r.organismos, r.notificar_telegram
    FROM alertas_reglas r
    WHERE r.activa = TRUE AND r.record_status = 1;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_Alertas_Toggle(
    p_id BIGINT,
    p_usuario_id VARCHAR(100)
)
RETURNS TABLE(p_activa BOOLEAN, p_error_msg TEXT) AS $$
DECLARE
    v_activa BOOLEAN;
BEGIN
    SELECT activa INTO v_activa FROM alertas_reglas WHERE id = p_id AND usuario_id = p_usuario_id AND record_status = 1;

    IF NOT FOUND THEN
        RETURN QUERY SELECT NULL::BOOLEAN, 'ALT_001: Regla no encontrada o no pertenece al usuario'::TEXT;
        RETURN;
    END IF;

    UPDATE alertas_reglas SET activa = NOT v_activa, updated_at = CURRENT_TIMESTAMP WHERE id = p_id;

    RETURN QUERY SELECT NOT v_activa, NULL::TEXT;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_Alertas_Eliminar(
    p_id BIGINT,
    p_usuario_id VARCHAR(100)
)
RETURNS TABLE(p_error_msg TEXT) AS $$
BEGIN
    UPDATE alertas_reglas SET record_status = 0, updated_at = CURRENT_TIMESTAMP
    WHERE id = p_id AND usuario_id = p_usuario_id AND record_status = 1;

    IF NOT FOUND THEN
        RETURN QUERY SELECT 'ALT_001: Regla no encontrada o no pertenece al usuario'::TEXT;
    ELSE
        RETURN QUERY SELECT NULL::TEXT;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- ── Alertas disparadas ──────────────────────────────────────────────────────

CREATE OR REPLACE FUNCTION usp_AlertasDisparadas_ExisteParaLicitacion(
    p_regla_id BIGINT,
    p_licitacion_id BIGINT
)
RETURNS TABLE(p_existe BOOLEAN) AS $$
BEGIN
    RETURN QUERY
    SELECT EXISTS(
        SELECT 1 FROM alertas_disparadas
        WHERE regla_id = p_regla_id AND licitacion_id = p_licitacion_id
    );
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_AlertasDisparadas_Registrar(
    p_regla_id BIGINT,
    p_licitacion_id BIGINT,
    p_termino_match VARCHAR(200),
    p_resumen_enriquecido TEXT,
    p_notificacion_inapp_id BIGINT,
    p_es_prueba BOOLEAN
)
RETURNS TABLE(p_id BIGINT) AS $$
BEGIN
    RETURN QUERY
    INSERT INTO alertas_disparadas
        (regla_id, licitacion_id, termino_match, resumen_enriquecido, notificacion_inapp_id, es_prueba)
    VALUES
        (p_regla_id, p_licitacion_id, p_termino_match, p_resumen_enriquecido::jsonb, p_notificacion_inapp_id, p_es_prueba)
    ON CONFLICT (regla_id, licitacion_id) DO NOTHING
    RETURNING id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_AlertasDisparadas_MarcarTelegram(
    p_id BIGINT,
    p_enviada BOOLEAN,
    p_error TEXT
)
RETURNS VOID AS $$
BEGIN
    UPDATE alertas_disparadas
    SET notificacion_telegram_enviada = p_enviada, notificacion_telegram_error = p_error
    WHERE id = p_id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_AlertasDisparadas_Historial(
    p_regla_id BIGINT,
    p_page INT,
    p_page_size INT
)
RETURNS TABLE(
    p_id BIGINT, p_licitacion_id BIGINT, p_termino_match VARCHAR(200),
    p_resumen_enriquecido JSONB, p_es_prueba BOOLEAN, p_disparada_en TIMESTAMP,
    p_total_count BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT d.id, d.licitacion_id, d.termino_match, d.resumen_enriquecido, d.es_prueba, d.disparada_en,
           COUNT(*) OVER() AS total_count
    FROM alertas_disparadas d
    WHERE d.regla_id = p_regla_id
    ORDER BY d.disparada_en DESC
    LIMIT p_page_size OFFSET (p_page - 1) * p_page_size;
END;
$$ LANGUAGE plpgsql;

-- ── Destinatarios (account managers de gobierno) ────────────────────────────

CREATE OR REPLACE FUNCTION usp_AlertasDestinatarios_ListarAccountManagers()
RETURNS TABLE(p_usuario_id VARCHAR(100), p_telegram_chat_id VARCHAR(50)) AS $$
BEGIN
    RETURN QUERY
    SELECT d.usuario_id, d.telegram_chat_id
    FROM alertas_destinatarios d
    WHERE d.es_account_manager_gobierno = TRUE;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_AlertasDestinatarios_GuardarChatId(
    p_usuario_id VARCHAR(100),
    p_telegram_chat_id VARCHAR(50)
)
RETURNS VOID AS $$
BEGIN
    INSERT INTO alertas_destinatarios (usuario_id, telegram_chat_id)
    VALUES (p_usuario_id, p_telegram_chat_id)
    ON CONFLICT (usuario_id) DO UPDATE
        SET telegram_chat_id = p_telegram_chat_id, updated_at = CURRENT_TIMESTAMP;
END;
$$ LANGUAGE plpgsql;

-- ── Exponer el id interno en usp_Licitaciones_Listar ────────────────────────
-- El frontend necesita el id interno (bigint) de la licitación para el selector del
-- endpoint de "probar alerta" (003-fase6-alertas-keywords, User Story 5) — hoy
-- usp_Licitaciones_Listar (V006) no lo devuelve. Cambiar RETURNS TABLE requiere DROP
-- primero (Postgres no permite CREATE OR REPLACE si cambia el tipo de retorno).
DROP FUNCTION IF EXISTS usp_Licitaciones_Listar(INT, INT, VARCHAR, SMALLINT, VARCHAR, VARCHAR, DATE, DATE, VARCHAR, VARCHAR);

CREATE OR REPLACE FUNCTION usp_Licitaciones_Listar(
    p_page INT DEFAULT 1,
    p_page_size INT DEFAULT 20,
    p_search VARCHAR DEFAULT NULL,
    p_estado SMALLINT DEFAULT NULL,
    p_tipo VARCHAR DEFAULT NULL,
    p_organismo VARCHAR DEFAULT NULL,
    p_fecha_desde DATE DEFAULT NULL,
    p_fecha_hasta DATE DEFAULT NULL,
    p_sort_by VARCHAR DEFAULT 'fecha_publicacion',
    p_sort_dir VARCHAR DEFAULT 'desc'
)
RETURNS TABLE (
    Id BIGINT,
    CodigoExterno VARCHAR,
    Nombre VARCHAR,
    Tipo VARCHAR,
    CodigoEstado SMALLINT,
    EstadoNombre VARCHAR,
    Organismo VARCHAR,
    FechaPublicacion TIMESTAMP,
    FechaCierre TIMESTAMP,
    MontoEstimado DECIMAL,
    Moneda VARCHAR,
    ItemsCount INT,
    TotalCount BIGINT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_offset INT;
BEGIN
    v_offset := (GREATEST(p_page, 1) - 1) * p_page_size;

    IF p_sort_by IS NULL OR p_sort_by NOT IN ('fecha_publicacion', 'fecha_cierre', 'nombre', 'monto_estimado', 'codigo_externo') THEN
        p_sort_by := 'fecha_publicacion';
    END IF;
    IF p_sort_dir IS NULL OR p_sort_dir NOT IN ('asc', 'desc') THEN
        p_sort_dir := 'desc';
    END IF;

    RETURN QUERY
    SELECT
        l.id AS Id,
        l.codigo_externo AS CodigoExterno,
        l.nombre AS Nombre,
        l.tipo AS Tipo,
        l.codigo_estado AS CodigoEstado,
        e.nombre AS EstadoNombre,
        l.organismo AS Organismo,
        l.fecha_publicacion AS FechaPublicacion,
        l.fecha_cierre AS FechaCierre,
        l.monto_estimado AS MontoEstimado,
        l.moneda AS Moneda,
        (SELECT COUNT(*)::INT FROM licitaciones_items li WHERE li.licitacion_id = l.id) AS ItemsCount,
        COUNT(*) OVER() AS TotalCount
    FROM licitaciones l
    JOIN estados_licitacion e ON e.codigo = l.codigo_estado
    WHERE l.deleted_at IS NULL
      AND (p_search IS NULL OR l.nombre ILIKE '%' || p_search || '%' OR l.codigo_externo ILIKE '%' || p_search || '%')
      AND (p_estado IS NULL OR l.codigo_estado = p_estado)
      AND (p_tipo IS NULL OR l.tipo = p_tipo)
      AND (p_organismo IS NULL OR l.organismo ILIKE '%' || p_organismo || '%')
      AND (p_fecha_desde IS NULL OR l.fecha_publicacion >= p_fecha_desde)
      AND (p_fecha_hasta IS NULL OR l.fecha_publicacion <= (p_fecha_hasta + INTERVAL '1 day'))
    ORDER BY
        CASE WHEN p_sort_by = 'fecha_publicacion' AND p_sort_dir = 'asc' THEN l.fecha_publicacion END ASC NULLS LAST,
        CASE WHEN p_sort_by = 'fecha_publicacion' AND p_sort_dir = 'desc' THEN l.fecha_publicacion END DESC NULLS LAST,
        CASE WHEN p_sort_by = 'fecha_cierre' AND p_sort_dir = 'asc' THEN l.fecha_cierre END ASC NULLS LAST,
        CASE WHEN p_sort_by = 'fecha_cierre' AND p_sort_dir = 'desc' THEN l.fecha_cierre END DESC NULLS LAST,
        CASE WHEN p_sort_by = 'nombre' AND p_sort_dir = 'asc' THEN l.nombre END ASC NULLS LAST,
        CASE WHEN p_sort_by = 'nombre' AND p_sort_dir = 'desc' THEN l.nombre END DESC NULLS LAST,
        CASE WHEN p_sort_by = 'monto_estimado' AND p_sort_dir = 'asc' THEN l.monto_estimado END ASC NULLS LAST,
        CASE WHEN p_sort_by = 'monto_estimado' AND p_sort_dir = 'desc' THEN l.monto_estimado END DESC NULLS LAST,
        CASE WHEN p_sort_by = 'codigo_externo' AND p_sort_dir = 'asc' THEN l.codigo_externo END ASC NULLS LAST,
        CASE WHEN p_sort_by = 'codigo_externo' AND p_sort_dir = 'desc' THEN l.codigo_externo END DESC NULLS LAST
    OFFSET v_offset
    LIMIT p_page_size;
END;
$$;

-- ── Soporte para el motor de matching de Alertas (módulo Licitaciones) ──────
-- Devuelve las licitaciones publicadas desde p_fecha_desde, con los campos mínimos que
-- necesita AlertasMatchingService (id interno + texto para matching + monto/tipo/organismo
-- para los filtros de la regla). Separado de usp_Licitaciones_Listar (que sirve al frontend)
-- para no acoplar cambios de un caso de uso al otro.
CREATE OR REPLACE FUNCTION usp_Licitaciones_ListarParaMatching(
    p_fecha_desde TIMESTAMP
)
RETURNS TABLE(
    p_id BIGINT, p_codigo_externo VARCHAR(50), p_nombre VARCHAR(500), p_descripcion TEXT,
    p_monto_estimado DECIMAL(18,2), p_tipo VARCHAR(30), p_organismo VARCHAR(200)
) AS $$
BEGIN
    RETURN QUERY
    SELECT l.id, l.codigo_externo, l.nombre, l.descripcion, l.monto_estimado, l.tipo, l.organismo
    FROM licitaciones l
    WHERE l.fecha_publicacion >= p_fecha_desde AND l.deleted_at IS NULL;
END;
$$ LANGUAGE plpgsql;
