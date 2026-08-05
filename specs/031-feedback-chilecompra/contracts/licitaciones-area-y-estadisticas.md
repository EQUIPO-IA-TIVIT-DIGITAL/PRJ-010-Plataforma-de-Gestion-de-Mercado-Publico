# Contrato: Filtro por área de negocio + estadísticas por estado (US1, US2)

## `GET /api/v1/licitaciones` (extensión, sin romper compatibilidad)

Parámetros nuevos, opcionales, agregados a los ya existentes (`page`, `pageSize`, `search`, `estado`, `tipo`, `organismo`, `fechaDesde`, `fechaHasta`, `sortBy`, `sortDir`):

| Parámetro | Tipo | Descripción |
|---|---|---|
| `area` | `short?` | código de `areas_negocio` (1=Cloud, 2=Ciberseguridad, 3=Digital). Filtra por `EXISTS` léxico contra `search_vector` (ver `research.md` §1) |
| `sinClasificar` | `bool?` | si es `true`, devuelve solo licitaciones que no calzan con ninguna área conocida (FR-003). Mutuamente excluyente con `area` — si vienen ambos, `area` tiene prioridad y se ignora `sinClasificar` |

Respuesta: sin cambios en la forma del DTO existente (`LicitacionResumenDto` + metadata de paginación).

## `GET /api/v1/catalogos/areas-negocio` (nuevo)

Lista las áreas de negocio configuradas, para poblar el selector de filtro.

```json
[
  { "codigo": 1, "nombre": "Cloud" },
  { "codigo": 2, "nombre": "Ciberseguridad" },
  { "codigo": 3, "nombre": "Digital" }
]
```

No expone `palabras_clave` al frontend (detalle de implementación interno).

## `GET /api/v1/licitaciones/estadisticas-estado` (nuevo)

Parámetros: `area` (opcional, mismo significado que arriba), `sinClasificar` (opcional).

```json
[
  { "codigoEstado": 5, "nombreEstado": "Publicada", "cantidad": 4210 },
  { "codigoEstado": 6, "nombreEstado": "Cerrada", "cantidad": 1876 },
  { "codigoEstado": 7, "nombreEstado": "Desierta", "cantidad": 302 },
  { "codigoEstado": 8, "nombreEstado": "Adjudicada", "cantidad": 5990 },
  { "codigoEstado": 15, "nombreEstado": "Revocada", "cantidad": 41 }
]
```

Todos los estados de `estados_licitacion` aparecen siempre, incluso con `cantidad: 0` (LEFT JOIN, ver `data-model.md`) — el frontend no debe asumir que la ausencia de un estado significa error.

**Drill-down**: cada fila es clicable en el frontend y navega a `GET /api/v1/licitaciones?estado={codigoEstado}&area={area}` (mismo endpoint de US1, sin endpoint adicional).
