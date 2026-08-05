-- V128: Completa las entradas "Sin clasificar" de tipos_licitacion (spec 019 — el usuario
-- pidió completarlas tras notar que quedaban vacías en /catalogos).
--
-- Fuentes usadas (búsqueda web, no hay glosario oficial completo de ChileCompra disponible
-- para descargar en un solo documento -- ver research.md de esta spec si se agrega ahí):
--   - https://www.chilecompra.cl/2025/10/se-eliminan-los-tipos-de-licitacion-lq-y-h2-en-mercado-publico/
--     (confirma H2 = Licitación Privada 2.000-5.000 UTM, eliminado 23-10-2025)
--   - Búsqueda web adicional confirma H1 = Licitación Privada < 100 UTM, y una página del
--     propio buscador de mercadopublico.cl usa literalmente el filtro "Licitación Privada
--     entre 100 y 1.000 UTM" sin código explícito -- por el patrón numérico H1/H2, 'H' (sin
--     número) es con alta probabilidad esa categoría intermedia (mismo patrón que LE es la
--     categoría pública intermedia entre L1 y LP).
--   - CI y DC no se encontraron en ningún glosario numérico de montos -- corresponden a
--     mecanismos de compra nuevos introducidos en la reforma de la Ley de Compras (Contratos
--     para la Innovación y Diálogos Competitivos), consistente con el conteo de 0 licitaciones
--     de estos tipos en la base local (mecanismos recientes, poco usados todavía).
--   - 'O' no tiene fuente oficial encontrada, pero el 100% de sus 410 licitaciones reales en
--     la base local son obras públicas (plazas, mejoramientos, habilitación borde costero) --
--     se agrupa junto a B/L/R (códigos legados de una sola letra, mismo patrón ya usado en
--     V108 para esos tres) en vez de inventar una etiqueta específica sin respaldo.
--
-- Confianza: MEDIA para H (inferido por patrón numérico, no confirmado literalmente), ALTA
-- para CI/DC (mecanismos documentados de la reforma), MEDIA para O (inferido por evidencia de
-- datos reales + patrón de código legado, no por fuente oficial). Revisar con el equipo
-- comercial o ChileCompra si en algún momento se necesita certeza absoluta.

UPDATE tipos_licitacion SET nombre = 'Licitación Privada Media', descripcion = 'Licitación privada para contratos entre 100 y 1.000 UTM (equivalente privado de LE) -- inferido por el patrón H1 (<100 UTM) / H2 (2.000-5.000 UTM, eliminado 2025); sin confirmación literal oficial'
WHERE codigo = 'H';

UPDATE tipos_licitacion SET nombre = 'Contrato para la Innovación', descripcion = 'Mecanismo de compra con preselección orientado a soluciones innovadoras, introducido en la reforma de la Ley de Compras'
WHERE codigo = 'CI';

UPDATE tipos_licitacion SET nombre = 'Diálogo Competitivo', descripcion = 'Mecanismo de compra que permite dialogar con proveedores preseleccionados antes de la oferta final, introducido en la reforma de la Ley de Compras'
WHERE codigo = 'DC';

UPDATE tipos_licitacion SET nombre = 'Obras Públicas / Suministros', descripcion = 'Licitaciones enfocadas en infraestructura pública o suministro de insumos complejos (código legado de una sola letra, igual que B/L/R)'
WHERE codigo = 'O';
