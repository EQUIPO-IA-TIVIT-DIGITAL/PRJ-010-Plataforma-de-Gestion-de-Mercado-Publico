# Data Model: Ajustes Urgentes del Cliente

**Feature**: 017-ajustes-urgentes-cliente | **Date**: 2026-07-01

No se crean tablas nuevas. Cambios: 2 stored procedures nuevos (borrado de notificaciones) y una extensión del esquema JSON del resultado de análisis (columna existente).

## 1. Notificación (tabla existente — sin cambios de esquema)

Operaciones nuevas sobre la tabla existente de notificaciones:

| SP nuevo | Parámetros | Comportamiento |
|----------|-----------|----------------|
| `usp_Notificaciones_Eliminar` | `p_id BIGINT, p_user_id, p_tenant_id` | Borra la notificación solo si pertenece al usuario y tenant. Retorna filas afectadas (0 = no existe / no autorizada → 404). |
| `usp_Notificaciones_EliminarTodas` | `p_user_id, p_tenant_id` | Borra todas las notificaciones del usuario en el tenant. Retorna cantidad eliminada. |

**Reglas**: borrado físico (sin papelera); el aislamiento por usuario/tenant se valida dentro del SP (nunca en el controller). Tras eliminar, el contador de no leídas se recalcula con el SP existente de count.

**Migración**: `V075__Notificaciones_Eliminar_SPs.sql`.

## 2. Resultado de Análisis — extensión del JSON (columna existente)

El JSON producido por Gemini y guardado en la columna de resultado del análisis agrega la sección `validacion_documental`:

```json
{
  "validacion_documental": {
    "documentos": [
      {
        "nombre": "string — nombre del documento/antecedente",
        "requerido": "bool — exigido por las bases (antecedentes_requeridos)",
        "enviado": "bool — consta entre los documentos adjuntos entregados",
        "observado_en_acta": "string|null — qué dice el acta sobre él (faltante, observado, ok)",
        "estado": "ok | faltante | inconsistente | sin_informacion"
      }
    ],
    "inconsistencias": [
      {
        "documento": "string",
        "dice_acta": "string — afirmación del acta (ej. 'no presentó garantía')",
        "evidencia": "string — qué evidencian los documentos enviados",
        "severidad": "alta | media | baja"
      }
    ],
    "resumen": "string — veredicto de coherencia en 1-2 frases",
    "coherente": "bool — true si no hay inconsistencias de severidad alta"
  }
}
```

**Estados por documento**:

| Estado | Condición |
|--------|-----------|
| `ok` | Requerido y enviado, sin observación negativa del acta |
| `faltante` | Requerido, no enviado (coherente con el acta) |
| `inconsistente` | El acta lo declara faltante/observado pero SÍ consta como enviado (caso del cliente) — o viceversa |
| `sin_informacion` | No hay registro de envíos para contrastar (FR-007) |

**Transiciones**: el estado se calcula en dos pasos — Gemini propone y el post-proceso determinístico de `AnalisisService` corrige/añade (nunca elimina) inconsistencias basándose en la lista real de archivos del workspace.

**Compatibilidad**: análisis históricos sin `validacion_documental` → el frontend muestra "Comparativa no disponible para análisis anteriores; re-analizar para generarla". No se migran datos.

## 3. Configuración de sincronización (appsettings / env — sin BD)

| Clave | Default | Uso |
|-------|---------|-----|
| `Sync:IntervalDays` | `7` | Cadencia del `SyncEngineService` (antes 24 h fijas) |
| `Sync:WindowDays` | `8` | Ventana incremental con 1 día de solapamiento |
| Marca `sync_backfill_2025` | — | Registrada en el log de sync al completar el backfill 01-01-2025 → hoy (idempotencia) |

## 4. Descripciones de catálogo (constante frontend — sin BD)

`src/mpm-web/src/constants/catalogoDescripciones.ts`:

```ts
type CatalogoDescripcion = {
  titulo: string;       // "Licitación Pública"
  explicacion: string;  // lenguaje simple, 2-4 frases
  ejemplo?: string;     // caso ilustrativo
};
// mapas: ESTADOS_DESC[codigo], TIPOS_DESC[codigo]
```

Sin persistencia; contenido basado en definiciones oficiales de ChileCompra. Posible migración futura a columna `descripcion` sin impacto en la UI.

## 5. Sesión (frontend — sin BD)

| Elemento | Almacenamiento | Cambio |
|----------|----------------|--------|
| `mpm_token`, `mpm_user` | `localStorage` | Sin cambios de formato; limpieza centralizada en `sessionExpired()` |
| `mpm_session_expired` | `sessionStorage` | NUEVO flag efímero: el login lo lee para mostrar "Tu sesión expiró" y lo borra |
