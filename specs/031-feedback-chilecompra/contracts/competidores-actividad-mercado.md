# Contrato: Actividad total de mercado de un competidor (US4)

Ver `research.md` §4 para la justificación de por qué esto es asíncrono y acotado por área+período.

## `GET /api/v1/competidores/{nombre}/actividad-mercado`

Parámetros: `area` (código de `areas_negocio`, recomendado para acotar costo — ver research), `fechaDesde`, `fechaHasta` (mismo rango que el informe ejecutivo existente que lo originó).

**Comportamiento get-or-generate** (mismo patrón que `CompetidorAnalysisService.ObtenerOGenerarAnalisisAsync`):

- Si existe una fila en `competidores_actividad_mercado` con esa clave (`nombre_competidor` + `area` + `fechaDesde` + `fechaHasta`) y `estado = 'listo'` → devuelve el contenido cacheado, `200 OK`.
- Si no existe, o existe con `estado = 'error'` → crea/reencola la fila en `estado = 'generando'`, dispara el job de scraping acotado en background, y responde `202 Accepted`:
  ```json
  { "estado": "generando", "mensaje": "Calculando actividad de mercado, puede tardar varios minutos." }
  ```
- Si existe con `estado = 'generando'` → mismo `202 Accepted`, sin volver a encolar (idempotente).

**Respuesta cuando `estado = 'listo'`**:

```json
{
  "estado": "listo",
  "nombreCompetidor": "Telefónica Empresas",
  "area": "Cloud",
  "periodo": { "desde": "2026-01-01", "hasta": "2026-07-31" },
  "cantidadLicitaciones": 58,
  "montoTotalAdjudicado": 145000000000,
  "generadoAt": "2026-08-05T10:00:00Z",
  "licitaciones": [
    { "licitacionId": 9931, "nombre": "...", "montoAdjudicado": 2100000000, "tivitParticipo": false }
  ]
}
```

El campo `tivitParticipo` por licitación es lo que permite al frontend distinguir visualmente "encuentro directo" (ya cubierto por el informe existente) de "brecha de mercado" (licitación nueva que este endpoint aporta) dentro de la misma lista, sin que el usuario tenga que cruzar dos vistas.

**Frontend (`CompetidoresPage.tsx`)**: nuevo panel "Actividad total de mercado" dentro de la ficha de un competidor ya identificado, con estado de carga mientras `estado = 'generando'` (polling cada ~10-15s, igual al patrón ya usado para el estado de un `analisis_workspace`).
