# ADR-015 — Carga manual de pliegos (deprecación de descarga automatizada)

**Fecha:** 2026-08-25  
**Rama:** `038-carga-manual-pliegos`  
**Estado:** Aprobado  
**Autores:** Orchestrator + Leonardo (HITL)  
**Relacionado:** `docs/api-first/licitaciones-documentos.md` (V141), `tools/scraper-mp-v2/descargar-documentos.js`, `src/MPM.Modules.Licitaciones/Services/AdjuntoDescargaService.cs`

---

## Contexto

El flujo comercial solicitado por Leonardo es:

```
Licitación -> Descargar pliegos/Bases -> Analizar con IA (Go/No-Go) -> Generar Propuesta
```

La descarga automatizada de `Ver Adjuntos` en Mercado Público (`DetalleAcquisition.aspx` -> popup `ViewAttachmentLC.aspx`) está **bloqueada por reCAPTCHA Enterprise + WAF Volterra**:

*   En `descargar-documentos.js` se documentó el fallo sistemático:
    *   `403.html` intermedia antes de `ViewAttachment.aspx`
    *   `img[src*="robot.png"]` / `acceso denegado`
    *   Fingerprint `headless` penalizado incluso con `xvfb-run --auto-servernum` + `MP_HEADLESS=false`
    *   Reintentos + fallback `window.open` no resuelven el bloqueo en IPs de datacenter (Cloud Run)
*   Ramas `036-flujo-comercial-ofertas` y `037-observabilidad-e05` dejan el problema abierto. `037` no modifica el módulo de documentos (diff vacío en `Licitaciones`).

Los demás scrapers (listado, ficha, cuadro de ofertas, datos abiertos) **siguen operativos**; el bloqueo es exclusivo de adjuntos.

Intentar bypassear reCAPTCHA Enterprise implicaría: solver externo (2captcha), rotación de proxies residenciales, mantenimiento continuo y riesgo de violar ToS. Es sobreingeniería frágil para un paso que el usuario puede hacer en 2 clics.

## Decisión

1.  **Modo por defecto = manual.** El sistema expone:
    *   Link directo a la ficha oficial: `https://www.mercadopublico.cl/fichaLicitacion.html?idlicitacion={codigoExterno}` (`target=_blank`)
    *   Zona drag & drop para subir los PDFs/DOCs descargados por el usuario
    *   Lista de documentos subidos (hash SHA-256, versión, estado `completado`)
    *   Botón `Analizar con IA` habilitado cuando hay >=1 documento analizable. El pipeline de IA (`AnalisisComercialService`) no cambia: lee bytes desde `IStorageService` igual que antes.

2.  **Scraper conservado como referencia, desactivado por flag.**
    *   Archivo `tools/scraper-mp-v2/descargar-documentos.js` **NO se borra**. Se marca `@DEPRECATED` en cabecera y en `README.md`.
    *   Backend `AdjuntoDescargaService.cs` se marca `[Obsolete("ADR-015: bloqueado por reCAPTCHA Enterprise. Usar upload manual. Flag Extraccion:ModoDescarga")]`
    *   Config `Extraccion:ModoDescarga = manual | auto` (default `manual`). En `manual`, `POST /documentos/descargar` responde `DOC_007` (feature deshabilitada) o se oculta en UI. En `auto` (solo si se habilita explícitamente) el flujo antiguo sigue invocable.
    *   No se elimina historial Git ni tests asociados; se skippean con `[Skip("ADR-015")]` si aplica.

3.  **Nuevo endpoint para carga manual:**
    *   `POST /api/v1/licitaciones/{codigoExterno}/documentos/upload-manual` (multipart/form-data, `file-upload` skill, GCS, validación MIME por magic bytes, 20MB por archivo, hasta 10 archivos por request).

## Alternativas consideradas

| Alternativa | Descarte |
|---|---|
| Solver de captcha (2captcha + proxy residencial) | Costo, latencia, ToS, mantenimiento; no aporta valor vs upload manual |
| Presigned URL directo a GCS desde frontend | Más complejo, sin ganancia para <20MB; descartado por simplicidad |
| Eliminar código del scraper | Pérdida de conocimiento (retry, xvfb, 403 handling). Git lo guarda pero se pierde descubribilidad. Decisión: conservar deprecado |

## Consecuencias

**Positivas:**
*   Flujo de Leonardo desbloqueado en 1-2 días, success rate 100% en pliegos.
*   Cumple `framework-security` (no bypass de anti-bot) y `Simplicity Gate`.
*   Pipeline IA y `licitaciones_adjuntos` (hash, versión, `descarga_estado`) se reutilizan sin migración.

**Negativas / Mitigación:**
*   Fricción UX leve (2 clics extra) -> Mitigada con link + dropzone grande + mensaje de ayuda.
*   Código deprecado permanece -> Mitigado con flag + ADR + `Obsolete`.

**No impacto:**
*   Otros scrapers (`ScraperBackgroundService`, `agente-mp.js`, datos abiertos) permanecen intactos.

## Validación

*   Manual upload completa -> `GET /documentos` muestra `estadoConjunto=completado` -> `POST /analisis-comercial` funciona (idem que con descarga auto).
*   Modo `auto` deshabilitado por defecto -> UI no muestra `Descargar documentos` salvo flag.

## Revisión

Revisar en `2026-12-01` si Mercado Público expone API oficial de adjuntos. Si ocurre, re-evaluar `ModoDescarga=auto`.

## Referencias

*   `docs/api-first/licitaciones-documentos.md` (spec V141)
*   `docs/specs/038-carga-manual-pliegos.feature-spec.md` (nuevo)
*   `tools/scraper-mp-v2/descargar-documentos.js:193-221` (evidencia de bloqueo)
