CREATE OR REPLACE PROCEDURE usp_Conversaciones_Crear(
    IN p_tipo VARCHAR(10),
    IN p_asunto VARCHAR(200),
    IN p_licitacion_id BIGINT,
    IN p_participante_ids JSONB,
    IN p_creador_id VARCHAR(100),
    INOUT p_id BIGINT DEFAULT NULL,
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_participante_id VARCHAR(100);
    v_participantes TEXT[];
    v_count INT;
BEGIN
    IF p_tipo = 'directo' THEN
        SELECT ARRAY(
            SELECT jsonb_array_elements_text(p_participante_ids)
            UNION
            SELECT p_creador_id
        ) INTO v_participantes;

        IF array_length(v_participantes, 1) != 2 THEN
            p_error_msg := 'Una conversacion directa debe tener exactamente 2 participantes';
            RETURN;
        END IF;

        SELECT COUNT(*) INTO v_count
        FROM conversaciones c
        WHERE c.tipo = 'directo'
          AND c.deleted_at IS NULL
          AND EXISTS (
              SELECT 1 FROM conversacion_participantes cp1
              WHERE cp1.conversacion_id = c.id
                AND cp1.user_id = v_participantes[1]
                AND cp1.left_at IS NULL
          )
          AND EXISTS (
              SELECT 1 FROM conversacion_participantes cp2
              WHERE cp2.conversacion_id = c.id
                AND cp2.user_id = v_participantes[2]
                AND cp2.left_at IS NULL
          )
          AND (
              SELECT COUNT(*) FROM conversacion_participantes cp3
              WHERE cp3.conversacion_id = c.id AND cp3.left_at IS NULL
          ) = 2;

        IF v_count > 0 THEN
            p_error_msg := 'MSG_002: Ya existe una conversacion directa entre estos usuarios';
            RETURN;
        END IF;
    END IF;

    IF p_licitacion_id IS NOT NULL THEN
        IF NOT EXISTS (SELECT 1 FROM licitaciones WHERE id = p_licitacion_id AND deleted_at IS NULL) THEN
            p_error_msg := 'MSG_004: Licitacion no valida';
            RETURN;
        END IF;
    END IF;

    INSERT INTO conversaciones (tipo, asunto, licitacion_id)
    VALUES (p_tipo, p_asunto, p_licitacion_id)
    RETURNING id INTO p_id;

    INSERT INTO conversacion_participantes (conversacion_id, user_id, rol)
    VALUES (p_id, p_creador_id, 'admin');

    FOR v_participante_id IN SELECT jsonb_array_elements_text(p_participante_ids)
    LOOP
        IF v_participante_id != p_creador_id THEN
            INSERT INTO conversacion_participantes (conversacion_id, user_id, rol)
            VALUES (p_id, v_participante_id, 'miembro')
            ON CONFLICT DO NOTHING;
        END IF;
    END LOOP;

    INSERT INTO mensajes (conversacion_id, user_id, tipo, contenido)
    VALUES (p_id, 'system', 'sistema', 'Conversacion creada');

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
