# Contrato: Cobertura de mercado por área (US5 / FR-008)

Ver `research.md` §3 para la justificación de la comparativa elegida y `data-model.md` para el modelo de respuesta.

## `GET /api/v1/analisis/ejecutivo/cobertura-mercado`

Parámetros: `area` (código de `areas_negocio`, opcional — si se omite, cubre todas las áreas), `fechaDesde`, `fechaHasta`. Misma validación que `ActividadMercadoRequest` de `CompetidoresController.cs` (`fechaHasta` no puede ser anterior a `fechaDesde`).

**Diferencia deliberada con el patrón de `competidores-actividad-mercado.md` (spec 031)**: ese endpoint es asíncrono (`202 Accepted` + polling) porque dispara un scrape en vivo contra Mercado Público para un competidor específico. Este endpoint **no necesita eso** — el universo de licitaciones ya está sincronizado a diario vía la API oficial (`SyncEngineService`, sin scraping) y la participación de TIVIT ya vive en `licitaciones_ofertas`/`analisis_workspaces`. Es una agregación sobre datos ya presentes en la base: responde síncrono, `200 OK`, sin estado `generando`.

**Respuesta**:

```json
{
  "areaCodigo": 1,
  "periodo": { "desde": "2026-01-01", "hasta": "2026-08-05" },
  "totalLicitacionesMercado": 214,
  "totalLicitacionesTivit": 61,
  "porcentajeCobertura": 28,
  "licitacionesSinParticipacion": [
    {
      "codigo": "622-59-LP25",
      "nombre": "SERVICIO DE INFRAESTRUCTURA COMO SERVICIO IAAS",
      "organismo": "MINISTERIO DE...",
      "fechaCierre": "2026-06-30"
    }
  ]
}
```

**Frontend (`EjecutivoDashboardPage.tsx`)**: nueva `Card` "Cobertura de mercado" junto a las tarjetas existentes de resumen (total analizadas/ganadas/perdidas), con un `Progress` mostrando `porcentajeCobertura` y una `Table` colapsable (`Collapse`, ya en uso en esta página) con `licitacionesSinParticipacion`, cada fila con link directo a la ficha de la licitación (mismo patrón de navegación que la tabla de licitaciones analizadas ya existente en esta página).

**Backend**: nuevo stored procedure `usp_AnalisisEjecutivo_CoberturaMercado(p_tenant_id, p_area_codigo, p_fecha_desde, p_fecha_hasta)`, agregando sobre `licitaciones` (universo, filtrado por área vía el mismo mecanismo de palabras clave que ya usa `usp_Licitaciones_FiltrarPorArea` de spec 031) y `licitaciones_ofertas`/`analisis_workspaces` (participación de TIVIT) — sin tabla nueva, sigue la convención `usp_<Entidad>_<Verbo>` del Principio II de la constitución.
