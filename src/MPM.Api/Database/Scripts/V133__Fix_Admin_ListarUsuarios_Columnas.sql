-- V133: Fix de mapeo Dapper en usp_Admin_ListarUsuarios (V131).
-- Las columnas de RETURNS TABLE usaban prefijo "p_" (p_id, p_email...); Dapper mapea
-- por nombre de columna → propiedad, así que "p_total_count" no mapeaba a TotalCount
-- (y el resto quedaba vacío). Se redefinen las columnas OUT sin prefijo, igual que
-- se hizo en usp_Admin_ListarLogs (V132). Los parámetros IN (p_search/p_pagina/
-- p_pagina_size) no colisionan con los nuevos nombres de salida.
-- Se requiere DROP previo: CREATE OR REPLACE no permite cambiar el tipo de retorno.

DROP FUNCTION IF EXISTS usp_Admin_ListarUsuarios(VARCHAR, INT, INT);

CREATE OR REPLACE FUNCTION usp_Admin_ListarUsuarios(
    p_search VARCHAR(200) DEFAULT NULL,
    p_pagina INT DEFAULT 1,
    p_pagina_size INT DEFAULT 20
)
RETURNS TABLE(
    id BIGINT,
    email VARCHAR(255),
    nombre VARCHAR(200),
    roles TEXT[],
    activo BOOLEAN,
    ultimo_login TIMESTAMP,
    tenant_nombre VARCHAR(200),
    es_account_manager BOOLEAN,
    total_count BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        u.id,
        u.email,
        u.nombre,
        u.roles,
        u.activo,
        u.ultimo_login,
        u.tenant_nombre,
        EXISTS (
            SELECT 1 FROM alertas_destinatarios d
            WHERE d.usuario_id = u.id::VARCHAR AND d.es_account_manager_gobierno
        ) AS es_account_manager,
        COUNT(*) OVER() AS total_count
    FROM usuarios u
    WHERE u.deleted_at IS NULL
      AND (p_search IS NULL OR TRIM(p_search) = ''
           OR u.nombre ILIKE '%' || p_search || '%'
           OR u.email ILIKE '%' || p_search || '%')
    ORDER BY u.nombre
    LIMIT p_pagina_size OFFSET (p_pagina - 1) * p_pagina_size;
END;
$$ LANGUAGE plpgsql;
