-- V108 (027-catalogo-frontend-licitaciones-generales): repuebla tipos_licitacion con los
-- códigos reales del portal (glosario de 026-robustez-sincronizacion-tipos-reales) en vez de
-- las 4 categorías genéricas originales, y filtra usp_Catalogos_EstadosLicitacion() a los 5
-- códigos vigentes (5,6,7,8,15) sin tocar la tabla estados_licitacion ni las 144 licitaciones
-- que usan el código heredado 1 como fallback intencional (spec 026, FR-007) -- ver
-- research.md de esta spec para el detalle de por qué no se borran filas de estados_licitacion.

-- Debe dropearse antes de recrear: CREATE OR REPLACE FUNCTION no permite cambiar el tipo de
-- una columna de RETURNS TABLE (codigo pasa de SMALLINT a VARCHAR).
DROP FUNCTION IF EXISTS usp_Catalogos_TiposLicitacion();

ALTER TABLE tipos_licitacion ALTER COLUMN codigo TYPE VARCHAR(10) USING codigo::text;

TRUNCATE TABLE tipos_licitacion;

INSERT INTO tipos_licitacion (codigo, nombre, slug, descripcion) VALUES
    ('LE', 'Licitación Pública Menor', 'le', 'Convocatoria pública abierta para contratos menores a 100 UTM'),
    ('LP', 'Licitación Pública Media', 'lp', 'Convocatoria estándar para contratos medianos de bienes o servicios (100 - 1.000 UTM)'),
    ('LQ', 'Licitación Pública Mayor', 'lq', 'Convocatoria pública para contratos de volumen intermedio alto (1.000 - 2.000 UTM)'),
    ('LR', 'Licitación Pública Grande', 'lr', 'Convocatorias públicas complejas para grandes contratos gubernamentales (> 2.000 UTM)'),
    ('CO', 'Convenio Marco', 'co', 'Tienda virtual de ChileCompra donde los organismos adquieren directamente de un catálogo pre-adjudicado'),
    ('CA', 'Compra Ágil', 'ca', 'Proceso simplificado de cotizaciones en línea, dirigido a compras de menor cuantía (<= 30 UTM)'),
    ('TD', 'Trato Directo', 'td', 'Contratación directa sin licitación por causales fundadas en la ley'),
    ('LS', 'Licitación de Servicios', 'ls', 'Contratación de servicios de consultoría, asesorías de software, auditorías o servicios profesionales'),
    ('L', 'Obras Públicas / Suministros', 'l', 'Licitaciones enfocadas en infraestructura pública o suministro de insumos complejos'),
    ('B', 'Obras Públicas / Suministros', 'b', 'Licitaciones enfocadas en infraestructura pública o suministro de insumos complejos'),
    ('R', 'Obras Públicas / Suministros', 'r', 'Licitaciones enfocadas en infraestructura pública o suministro de insumos complejos'),
    ('E', 'Especiales / Internacionales', 'e', 'Convocatorias de organismos multilaterales o licitaciones con bases especiales de financiamiento'),
    ('I', 'Especiales / Internacionales', 'i', 'Convocatorias de organismos multilaterales o licitaciones con bases especiales de financiamiento'),
    ('O', 'Sin clasificar', 'o', 'Pendiente de documentar'),
    ('H', 'Sin clasificar', 'h', 'Pendiente de documentar'),
    ('CI', 'Sin clasificar', 'ci', 'Pendiente de documentar'),
    ('DC', 'Sin clasificar', 'dc', 'Pendiente de documentar');

CREATE FUNCTION usp_Catalogos_TiposLicitacion()
RETURNS TABLE(codigo VARCHAR(10), nombre VARCHAR(50), slug VARCHAR(30))
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY SELECT t.codigo, t.nombre, t.slug FROM tipos_licitacion t ORDER BY t.codigo;
END;
$$;

CREATE OR REPLACE FUNCTION usp_Catalogos_EstadosLicitacion()
RETURNS TABLE(codigo SMALLINT, nombre VARCHAR(50))
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY SELECT e.codigo, e.nombre FROM estados_licitacion e
    WHERE e.codigo IN (5, 6, 7, 8, 15)
    ORDER BY e.codigo;
END;
$$;
