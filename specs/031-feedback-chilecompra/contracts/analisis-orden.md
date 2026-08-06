# Contrato: Orden del historial de análisis por fecha de adjudicación (US3)

## `GET /api/v1/analisis/workspaces` (sin cambio de firma)

Sin parámetros nuevos — es un cambio de **comportamiento por defecto**, no una opción configurable (ver `research.md` §3: el spec pide corregir el default, no agregar un selector).

**Antes**: `ORDER BY aw.created_at DESC` (fecha en que se ejecutó el análisis).

**Después**: `ORDER BY COALESCE(l.fecha_adjudicacion, l.fecha_estimada_adjudicacion) DESC NULLS LAST, aw.created_at DESC` (fecha de adjudicación de la licitación asociada; si no hay ninguna fecha de adjudicación registrada, esa fila cae al final, ordenada entre sí por `created_at`).

**Cambio de forma en la respuesta**: el item de la lista (`AnalisisWorkspaceListItemDto` o equivalente) gana un campo nuevo `fechaAdjudicacion: string | null`, para que el frontend pueda mostrar la fecha que explica el orden (evita repetir la confusión original: "¿por qué esto está primero?").

```json
{
  "id": 84,
  "licitacionId": 1204,
  "nombre": "CLOUD COMPUTING GCP PARA...",
  "estado": "completado",
  "fechaAdjudicacion": "2026-07-30",
  "createdAt": "2026-07-25T10:00:00Z"
}
```
