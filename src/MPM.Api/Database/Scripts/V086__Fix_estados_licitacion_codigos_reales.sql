-- El catalogo estados_licitacion (V005/V036) fue inventado con codigos 1-8 arbitrarios que
-- nunca correspondieron a los codigos reales que devuelve la API de Mercado Publico
-- (api.mercadopublico.cl). El "Diccionario de Datos" oficial de la API no documenta el
-- significado numerico de CodigoEstado, asi que se verifico en vivo contra la API real
-- (con el ticket de produccion, cruzando CodigoEstado con el campo de texto "Estado" del
-- endpoint de detalle) el 2026-07-06:
--   5  -> "Publicada"
--   6  -> "Cerrada"
--   7  -> "Desierta"  (aparece como "Desierta (o art. 3 o 9 Ley 19.886)")
--   8  -> "Adjudicada"
--   15 -> "Revocada"
--
-- Esto tambien corrige datos ya existentes: las 38 licitaciones de TIVIT ya sincronizadas
-- por el scraper usan estos mismos codigos reales (6, 7, 8), y con el catalogo viejo al
-- menos una quedaba mal etiquetada (codigo 8 se mostraba como "En Espera" en vez de
-- "Adjudicada", el estado que necesita ver Francisco).
--
-- Los codigos 1-4 del catalogo original no corresponden a ningun valor observado en datos
-- reales; se dejan intactos (filas sin uso, no rompen nada) por si la API los usa en casos
-- no cubiertos por esta muestra.
UPDATE estados_licitacion SET nombre = 'Publicada', descripcion = 'Licitacion publicada y en plazo de recepcion de ofertas' WHERE codigo = 5;
UPDATE estados_licitacion SET nombre = 'Cerrada', descripcion = 'Plazo de recepcion de ofertas cerrado' WHERE codigo = 6;
UPDATE estados_licitacion SET nombre = 'Desierta', descripcion = 'Declarada desierta (sin ofertas validas o art. 3/9 Ley 19.886)' WHERE codigo = 7;
UPDATE estados_licitacion SET nombre = 'Adjudicada', descripcion = 'Adjudicada a uno o mas proveedores' WHERE codigo = 8;

INSERT INTO estados_licitacion (codigo, nombre, descripcion)
VALUES (15, 'Revocada', 'Revocada por el organismo comprador')
ON CONFLICT (codigo) DO NOTHING;
