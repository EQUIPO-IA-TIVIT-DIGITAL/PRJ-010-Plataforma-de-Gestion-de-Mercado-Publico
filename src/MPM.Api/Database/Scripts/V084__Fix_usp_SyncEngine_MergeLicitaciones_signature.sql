-- V012 fue editada despues de ya haber sido aplicada (se le agregaron los parametros OUT
-- p_creados/p_actualizados/p_error_msg que MPM.Modules.Licitaciones.Data.SyncEngineHandler
-- espera), pero el DatabaseInitializer solo compara por numero de version -- como V012 ya
-- estaba marcada como aplicada, el cambio nunca se ejecuto contra la base real, que se quedo
-- con el overload viejo de 1 solo parametro (jsonb). Se elimina ese overload viejo.
--
-- Ademas, igual que V069 (usp_SyncLog_Finalizar), Npgsql envia el string C# como tipo
-- 'text', no 'jsonb', por lo que declarar el parametro como JSONB hace que Postgres no
-- encuentre el procedimiento (42883). Se declara como TEXT y se castea internamente.
DROP PROCEDURE IF EXISTS usp_SyncEngine_MergeLicitaciones(JSONB);
DROP PROCEDURE IF EXISTS usp_SyncEngine_MergeLicitaciones(TEXT);

CREATE OR REPLACE PROCEDURE usp_SyncEngine_MergeLicitaciones(
    p_datos TEXT,
    OUT p_creados INT,
    OUT p_actualizados INT,
    OUT p_error_msg TEXT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_item JSONB;
    v_licitacion_id BIGINT;
BEGIN
    p_creados := 0;
    p_actualizados := 0;

    FOR v_item IN SELECT * FROM jsonb_array_elements(p_datos::jsonb)
    LOOP
        INSERT INTO licitaciones (codigo_externo, nombre, descripcion, codigo_estado, tipo,
                                   organismo, unidad_tecnica, moneda, monto_estimado,
                                   fecha_publicacion, fecha_cierre, fecha_adjudicacion,
                                   fecha_estimada_adjudicacion, link, raw_data)
        VALUES (
            v_item->>'codigo_externo',
            v_item->>'nombre',
            v_item->>'descripcion',
            (v_item->>'codigo_estado')::SMALLINT,
            v_item->>'tipo',
            v_item->>'organismo',
            v_item->>'unidad_tecnica',
            v_item->>'moneda',
            (v_item->>'monto_estimado')::DECIMAL,
            (v_item->>'fecha_publicacion')::TIMESTAMP,
            (v_item->>'fecha_cierre')::TIMESTAMP,
            NULLIF(v_item->>'fecha_adjudicacion', '')::TIMESTAMP,
            NULLIF(v_item->>'fecha_estimada_adjudicacion', '')::TIMESTAMP,
            v_item->>'link',
            v_item->>'raw_data'
        )
        ON CONFLICT (codigo_externo) DO UPDATE SET
            nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            codigo_estado = EXCLUDED.codigo_estado,
            monto_estimado = EXCLUDED.monto_estimado,
            fecha_cierre = EXCLUDED.fecha_cierre,
            fecha_adjudicacion = EXCLUDED.fecha_adjudicacion,
            updated_at = CURRENT_TIMESTAMP,
            deleted_at = NULL,
            raw_data = EXCLUDED.raw_data
        RETURNING id INTO v_licitacion_id;

        IF v_licitacion_id IS NOT NULL THEN
            IF FOUND THEN
                p_actualizados := p_actualizados + 1;
            ELSE
                p_creados := p_creados + 1;
            END IF;
        END IF;

        -- Merge items (delete and reinsert)
        DELETE FROM licitaciones_items WHERE licitacion_id = v_licitacion_id;
        INSERT INTO licitaciones_items (licitacion_id, codigo, nombre, cantidad, unidad_medida, precio_estimado, categoria)
        SELECT v_licitacion_id, (i->>'codigo')::INT, i->>'nombre', (i->>'cantidad')::INT,
               i->>'unidad_medida', NULLIF(i->>'precio_estimado', '')::DECIMAL, i->>'categoria'
        FROM jsonb_array_elements(v_item->'items') AS i;
    END LOOP;

EXCEPTION WHEN OTHERS THEN
    p_error_msg := SQLERRM;
END;
$$;
