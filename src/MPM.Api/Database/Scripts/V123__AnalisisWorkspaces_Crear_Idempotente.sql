-- V123: US5 (spec 031) — para que "marcar de interés" no dispare un segundo análisis
-- cuando ya existe uno para la misma licitación (FR-013), usp_AnalisisWorkspaces_Crear
-- necesita ser idempotente por licitacion_id. Hoy (V052 + V059) siempre hace INSERT sin
-- chequear duplicados -- esto no es exclusivo de Colaboracion, corrige un bug latente
-- de Analisis en general (ver research.md §5 / plan.md T027).
--
-- IMPORTANTE: V059 convirtió esto de FUNCTION (OUT params) a PROCEDURE (INOUT params) --
-- hay que preservar esa forma real, no la de V052. Un CREATE OR REPLACE FUNCTION aquí
-- crearía un objeto nuevo coexistiendo con el PROCEDURE existente ("is not unique" al
-- llamarlo, confirmado en vivo contra Postgres real de docker-compose).
--
-- Solo aplica el chequeo cuando p_licitacion_id NO es NULL -- los workspaces "sueltos"
-- (sin licitación asociada, permitido desde V059) siguen creándose libremente, no hay
-- noción de duplicado para ellos.

CREATE OR REPLACE PROCEDURE usp_AnalisisWorkspaces_Crear(
    p_licitacion_id BIGINT DEFAULT NULL,
    p_nombre VARCHAR(200) DEFAULT '',
    p_user_id VARCHAR(50) DEFAULT '',
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_existente BIGINT;
BEGIN
    IF p_nombre IS NULL OR trim(p_nombre) = '' THEN
        p_error_msg := 'VAL_001:nombre es requerido';
        RETURN;
    END IF;

    IF p_licitacion_id IS NOT NULL THEN
        IF NOT EXISTS (SELECT 1 FROM licitaciones WHERE id = p_licitacion_id AND deleted_at IS NULL) THEN
            p_error_msg := 'VAL_006:licitacionId no encontrado';
            RETURN;
        END IF;

        SELECT aw.id INTO v_existente
        FROM analisis_workspaces aw
        WHERE aw.licitacion_id = p_licitacion_id AND aw.record_status = 1
        LIMIT 1;

        IF v_existente IS NOT NULL THEN
            p_id := v_existente;
            p_error_msg := NULL;
            RETURN;
        END IF;
    END IF;

    INSERT INTO analisis_workspaces (licitacion_id, nombre, user_id)
    VALUES (p_licitacion_id, trim(p_nombre), p_user_id)
    RETURNING id INTO p_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;
