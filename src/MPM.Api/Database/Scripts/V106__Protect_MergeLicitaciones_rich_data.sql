-- V106: Update usp_SyncEngine_MergeLicitaciones to protect rich scraper data and apply fallback values
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
        BEGIN
            INSERT INTO licitaciones (codigo_externo, nombre, descripcion, codigo_estado, tipo,
                                       organismo, unidad_tecnica, moneda, monto_estimado,
                                       fecha_publicacion, fecha_cierre, fecha_adjudicacion,
                                       fecha_estimada_adjudicacion, link, raw_data)
            VALUES (
                v_item->>'codigo_externo',
                v_item->>'nombre',
                v_item->>'descripcion',
                COALESCE((SELECT codigo FROM estados_licitacion WHERE codigo = (v_item->>'codigo_estado')::SMALLINT), 1::SMALLINT),
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
                (v_item->>'raw_data')::JSONB
            )
            ON CONFLICT (codigo_externo) DO UPDATE SET
                nombre = EXCLUDED.nombre,
                descripcion = COALESCE(licitaciones.descripcion, EXCLUDED.descripcion),
                codigo_estado = COALESCE((SELECT codigo FROM estados_licitacion WHERE codigo = EXCLUDED.codigo_estado), 1::SMALLINT),
                tipo = EXCLUDED.tipo,
                fecha_publicacion = COALESCE(licitaciones.fecha_publicacion, EXCLUDED.fecha_publicacion),
                monto_estimado = COALESCE(licitaciones.monto_estimado, EXCLUDED.monto_estimado),
                organismo = COALESCE(licitaciones.organismo, EXCLUDED.organismo),
                unidad_tecnica = COALESCE(licitaciones.unidad_tecnica, EXCLUDED.unidad_tecnica),
                link = COALESCE(licitaciones.link, EXCLUDED.link),
                fecha_cierre = EXCLUDED.fecha_cierre,
                fecha_adjudicacion = COALESCE(licitaciones.fecha_adjudicacion, EXCLUDED.fecha_adjudicacion),
                updated_at = CURRENT_TIMESTAMP,
                deleted_at = NULL,
                raw_data = CASE 
                    -- Conservamos el raw_data si ya tiene detalles de comprador del scraper/detalle de API
                    WHEN licitaciones.raw_data IS NOT NULL AND (licitaciones.raw_data->'Comprador') IS NOT NULL THEN licitaciones.raw_data
                    ELSE EXCLUDED.raw_data
                END
            RETURNING id INTO v_licitacion_id;

            IF v_licitacion_id IS NOT NULL THEN
                IF FOUND THEN
                    p_actualizados := p_actualizados + 1;
                ELSE
                    p_creados := p_creados + 1;
                END IF;
            END IF;

            DELETE FROM licitaciones_items WHERE licitacion_id = v_licitacion_id;
            INSERT INTO licitaciones_items (licitacion_id, codigo, nombre, cantidad, unidad_medida, precio_estimado, categoria)
            SELECT v_licitacion_id, (i->>'codigo')::INT, i->>'nombre', (i->>'cantidad')::INT,
                   i->>'unidad_medida', NULLIF(i->>'precio_estimado', '')::DECIMAL, i->>'categoria'
            FROM jsonb_array_elements(v_item->'items') AS i;
        EXCEPTION WHEN OTHERS THEN
            p_error_msg := COALESCE(p_error_msg || '; ', '') || (v_item->>'codigo_externo') || ': ' || SQLERRM;
        END;
    END LOOP;
END;
$$;
