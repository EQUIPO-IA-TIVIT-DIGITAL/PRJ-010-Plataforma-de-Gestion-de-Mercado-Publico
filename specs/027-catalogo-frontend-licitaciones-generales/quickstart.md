# Quickstart: Frontend de Licitaciones Alineado al Catálogo Real

**Feature**: `027-catalogo-frontend-licitaciones-generales`

## Prerrequisitos

- Stack local corriendo (`docker compose up --build`, o local con `dotnet run` + `npm run dev`).
- Base con el volumen real de licitaciones generales ya sincronizado (no es necesario para validar el filtro en sí, pero sí para confirmar que devuelve resultados reales en SC-001).

## Escenario 1 — US1: filtro de Tipo funcional (SC-001)

1. Ir a `/licitaciones`, abrir el selector de Tipo.
2. Confirmar que las opciones son códigos reales del glosario (LE, LP, LQ, LR, CO, CA, TD, LS...), no las 4 categorías genéricas anteriores.
3. Seleccionar "LP" (o cualquier otra opción) y confirmar que el listado devuelve resultados reales, no una lista vacía.
4. Verificación directa: `GET /api/v1/catalogos/tipos-licitacion` → confirmar que `codigo` es un string real (`"LP"`, no `1`).

## Escenario 2 — US2: selector de Estado sin duplicados (SC-002)

1. Abrir el selector de Estado en `/licitaciones`.
2. Contar las opciones: deben ser exactamente 5 (Publicada, Cerrada, Desierta, Adjudicada, Revocada), sin ningún nombre repetido.
3. Verificación directa: `GET /api/v1/catalogos/estados-licitacion` → confirmar 5 filas.

## Escenario 3 — US3: tabla sin columnas vacías (SC-003)

1. Ir a `/licitaciones` en modo "Filtros" (tabla normal, no búsqueda inteligente).
2. Confirmar que la tabla NO muestra columnas de Organismo, Monto ni Items.
3. Hacer clic en cualquier licitación general para abrir el detalle.
4. Confirmar que el detalle sí puede mostrar Organismo/Monto/Items si están disponibles (puede requerir el enriquecimiento bajo demanda ya existente — la primera apertura de una licitación sin estos datos puede tardar un poco más mientras se consulta el detalle real).

## Escenario 4 — SC-004: nada se pierde

1. Elegir una licitación de participación de TIVIT que ya tenga Organismo/Monto/Items completos (por ejemplo, una de las capturadas por el scraper).
2. Abrir su ficha de detalle.
3. Confirmar que Organismo, Monto e Items siguen visibles ahí exactamente igual que antes de esta feature — el cambio es solo sobre la tabla del listado, no sobre el detalle.

## Verificación de datos (no depende de la UI)

```sql
-- Confirma que el catálogo de tipos cubre los códigos reales que existen en los datos
SELECT DISTINCT tipo FROM licitaciones
WHERE tipo NOT IN (SELECT codigo FROM tipos_licitacion);
-- Esperado: 0 filas (o solo códigos "pendientes de documentar" ya agregados con descripción provisoria)

-- Confirma que el selector de Estado no expone códigos huérfanos
SELECT * FROM estados_licitacion WHERE codigo NOT IN (5,6,7,8,15);
-- Esperado: siguen existiendo en la tabla (no se borran), pero usp_Catalogos_EstadosLicitacion() no debe devolverlos
```
