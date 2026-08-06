-- V118: Catálogo de áreas de negocio (spec 031 — feedback ChileCompra)
-- US1/US2: clasificación de licitaciones por área (Cloud, Ciberseguridad, Digital)
-- calculada en consulta contra licitaciones.search_vector (V066), sin columna nueva
-- en licitaciones ni job de reclasificación (ver research.md §1 de la spec 031).

CREATE TABLE IF NOT EXISTS areas_negocio (
    codigo SMALLINT PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL,
    palabras_clave TEXT[] NOT NULL DEFAULT '{}',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO areas_negocio (codigo, nombre, palabras_clave) VALUES
    (1, 'Cloud', ARRAY[
        'cloud', 'nube', 'computación en la nube', 'servicios en la nube',
        'aws', 'amazon web services', 'google cloud', 'gcp', 'azure',
        'infraestructura como servicio', 'iaas', 'paas', 'saas',
        'centro de datos', 'data center', 'servidores', 'hosting',
        'almacenamiento en la nube', 'migración a la nube'
    ]),
    (2, 'Ciberseguridad', ARRAY[
        'ciberseguridad', 'seguridad informática', 'seguridad de la información',
        'soc', 'centro de operaciones de seguridad', 'firewall',
        'antivirus', 'antimalware', 'pentesting', 'test de penetración',
        'vulnerabilidades', 'ciberataque', 'ransomware', 'phishing',
        'ciberinteligencia', 'monitoreo de seguridad', 'siem',
        'autenticación', 'cifrado', 'encriptación', 'respaldo de datos', 'backup'
    ]),
    (3, 'Digital', ARRAY[
        'transformación digital', 'desarrollo de software', 'aplicación web',
        'aplicación móvil', 'sistema web', 'plataforma digital',
        'portal web', 'automatización de procesos', 'inteligencia artificial',
        'analítica de datos', 'business intelligence', 'digitalización',
        'sistema de gestión', 'integración de sistemas', 'api'
    ])
ON CONFLICT (codigo) DO NOTHING;

CREATE OR REPLACE FUNCTION usp_Catalogos_AreasNegocio()
RETURNS TABLE(codigo SMALLINT, nombre VARCHAR(50))
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY SELECT a.codigo, a.nombre FROM areas_negocio a ORDER BY a.codigo;
END;
$$;

-- Función compartida: dado el search_vector de una licitación, devuelve los códigos
-- de área con los que calza (léxicamente, vía plainto_tsquery). Reutilizada por
-- usp_Licitaciones_Listar (V119, US1) y usp_Licitaciones_ContarPorEstado (V121, US2)
-- para no duplicar la lógica de matching.
CREATE OR REPLACE FUNCTION fn_licitacion_area_codigos(p_search_vector TSVECTOR)
RETURNS SMALLINT[]
LANGUAGE sql
STABLE
AS $$
    SELECT COALESCE(array_agg(an.codigo ORDER BY an.codigo), '{}')
    FROM areas_negocio an
    WHERE p_search_vector IS NOT NULL
      AND EXISTS (
        SELECT 1 FROM unnest(an.palabras_clave) kw
        WHERE p_search_vector @@ plainto_tsquery('spanish', kw)
      );
$$;
