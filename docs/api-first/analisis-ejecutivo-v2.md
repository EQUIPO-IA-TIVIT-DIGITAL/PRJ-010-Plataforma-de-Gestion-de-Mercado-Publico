# API Spec — Análisis Ejecutivo v2: Crecimiento Interanual (YoY)

**Versión**: 2.0 (delta sobre `docs/api-first/analisis.md`)
**Módulo**: Análisis — Dashboard Ejecutivo
**Generado por**: api-first-spec (agente design)
**Fecha**: 2026-08-20
**Origen funcional**: Reunión con cliente 14-08-2026 — *"cuánto he ganado este año vs el año pasado, % de crecimiento"* (decisión aprobada)
**Estado**: Pendiente validación HITL de supuestos marcados `[HITL]`

---

## 1. Scope

### Included

- Comparación interanual (YoY) del monto total ganado en el Dashboard Ejecutivo: monto del año consultado, monto del año anterior y % de variación.
- Manejo explícito de los casos borde: año anterior sin datos, base de comparación cero, primer año con datos.
- Extensión **aditiva** del contrato existente de `GET /api/v1/analisis/ejecutivo` (nuevo objeto anidado `comparacionAnual`; ningún campo existente cambia de nombre ni semántica).
- Bloque de tendencia en la UI del dashboard ejecutivo (`EjecutivoDashboardPage.tsx`).

### Excluded

- Serie multi-año completa (3+ años) → se eligió delta simple vs año anterior por ser lo más simple que cumple el pedido; la serie queda como evolución natural en v2.x si el cliente la pide.
- Inclusión de convenios marco en los montos → track separado ya acordado con el cliente. Ver §8 regla ANA-R030.
- Comparación de otras métricas (cantidad ganadas, puntaje promedio, ranking competidores) → solo el monto ganado fue pedido. Extensible después con el mismo patrón.
- Cambios al SP `usp_Analisis_ObtenerResultadosCompletos` → **no requiere migración**; el cálculo es en memoria sobre datos ya disponibles.

## 2. Data Model

Sin cambios de esquema. La feature consume las mismas filas que hoy produce `usp_Analisis_ObtenerResultadosCompletos(@p_anio)` (contenido_json de workspaces de análisis, filtrado por año real de la licitación según V112, deduplicado según V150).

### Nuevo DTO (solo contrato, no tabla)

```csharp
public class ComparacionAnualDto
{
    public int AnioActual { get; set; }
    public int AnioAnterior { get; set; }
    public decimal MontoActual { get; set; }        // misma semántica que MontoTotalGanado para AnioActual
    public decimal MontoAnterior { get; set; }      // idem para AnioAnterior
    public double? VariacionPorcentaje { get; set; } // null cuando no es calculable (ver ANA-R024)
    public bool TieneDatosAnioAnterior { get; set; } // false => el año anterior no tiene ninguna licitación analizada
}
```

`DashboardEjecutivoDto` gana un campo: `public ComparacionAnualDto? ComparacionAnual { get; set; }`.

## 3. Required Catalogs

No aplica (sin catálogos nuevos). Sección incluida por completitud del formato; nada que sembrar.

## 4. State Flow

No aplica: entidad sin ciclo de vida (cálculo derivado, read-only).

## 5. REST Endpoints

### `GET /api/v1/analisis/ejecutivo` — Dashboard ejecutivo (modificado, compatible)

**Descripción**: Igual comportamiento actual; la respuesta incorpora `data.comparacionAnual` cuando se consulta con filtro de año.

**Query Parameters** (sin cambios):

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `anio` | int | No | Año real de las licitaciones (fecha adjudicación > publicación > fallback created_at). Si se envía, la respuesta incluye `comparacionAnual`. Si se omite, `comparacionAnual` es `null`. |

**Response `200`** (solo se muestra el delta; el resto del payload es idéntico al vigente):

```json
{
  "success": true,
  "data": {
    "totalAnalizadas": 42,
    "totalGanadas": 18,
    "totalPerdidas": 24,
    "montoTotalGanado": 1250000000.00,
    "montoTotalPerdido": 800000000.00,
    "puntajePromedioTivit": 78.4,
    "puntajePromedioGanador": 82.1,
    "rankingCompetidores": [],
    "factoresPerdidaFrecuentes": [],
    "licitaciones": [],
    "aniosDisponibles": [2026, 2025, 2024],
    "comparacionAnual": {
      "anioActual": 2026,
      "anioAnterior": 2025,
      "montoActual": 1250000000.00,
      "montoAnterior": 980000000.00,
      "variacionPorcentaje": 27.6,
      "tieneDatosAnioAnterior": true
    }
  }
}
```

**Casos de respuesta de `comparacionAnual`:**

| Escenario | montoAnterior | variacionPorcentaje | tieneDatosAnioAnterior |
|-----------|---------------|---------------------|------------------------|
| Año anterior con datos | suma real | % calculado | true |
| Año anterior sin ninguna licitación analizada | 0 | null | false |
| Año anterior con datos pero monto 0 (ninguna ganada con adjudicación) | 0 | null | true |
| Año actual sin ganadas, año anterior con monto > 0 | suma real | -100.0 | true |

**Errors** (sin cambios): `VAL_001` (400) si `anio` no es entero válido.

| DB Object | Type | Description |
|-----------|------|-------------|
| `usp_Analisis_ObtenerResultadosCompletos(@p_anio)` | Function | Se invoca dos veces: una con el año pedido y otra con `anio - 1`. Sin cambios en su definición. |

## 6. Database Objects

| Endpoint | DB Object | Type | Cambio |
|----------|-----------|------|--------|
| GET /api/v1/analisis/ejecutivo | usp_Analisis_ObtenerResultadosCompletos | Function | Ninguno. Segunda invocación con `@p_anio = anio - 1` desde el servicio. |

> **Decisión de diseño** (alternativas evaluadas):
> - **Elegida — segunda pasada en servicio**: reutiliza el pipeline existente (dedup, extracción de año real, parseo de contenido_json) extrayendo un método privado `CalcularTotalesGanadas(anio)`. Costo: 1 llamada SP adicional. Los volúmenes (workspaces de análisis completados) son pequeños.
> - Rechazada — SP multi-año (`@p_anios int[]`): obliga a migrar firma del SP, duplicar lógica de dedup/año-real fuera del servicio y tocar V150; más riesgo para el mismo resultado.
> - Rechazada — serie completa en memoria sin filtro: cambia el perfil de memoria del endpoint para todos los llamadores existentes.

## 7. Shared DTOs

`ComparacionAnualDto` (definido en §2) vive junto a `DashboardEjecutivoDto` en `MPM.Modules.Analisis/Models/AnalisisDtos.cs`. El frontend replica el tipo en `src/mpm-web/src/types/analisis.ts`.

## 8. Business Rules

### Reglas de cálculo

- **ANA-R020**: `MontoActual` y `MontoAnterior` se calculan con la MISMA fórmula que `MontoTotalGanado` vigente: suma de `MontoAdjudicado` de licitaciones ganadas (deduplicadas) cuyo año real coincide con el año consultado. Cualquier cambio futuro de base (ej. incluir convenios marco) debe aplicarse a ambos años simultáneamente para que el % siga siendo comparable.
- **ANA-R021**: `variacionPorcentaje = ((montoActual − montoAnterior) / montoAnterior) × 100`, redondeado a 1 decimal.
- **ANA-R022**: Si `montoAnterior = 0`, `variacionPorcentaje = null` (división indefinida; la UI interpreta según `tieneDatosAnioAnterior`).
- **ANA-R023**: Si `montoActual = 0` y `montoAnterior > 0`, `variacionPorcentaje = -100.0`.
- **ANA-R024**: `tieneDatosAnioAnterior = false` solo cuando el año anterior no tiene NINGUNA licitación analizada (ganada o perdida); distingue "no hay datos" de "hay datos pero cero ganancias".
- **ANA-R025**: `comparacionAnual` es `null` cuando el request no incluye `anio`. `[HITL]` Confirmar con cliente que sin filtro de año el bloque YoY se oculta (hoy la página permite ver "todos los años").

### Reglas de alcance/base

- **ANA-R026**: Los convenios marco NO están incluidos en estos montos hoy (no producen `MontoAdjudicado` en este pipeline). El YoY se calcula sobre esa misma base actual. Cuando el track de convenios cambie la base, esta spec debe actualizarse y `variacionPorcentaje` recalcularse históricamente — el DTO no necesita cambios estructurales para eso.
- **ANA-R027**: El año anterior se define como `anio - 1` calendario, independiente de que exista en `aniosDisponibles` (por eso el flag `tieneDatosAnioAnterior`). `[HITL]` Validar que el cliente quiere año calendario y no últimos 12 meses móviles.

### Reglas de seguridad

- **ANA-R028**: Sin cambios: JWT requerido (heredado del controller). El dashboard es agregado, sin datos por usuario.

## 9. Error Codes

| Code | HTTP | Description | When |
|------|------|-------------|------|
| VAL_001 | 400 | Parámetro inválido | `anio` no parseable como entero |
| SYS_001 | 500 | Error interno | Fallo de BD o parseo catastrófico (comportamiento vigente) |

No se introducen códigos nuevos.

## 10. Criterios de aceptación (trazables a la reunión 14-08-2026)

- [ ] Dado un año con ganancias en 2026 y 2025, cuando el usuario abre `/analisis/ejecutivo?anio=2026`, entonces ve monto 2026, monto 2025 y el % de crecimiento con signo correcto y 1 decimal.
- [ ] Dado 2025 sin datos, cuando consulta 2026, entonces el bloque indica "sin datos del año anterior" y no muestra porcentaje.
- [ ] Dado 2025 con análisis pero monto ganado $0, cuando consulta 2026 con monto > 0, entonces el bloque indica que no hay base de comparación (no muestra ∞ ni 0%).
- [ ] Dado monto 2026 = 0 y 2025 > 0, entonces muestra -100%.
- [ ] La respuesta sin `anio` no incluye `comparacionAnual` (compatibilidad total con clientes actuales).
- [ ] Los montos mostrados coinciden exactamente con `montoTotalGanado` del mismo año consultado individualmente (misma base, ANA-R020).

## 11. Notas de implementación (orientativas, no contractuales)

- Backend: extraer `CalcularTotalesGanadas(int anio)` desde `AnalisisService.GetDashboardEjecutivoAsync`; llamarlo para `anio` y `anio - 1`.
- Frontend: tarjeta/bloque en `EjecutivoDashboardPage.tsx` bajo el KPI de monto ganado, con flecha ↑/↓ y color según signo; estados "sin datos" y "sin base" diferenciados.
