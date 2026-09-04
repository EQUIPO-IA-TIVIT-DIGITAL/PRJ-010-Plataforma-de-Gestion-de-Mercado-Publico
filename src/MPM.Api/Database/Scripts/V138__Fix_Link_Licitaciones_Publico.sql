-- V138: corrige el link publico de la ficha de Mercado Publico.
-- Antes: ?qs=/<codigo> -> vista interna que exige sesion y pertenencia a la unidad de la
-- ficha ("No tiene los permisos suficientes" / "No pertenece a la unidad de la ficha").
-- Ahora: ?idlicitacion=<codigo> -> ficha PUBLICA, sin login (verificado en vivo 2026-08-13).
-- Actualiza las filas ya persistidas; el codigo nuevo queda corregido en ApiMpService/LicitacionService.

UPDATE licitaciones
SET link = replace(link, '/Procurement/Modules/RFB/DetailsAcquisition.aspx?qs=/', '/Procurement/Modules/RFB/DetailsAcquisition.aspx?idlicitacion=')
WHERE link LIKE '%/Procurement/Modules/RFB/DetailsAcquisition.aspx?qs=/%';

-- raw_data tambien puede contener el link viejo (snapshot del JSON original) -- no se
-- reescribe: es un snapshot historico y LicitacionHandler lee 'link' de la columna link.
