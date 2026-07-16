# Contract: `GET /api/v1/licitaciones/buscar-natural`

**Feature**: `018-buscador-inteligente-nl` — extiende el endpoint existente (`LicitacionController.cs:137-152`), no crea uno nuevo.

## Request

```
GET /api/v1/licitaciones/buscar-natural?q={texto}&page={n}&pageSize={n}&estado={codigo}
```

| Parámetro | Tipo | Requerido | Cambia en 018 |
|---|---|---|---|
| `q` | string | Sí (min 2 chars) | Sin cambio de contrato — ahora se interpreta con IA antes de construir el tsquery |
| `page` | int | No (default 1) | Sin cambio |
| `pageSize` | int | No (default 20) | Sin cambio |
| `estado` | smallint? | No | Sin cambio — sigue teniendo prioridad sobre lo que infiera la IA de `q` |

No se agregan parámetros nuevos a la firma pública del endpoint — la interpretación de monto/fecha implícitos ocurre server-side a partir de `q`, no como parámetros adicionales que el frontend deba construir.

## Response

Sin cambios de forma respecto al contrato actual — `PaginatedResult<LicitacionNaturalSearchResult>`.

```json
{
  "items": [
    {
      "id": 123,
      "codigoExterno": "622-11-L126",
      "nombre": "...",
      "descripcion": "...",
      "organismo": "...",
      "codigoEstado": 5,
      "tipo": "...",
      "relevancia": 0.87
    }
  ],
  "total": 42,
  "page": 1,
  "pageSize": 20
}
```

`relevancia` sigue siendo el `ts_rank` de PostgreSQL (no un score de embeddings — no hay pipeline vectorial, ver `research.md`).

## Comportamiento nuevo (interno, no visible en la firma)

1. `LicitacionService.BuscarNaturalAsync` llama primero a `ConsultaSemanticaService.InterpretarAsync(q)`.
2. Si la interpretación tiene `Confianza = Alta`: se enriquece `q` con `TerminosExpandidos` y se completan `estado`/rangos de fecha inferidos (solo si el usuario no los pasó explícitos), antes de delegar a `LicitacionHandler`.
3. Si la interpretación falla, no está disponible, o `Confianza = Baja`: se usa `q` tal cual — comportamiento idéntico al actual `usp_Licitaciones_BuscarNatural` (FR-005).
4. El backend NO expone si hubo o no interpretación IA en la response — es un detalle interno. (Si a futuro se quiere mostrar "buscamos también: X, Y" en la UI, requiere un campo nuevo — fuera de alcance de este contrato inicial, ver Assumptions en spec.md.)

## Casos de error (sin cambios respecto al contrato actual)

| Código | Causa |
|---|---|
| `400 VAL_001` | `q` con menos de 2 caracteres tras `Trim()` |
| `200` con `items: []` | Consulta válida sin resultados (incluye el caso "consulta ambigua" del edge case del spec — nunca error) |
