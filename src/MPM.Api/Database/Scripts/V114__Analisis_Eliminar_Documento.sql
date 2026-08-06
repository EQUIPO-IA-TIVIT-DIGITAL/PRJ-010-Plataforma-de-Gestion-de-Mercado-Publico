-- V114__Analisis_Eliminar_Documento.sql
-- Procedure to soft-delete a document in a workspace by setting record_status = 0.
-- Also resets the workspace state to 'pendiente' if no active documents remain.

CREATE OR REPLACE PROCEDURE usp_AnalisisDocumentos_Eliminar(
    p_id BIGINT,
    p_workspace_id BIGINT,
    INOUT p_error_msg TEXT
) LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM analisis_documentos WHERE id = p_id AND workspace_id = p_workspace_id AND record_status = 1) THEN
        p_error_msg := 'ANA_006:Documento no encontrado o ya eliminado';
        RETURN;
    END IF;

    -- Soft delete the document
    UPDATE analisis_documentos
    SET record_status = 0
    WHERE id = p_id AND workspace_id = p_workspace_id;

    -- If no active documents remain, reset the workspace state to 'pendiente'
    IF NOT EXISTS (SELECT 1 FROM analisis_documentos WHERE workspace_id = p_workspace_id AND record_status = 1) THEN
        UPDATE analisis_workspaces
        SET estado = 'pendiente',
            updated_at = CURRENT_TIMESTAMP
        WHERE id = p_workspace_id;
    END IF;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;
