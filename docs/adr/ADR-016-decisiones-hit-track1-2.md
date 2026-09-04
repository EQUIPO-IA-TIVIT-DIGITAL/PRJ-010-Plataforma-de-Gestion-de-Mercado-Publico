# ADR-016 — Decisiones HITL Track 1 (Quick Wins) y Track 2 (Ingesta CM API)

**Fecha:** 2026-08-26  
**Rama:** `039-cierre-v11-hitl-tracks-1-2`  
**Estado:** Aprobado (HITL resuelto por Orchestrator con aprobación del usuario)  
**Autores:** Orchestrator (Manuel Aliaga) + Usuario (HITL)  
**Relacionado:** `docs/api-first/analisis-ejecutivo-v2.md`, `docs/api-first/preferencias-usuario.md`, `docs/specs/go-nogo-por-tipo.feature-spec.md`, `docs/api-first/ingesta-datos-abiertos.md` (pivotado a API mserv), `CHILECOMPRA_API_ANALYSIS.md`, `docs/specs/track1-quickwins.tasks.md`, `docs/specs/track2-ingesta-cm.tasks.md`

---

## Contexto

Capacitación interna 14-08-2026 (PRJ-010) generó 2 tracks de trabajo bloqueados en `.workflow/state.json: HITL-validacion-specs` (20/08). Para cerrar `v1.1` sin bloquear delivery, se resuelven las 6 decisiones bloqueantes + 6 no bloqueantes con defaults propuestos por diseño, validados contra normativa ChileCompra y viabilidad técnica verificada el 26/08 (curl 200 OK a `mserv` y a `transparenciachc.blob.core.windows.net`).

Investigación previa verificada: `scraping-datos-abiertos-chilecompra` (sin credenciales) + `CHILECOMPRA_API_ANALYSIS.md` demostraron que la fuente agregada `mserv-datos-abiertos` expone `modality` con `Convenio Marco` sin necesidad de parsear zips. Se elige **opción B sin zip** para cierre (ver ADR-016 §2).

## Decisión

### Track 1 — Quick Wins

| ID | Tema | Decisión | Justificación | Impacto si cambia |
|---|---|---|---|---|
| **GO-T001..T007** | Umbrales UTM por tipo | **Oficial ChileCompra: 100 / 1.000 / 2.000 UTM** (LE <100, LP 100-1.000, LQ 1.000-2.000, LR >2.000). El "450 UTM" mencionado por cliente queda como alias documental a rango LP con `WARN` log, no como regla | Norma Ley de Compras, no umbral interno. Evita desalineación con portal | Si cliente exige 450 como corte real, es parámetro `GO_UMBRAL_LP_MIN=450` sin recódigo |
| **GO-CO** | Convenio Marco | **Prompt-only en v1** (`modulacion_tipo.regla_aplicada=convenio_marco_evaluacion_catalogo`). Sin regla dura post-IA que fuerce `no_go` | Sin datos para calibrar override; ocultar razonamiento LLM es riesgoso. Trazabilidad en `resultado_json.modulacion_tipo` permite revisar con 20 análisis reales para v1.2 | Si tras 20 casos se ve sesgo, se agrega regla dura `GO-CO-HARD` en v1.2 |
| **ANA-R025** | YoY sin filtro año | Bloque oculto (`ComparacionAnual=null`) cuando `anio` es null | Evita comparar contra año anterior sin base temporal | — |
| **ANA-R027** | Año calendario vs 12m móviles | **Año calendario** (01-01 a 31-12) | Coherente con ficha oficial Datos Abiertos por año | — |
| **PREF-D2** | Preferencia monto mínimo default | **Aplicado en frontend** (hook siembra `montoDesde` si URL limpia) | Backend permanece stateless, URL manda sobre preferencia | — |
| **PREF-R005** | Prefill alertas desde preferencia | Sí, `AlertasPage` prellena con preferencia si existe | Reduce fricción | — |

### Track 2 — Ingesta CM (pivotado a API sin zip)

| ID | Tema | Decisión | Justificación |
|---|---|---|---|
| **HITL-01** | Neto vs bruto CLP | **Neto CLP** (`amountCLPAnnual` donde `idModalidad=5`). Modelo guarda ambos pero dashboard usa neto | Base comparable con cifras oficiales ChileCompra; bruto mete IVA y distorsiona ranking |
| **HITL-02** | RUTs seed | **Solo TIVIT `76.130.712-6`** (`activo=true`). Competidores vía `POST /admin/ingesta-cm/ruts` sin re-ingesta | Cliente confirmó "empezar solo con TIVIT" (CHILECOMPRA feedback 03/08) |
| **HITL-03** | Tolerancia conciliación CA-01 | **±1%** | Cubre redondeos + fecha corte paridad (ver `CHILECOMPRA_API_ANALYSIS` tabla) |
| **HITL-04** | Monedas sin paridad | `NULL + contador + WARN` (no bloquea) | Observabilidad sin fallar corrida |
| **HITL-05** | Scheduler | **Día 5, 06:00 `America/Santiago`** (`CM_CRON=0 6 5 * *`), procesa mes actual + anterior + PENDIENTE/FALLIDO | Día 5 da margen a cierre mensual del portal |
| **HITL-06** | Umbral filas ilegibles | `>1%` → `FALLIDO` | Calidad mínima |

### Pivot fuente Track 2

Se abandona `planillas-cm/*.zip` (37h) para cierre `v1.1` y se usa **`mserv-datos-abiertos /organismSupplier/modality`** (6h, JSON, sin archivos, sin `Windows-1252`). Verificado 26/08: `GET modality/2026/76.130.712-6` → `idModalidad=5 Convenio Marco 626M CLP` 200 OK. El zip line-item queda documentado para `v1.2` si se requiere drill-down por OC.

## Consecuencias

*   **Positivas:** `v1.1` cierra en 35.5h (F0 0.5 + F1 27 + F2 6 + F3 2) con brechas dashboard corregidas y sin deuda de archivos.
*   **Negativas:** Sin drill-down por OC en `v1.1`. Mitigado: el total agregado ya explica la brecha $1.340M→$2.500M.
*   **No impacto:** Fases 8-18 siguen `PAUSADO-CLIENTE` (ROADMAP 03/07), `016` queda `NO_VIABLE` (reCAPTCHA).

## Validación

*   `ComparacionAnual` con `anio=2026` → `variacionPorcentaje` correcto, `tieneDatosAnioAnterior=false` cuando 2025 sin filas.
*   `PUT /usuarios/me/preferencias-licitaciones {montoMinimo:50000000}` → `GET /licitaciones?montoDesde=50000000` en primer fetch sin URL.
*   `GET admin/ingesta-cm/resumen?rut=76.130.712-6` → `montoNetoClp` = `modality.CM` dentro de `±1%` vs ficha oficial.
*   `GET modality/2026` 200 OK (curl verificado 26/08).

## Revisión

Revisar `2026-12-01` si se requiere drill-down por OC (migrar a zip) o regla dura `GO-CO-HARD`.
