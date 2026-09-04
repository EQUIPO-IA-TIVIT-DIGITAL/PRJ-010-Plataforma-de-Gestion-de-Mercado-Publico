# Guía Migración Prod: V152 Search Vector con Descripción

## Contexto
- **Problema**: Filtro búsqueda solo indexa `nombre + codigo_externo` → nombres engañosos ("papas" = en realidad "camotes")
- **Solución**: Agregar `descripcion` al índice GIN `search_vector` con pesos (nombre=A, descripcion=B, codigo=C)
- **Datos prod**: ~180k filas en tabla `licitaciones`
- **Referencia**: Capacitación 14/08/2026 min 13:12-14:30

---

## Script Prod (CONCURRENTLY - Cero Downtime)

```sql
-- 1. Crear nuevo índice CONCURRENTLY (no bloquea lecturas/escrituras)
-- Tarda ~2-3 min wall-clock en 180k filas, pero producción sigue operativa
CREATE INDEX CONCURRENTLY idx_licitaciones_search_vector_v2
ON licitaciones
USING gin (
    setweight(to_tsvector('spanish', coalesce(nombre,'')), 'A') ||
    setweight(to_tsvector('spanish', coalesce(descripcion,'')), 'B') ||
    setweight(to_tsvector('spanish', coalesce(codigo_externo,'')), 'C')
)
WHERE deleted_at IS NULL;

-- 2. Verificar que se construyó OK (debe mostrar 'valid' en pg_index)
SELECT indexrelid::regclass, indisvalid 
FROM pg_index 
WHERE indexrelid = 'idx_licitaciones_search_vector_v2'::regclass;

-- 3. Swap atómico (milliseconds)
DROP INDEX CONCURRENTLY idx_licitaciones_search_vector;
ALTER INDEX idx_licitaciones_search_vector_v2 RENAME TO idx_licitaciones_search_vector;

-- 4. Verificar swap
SELECT indexrelid::regclass, indisvalid 
FROM pg_index 
WHERE indexrelid = 'idx_licitaciones_search_vector'::regclass;
```

---

## Rollback Inmediato (si algo falla)

```sql
-- Si el nuevo índice tiene problemas:
DROP INDEX CONCURRENTLY IF EXISTS idx_licitaciones_search_vector_v2;
-- El índice original sigue intacto si no hiciste el DROP/RENAME
```

---

## Checklist Pre-Migración Prod

| Item | Responsable | Hecho |
|------|-------------|-------|
| Snapshot BD (RDS/Cloud SQL) | DBA | ☐ |
| Ventana mantenimiento comunicada | Team Lead | ☐ |
| Script probado en staging (mismo volumen aprox) | Dev | ☐ |
| Monitoreo `pg_stat_progress_create_index` preparado | DBA | ☐ |
| Rollback plan revisado | Team | ☐ |

---

## Monitoreo Durante Migración

```sql
-- Progreso build índice (ejecutar cada 30s)
SELECT 
    phase, 
    round(100.0 * blocks_done / nullif(blocks_total,0), 2) as pct_done,
    pg_size_pretty(index_size) as index_size
FROM pg_stat_progress_create_index
WHERE index_relid = 'idx_licitaciones_search_vector_v2'::regclass;
```

---

## Validación Post-Migración

```sql
-- 1. Query test: buscar "camotes" debe encontrar licitación "papas"
SELECT * FROM usp_Licitaciones_Listar(1,20,'camotes',null,null,null,null,null,'fecha_publicacion','desc',null,null);

-- 2. EXPLAIN: debe usar Index Scan (no Seq Scan)
EXPLAIN ANALYZE SELECT * FROM usp_Licitaciones_Listar(1,20,'camotes',null,null,null,null,null,'fecha_publicacion','desc',null,null);

-- 3. Regresiones: código, nombre, monto siguen funcionando
SELECT * FROM usp_Licitaciones_Listar(1,5,'948354',null,null,null,null,null,'fecha_publicacion','desc',null,null);
SELECT * FROM usp_Licitaciones_Listar(1,5,'DETERGENTES',null,null,null,null,null,'fecha_publicacion','desc',null,null);
```

---

## Notas Técnicas

| Parámetro | Valor | Justificación |
|-----------|-------|---------------|
| Peso A | `nombre` | Match exacto en título = mayor relevancia |
| Peso B | `descripcion` | Contenido en bases/anexos = relevancia media |
| Peso C | `codigo_externo` | Código exacto = menor relevancia (ya es único) |
| `websearch_to_tsquery` | SP actual | Respeta pesos automáticamente |
| `WHERE deleted_at IS NULL` | Índice parcial | Excluye soft-deletes, ahorra espacio |

---

## Tiempo Estimado Prod (180k filas)

| Fase | Tiempo |
|------|--------|
| `CREATE INDEX CONCURRENTLY` | ~2-3 min |
| `DROP INDEX CONCURRENTLY` (viejo) | ~10-30s |
| `ALTER INDEX RENAME` | <1s |
| **Total wall-clock** | **~3-4 min** |
| **Downtime real** | **0s** (CONCURRENTLY) |

---

## Archivos Relacionados

- Migración local (dev/staging): `src/MPM.Api/Database/Scripts/V152__Search_Vector_Incluye_Descripcion.sql`
- SP que usa el índice: `src/MPM.Api/Database/Scripts/V137__Fix_Listar_Area_Materializada.sql` (L81 `search_vector @@ v_query`)
- Placeholder UI: `src/mpm-web/src/components/LicitacionFilterBar.tsx` L70