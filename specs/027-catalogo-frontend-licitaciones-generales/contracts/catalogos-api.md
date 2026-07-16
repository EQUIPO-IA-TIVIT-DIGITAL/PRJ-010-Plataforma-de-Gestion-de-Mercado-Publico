# Contract: `GET /api/v1/catalogos/tipos-licitacion` y `GET /api/v1/catalogos/estados-licitacion`

**Feature**: `027-catalogo-frontend-licitaciones-generales` — cambia la forma de la respuesta de tipos (clave pasa de número a texto) y el contenido de estados (menos filas). No cambia la ruta, verbo ni parámetros de ninguno de los dos endpoints.

## `GET /api/v1/catalogos/tipos-licitacion`

### Response — antes

```json
{ "data": [
  { "codigo": 1, "nombre": "Licitación Pública", "slug": "publica" },
  { "codigo": 2, "nombre": "Trato Directo", "slug": "trato-directo" }
]}
```

### Response — después

```json
{ "data": [
  { "codigo": "LE", "nombre": "Licitación Pública Menor", "slug": "le" },
  { "codigo": "LP", "nombre": "Licitación Pública Media", "slug": "lp" },
  { "codigo": "LQ", "nombre": "Licitación Pública Mayor", "slug": "lq" },
  { "codigo": "LR", "nombre": "Licitación Pública Grande", "slug": "lr" },
  { "codigo": "CO", "nombre": "Convenio Marco", "slug": "co" },
  { "codigo": "CA", "nombre": "Compra Ágil", "slug": "ca" },
  { "codigo": "TD", "nombre": "Trato Directo", "slug": "td" },
  { "codigo": "LS", "nombre": "Licitación de Servicios", "slug": "ls" }
]}
```

**Breaking change**: `codigo` pasa de `number` a `string`. Cualquier consumidor que compare `codigo` contra un entero deja de funcionar — el único consumidor conocido es `LicitacionFilterBar.tsx` (frontend de este mismo repo), actualizado como parte de esta spec.

## `GET /api/v1/catalogos/estados-licitacion`

### Response — antes (9 filas, con duplicados)

```json
{ "data": [
  { "codigo": 1, "nombre": "Publicada" },
  { "codigo": 2, "nombre": "Modificada" },
  { "codigo": 3, "nombre": "Desierta" },
  { "codigo": 4, "nombre": "Revocada" },
  { "codigo": 5, "nombre": "Publicada" },
  { "codigo": 6, "nombre": "Cerrada" },
  { "codigo": 7, "nombre": "Desierta" },
  { "codigo": 8, "nombre": "Adjudicada" },
  { "codigo": 15, "nombre": "Revocada" }
]}
```

### Response — después (5 filas, sin duplicados)

```json
{ "data": [
  { "codigo": 5, "nombre": "Publicada" },
  { "codigo": 6, "nombre": "Cerrada" },
  { "codigo": 7, "nombre": "Desierta" },
  { "codigo": 8, "nombre": "Adjudicada" },
  { "codigo": 15, "nombre": "Revocada" }
]}
```

**No es breaking change de tipo** (`codigo` sigue siendo `number`), solo cambia la cantidad de filas devueltas. Filtrar por `estado=1` en `GET /api/v1/licitaciones` sigue funcionando igual a nivel de backend (no se toca esa lógica ni los datos) — el código 1 simplemente deja de ofrecerse como opción en el selector de la UI.

## `GET /api/v1/licitaciones` (sin cambio de contrato)

El parámetro `tipo` sigue aceptando cualquier string y filtrando por coincidencia exacta contra `licitaciones.tipo` (`usp_Licitaciones_Listar`, sin cambios). Antes del filtro del catálogo, un valor como `tipo=LE` ya funcionaba a nivel de API — la spec solo hace que el frontend pueda ofrecerlo como opción real en el selector.
