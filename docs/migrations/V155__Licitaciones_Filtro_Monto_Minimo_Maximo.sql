-- V155: Documentación de filtro monto ya aplicado en V151
-- Esta migración es idempotente y sirve para historial/auditoría.
-- Verificar estado real en BD antes de aplicar en producción.
--
-- CONTEXTO:
-- V151 ya agregó los parámetros p_monto_desde y p_monto_hasta a usp_Licitaciones_Listar
-- junto con la lógica de filtrado WHERE y ORDER BY.
-- Esta migración NO modifica la BD si V151 ya se aplicó (idempotente).
--
-- Si V151 NO se aplicó en algún entorno (p.ej. BD fresh), esta migración
-- asegura que la firma de 14 parámetros exista.

-- NOTA: V155 es NO-OP intencional. La fuente canónica es V151 en src/MPM.Api/Database/Scripts.
-- Este archivo existe solo para historial documental y para que _migrations no falle si alguien
-- espera V155 en una BD ya migrada con V151. No crea lógica divergente (ver governance.md DEBT-001).
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_proc 
        WHERE proname = 'usp_licitaciones_listar' 
        AND pg_get_function_identity_arguments(oid) LIKE '%p_monto_desde%'
    ) THEN
        RAISE NOTICE 'V155 NoOp: usp_Licitaciones_Listar con p_monto_desde no existe. Asegúrate de aplicar V151 (src/MPM.Api/Database/Scripts/V151__Licitaciones_Filtro_Monto.sql) que es la fuente canónica. Este archivo NO crea la función para evitar divergencia (RecordStatus/codigo_area).';
        -- No se crea función aquí a propósito. Si estás en una BD fresca, aplica V151 directamente.
    ELSE
        RAISE NOTICE 'V155 NoOp: usp_Licitaciones_Listar ya tiene p_monto_desde/p_monto_hasta (V151 aplicada). Nada que hacer.';
    END IF;
END $$;

-- ============================================================
-- TEST DE VERIFICACIÓN MANUAL (ejecutar en psql / DBeaver):
-- ============================================================
-- 1. Verificar que la función existe con 14 parámetros:
-- SELECT pg_get_function_identity_arguments(oid) 
-- FROM pg_proc WHERE proname = 'usp_licitaciones_listar';

-- 2. Test filtro monto mínimo (solo licitaciones >= 50M):
-- SELECT * FROM usp_Licitaciones_Listar(1, 5, NULL, NULL, NULL, NULL, NULL, NULL, 'fecha_publicacion', 'desc', NULL, NULL, 50000000, NULL);
-- Debe retornar solo licitaciones con monto_estimado >= 50000000

-- 3. Test filtro monto máximo (solo licitaciones <= 100M):
-- SELECT * FROM usp_Licitaciones_Listar(1, 5, NULL, NULL, NULL, NULL, NULL, NULL, 'fecha_publicacion', 'desc', NULL, NULL, NULL, 100000000);

-- 4. Test rango completo (50M a 100M):
-- SELECT * FROM usp_Licitaciones_Listar(1, 5, NULL, NULL, NULL, NULL, NULL, NULL, 'fecha_publicacion', 'desc', NULL, NULL, 50000000, 100000000);

-- 5. Test combinado con búsqueda y orden por monto:
-- SELECT * FROM usp_Licitaciones_Listar(1, 10, 'construcción', NULL, NULL, NULL, NULL, NULL, 'monto_estimado', 'asc', NULL, NULL, 1000000, 50000000);