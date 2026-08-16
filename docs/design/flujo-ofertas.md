# Diseño — Flujo Comercial de Ofertas en MPM

**Estado**: Borrador para validación (v0.4 — entendimiento consolidado con el negocio)
**Fecha**: 2026-08-15
**Autor**: Orchestrator (TIVIT Foundry) con insumos del gerente comercial
**Sistema**: MPM — CU010 Mercado Público
**Tipo de cambio**: Evolución de pack existente (no proyecto nuevo)

---

## 1. Contexto y objetivo

El gerente comercial de TIVIT quiere que la plataforma MPM (que hoy ya trae las
licitaciones de Mercado Público y permite analizarlas) le responda rápido:

> **"¿Podemos ofertar esta licitación? ¿Sale a cuenta? ¿De cuánto? — y si sí,
> ayúdame a armar la propuesta."**

Hoy eso se hace a mano (bajar pliegos, leerlos, decidir con el equipo). La meta
es automatizar la lectura y el criterio, y dejar la decisión final siempre en
las personas.

**Fuentes de verdad para este diseño** (verificadas, no asumidas):

| Fuente | Qué aporta |
|--------|-----------|
| Scraper MPM (`tools/scraper-mp-v2/`, `MPM.Modules.Licitaciones`) | Licitaciones reales de ChileCompra, sesión/login, extracción de adjuntos |
| `AdjuntosHttpExtractor.cs` + `DocumentExtractionService` | Mecanismo real de descarga de adjuntos (navegador + sesión; fallback HTTP con reCAPTCHA documentado) |
| `ApiMpService.cs` | API oficial de ChileCompra: **no** expone adjuntos (solo datos de licitación y aclaraciones) |
| Base PRJ-001 (`Caso-01`, excluyendo `cu01-v2`) | Cerebro reutilizable: análisis de pliegos con IA, costeo, match talento Census, generador de propuesta DOCX (10 capítulos), go/no-go con decisión humana |
| Verificación en vivo 2026-08-15 | Ficha `729-134-LE26`: `ViewAttachment.aspx` protegida por reCAPTCHA Enterprise; `ViewAttachmentLC.aspx` sin token → robot.png (acceso denegado) |

---

## 2. Flujo de negocio (validado con el negocio)

```
1. Licitación entra por el scraper automático            (sin cambios)
2. Botón "Descargar documentos" en la ficha
   → PDFs (administrativo, técnico, preguntas y respuestas)
   → storage del sistema (GCS en prod / local en dev)
   → copia a Drive del usuario en prod                    (nuevo)
3. Botón "Analizar con IA" (zona IA, bajo demanda)
   → la IA lee los documentos guardados y extrae los datos (nuevo)
4. Match con TIVIT: Census (skills/certificaciones) + portafolio
   → ¿tenemos gente? ¿sale a cuenta? ¿de cuánto ofertamos?  (nuevo)
5. Vista ejecutiva: todo lo extraído + match + GO / NO GO  (nuevo)
6. GO  → genera propuesta (estructura base PRJ-001)        (nuevo)
        + avisa a las personas que el usuario marcó        (nuevo)
7. NO GO → registra el porqué y cierra                     (nuevo)
          + avisa a las personas que el usuario marcó      (nuevo)
```

Reglas transversales:
- **La decisión final es humana** (la IA solo recomienda: `strong_go` … `strong_no_go`).
- **Cero trabajo duplicado**: si una licitación ya tiene documentos guardados y
  no cambiaron, nadie re-descarga ni se re-paga análisis IA.
- **Censo (datos de personal de Census) va aparte del documento de propuesta**,
  nunca embebido en el DOCX principal.

---

## 3. Decisiones de diseño (con evidencia)

| # | Decisión | Detalle | Evidencia |
|---|----------|---------|-----------|
| D1 | **Descarga bajo demanda con la sesión existente** | Se reutiliza el login/sesión de Mercado Público que ya usa MPM (scraper y `.NET`). Nunca descargas masivas: la sesión tiene **cupo limitado de "Ver Adjuntos"** y bloqueos (403/robot.png) con enfriamiento | `agente-mp.js` (cupo/enfriamiento), `AdjuntosHttpExtractor.cs`, verificación en vivo |
| D2 | **Mecanismo de descarga = navegador real, SIN sesión** *(actualizado 2026-08-15 tras prueba E2E real)* | La ficha pública por `idlicitacion=` responde 200 sin sesión, pero **con sesión logueada redirige al portal interno** (Menu.aspx) y nunca carga. `ViewAttachmentLC.aspx` resuelve su reCAPTCHA Enterprise con Chromium channel (fingerprint del daemon) sin login. El script `descargar-documentos.js` va sin sesión: más simple, no gasta credenciales. Validado E2E: 8 documentos de `729-134-LE26` descargados con hash + binario servido | Verificación en vivo 2026-08-15 (debug-ficha + corrida E2E en docker) |
| D3 | **Cache y versionado de documentos por hash** | Al guardar cada PDF: `hash SHA-256` + `tamaño` + `fecha de la grilla` (cuando esté disponible). Antes de descargar: comparar tamaño/fecha (señal rápida, parcial). Después de descargar: comparar hash (definitivo, 100%). Análisis IA cacheado por versión del conjunto de documentos | Patrón ya usado en base PRJ-001 (`content_hash`) y en MPM (idempotencias) |
| D4 | **Almacenamiento** | El sistema guarda los PDFs en su storage actual (GCS prod / local dev). El botón además exporta a **Drive del usuario** en prod (integración Google Drive corporativa; en dev, carpeta local). Se puede ofrecer ambos destinos | Decisión de negocio + `storage` existente en MPM |
| D5 | **Proveedor IA = el de MPM** | La zona IA usa `ILlmClient`/`LlmClientResolver` de MPM (switch gcloud/Qwen configurable por SuperAdmin), no el cliente Gemini directo de la base PRJ-001. Los **prompts** de análisis de pliegos y de match se adaptan de la base (RFP_DATA_EXTRACTION, analisis-tender) | Switch IA ya existe (spec 033) |
| D6 | **GO/NO GO formal = evolución de Colaboración** | Se amplía `licitaciones_interes` (spec 031) con decisión formal: `go`/`no_go`, motivo, recomendación IA, score, decidido_por, notificados. NO GO cierra el expediente. Ambos caminos notifican a las personas elegidas a mano | Esbozo actual en `docs/api-first/colaboracion.md` |
| D7 | **Match TIVIT con Census** | Nueva integración en MPM: API Census (trabajadores, skills, certificaciones). Ojo conocido: **el token de Census expira en ~5 min** (BUG-023 de la base) → renovación por request. Complementar con portafolio/capacidades de TIVIT y grounding opcional | `integrations/census.py` + BUG-023 en base PRJ-001 |
| D8 | **Estimación de oferta = ORIENTATIVA** | Sin tarifa oficial vigente, la estimación usa la lógica de la base (4 escenarios, margen 20%, tarifas LATAM por seniority) y se muestra **siempre con disclaimer** "estimación orientativa, validar con comercial". Al existir tarifa oficial, se conecta | Decisión de negocio (no hay tabla oficial) |
| D9 | **Generador de propuesta = estructura PRJ-001** | 10 capítulos canónicos: carátula (razón social por país), confidencialidad, resumen ejecutivo, certificaciones ISO, experiencias, alcance, organigrama, aportes de las partes, entregables, capítulos teóricos. Plantilla DOCX corporativa de la base. Certificaciones y experiencias desde catálogos (recomendación IA con umbrales 0.8/0.5/0.3) | Exploración de la base (2 subagentes, 2026-08-15) |

---

## 4. Arquitectura propuesta (módulos MPM)

```
┌────────────────────────────────────────────────────────────┐
│ Frontend React (mpm-web)                                   │
│  · Ficha licitación: botón "Descargar documentos"          │
│  · Zona IA: análisis on-demand + chat                      │
│  · Vista ejecutiva: resumen + match + GO/NO GO             │
│  · Modal GO: armar propuesta + elegir notificados          │
│  · Modal NO GO: motivo + cierre                            │
└──────────────────────────┬─────────────────────────────────┘
                           │ REST /api/v1
┌──────────────────────────▼─────────────────────────────────┐
│ Backend .NET (MPM.Api)                                     │
│  · MPM.Modules.Licitaciones    → + descarga adjuntos       │
│  · MPM.Modules.Analisis (nuevo) → zona IA on-demand        │
│  · MPM.Modules.Colaboracion    → GO/NO GO formal           │
│  · MPM.Modules.Propuestas (nuevo) → generador DOCX         │
│  · MPM.Modules.Censo (nuevo)   → integración Census TIVIT  │
│  · MPM.Modules.Notificaciones  → avisos a personas         │
│  · ILlmClient (existente)      → proveedor IA MPM          │
└──────────────┬─────────────────────────────┬───────────────┘
               │                             │
        ┌──────▼──────┐              ┌───────▼───────┐
        │ PostgreSQL  │              │ Storage GCS  │
        │ + tablas    │              │ /local (dev) │
        │ nuevas (ver │              │ + Drive (prod)│
        │ §5)         │              └───────────────┘
        └─────────────┘
Integraciones externas: ChileCompra (sesión MP existente) · Census API (nueva) · Google Drive (nueva)
```

---

## 5. Modelo de datos (nuevas entidades, alto nivel)

```mermaid
erDiagram
    licitaciones ||--o{ licitacion_documentos : "tiene"
    licitaciones ||--o{ licitacion_decisiones : "decide"
    licitaciones ||--o{ analisis_ia_licitaciones : "analiza"
    licitaciones ||--o{ propuestas : "genera"
    licitacion_documentos ||--o{ analisis_ia_licitaciones : "versión usada"

    licitacion_documentos {
        bigint id PK
        bigint licitacion_id FK
        varchar tipo_documento "administrativo|tecnico|preguntas_respuestas|otro"
        varchar nombre
        varchar ruta_storage
        varchar hash_sha256 "detección de cambio (definitiva)"
        bigint tamanio_bytes "señal rápida de cambio"
        varchar fecha_grilla "fecha mostrada por el portal (si está disponible)"
        int version
        varchar descargado_por
        timestamp descargado_at
    }
    licitacion_decisiones {
        bigint id PK
        bigint licitacion_id FK UK
        varchar decision "go|no_go"
        varchar motivo
        varchar recomendacion_ia "strong_go|go|no_go|strong_no_go"
        numeric score_confianza
        varchar decidido_por
        timestamp decidido_at
        varchar notificados "emails/ids elegidos a mano"
        timestamp notificado_at
    }
    analisis_ia_licitaciones {
        bigint id PK
        bigint licitacion_id FK
        varchar documento_set_hash "cache del análisis por versión"
        varchar estado "pendiente|procesando|completado|error"
        varchar proveedor "gcloud|qwen (resolver MPM)"
        varchar modelo_usado
        text resultado_json "datos extraídos + match + estimación"
        text resumen_ejecutivo
        numeric costo_tokens_usd
        timestamp created_at
    }
    propuestas {
        bigint id PK
        bigint licitacion_id FK
        int version
        varchar capitulos_seleccionados
        varchar certificaciones_ids
        varchar experiencias_ids
        varchar ruta_archivo "DOCX/PDF generado"
        varchar estado "borrador|generada|enviada|descartada"
        varchar generado_por
        timestamp generado_at
    }
```

Además, catálogos base (semilla desde la base PRJ-001):
- `catalogo_experiencias` (proyectos similares: cliente, descripción, montos, fechas)
- `catalogo_certificaciones` (ISO y otras, con archivo DOCX/PDF asociado)
- `catalogo_capitulos` (bloques teóricos de la propuesta)
- `config_census` (endpoint/credenciales — nunca en repositorio)

---

## 6. Integraciones

| Integración | Estado hoy | Trabajo |
|-------------|-----------|---------|
| ChileCompra (sync licitaciones) | ✅ Existe | Sin cambios |
| ChileCompra (descarga adjuntos) | ✅ Existe (navegador + sesión, cupos) | Botón on-demand + cache (D1, D2, D3) |
| API oficial ChileCompra | ✅ Existe | Sin cambios (no expone adjuntos) |
| **Census TIVIT** | ❌ No existe en MPM | Nuevo módulo `MPM.Modules.Censo`: auth token (renovar c/request, expira ~5 min), skills/certificaciones por usuario, catálogo de skills |
| **Google Drive** | ❌ No existe en MPM | Subida de PDFs a Drive del usuario (prod); en dev carpeta local. Portar patrón de `google_drive.py` de la base PRJ-001 |
| **Proveedor IA** | ✅ Existe (switch gcloud/Qwen) | Reutilizar; adaptar prompts desde la base |

---

## 7. Fases de implementación

| Fase | Contenido | Entregable visible | Habilita |
|------|-----------|--------------------|----------|
| **1 — Descarga + cache + zona IA** | Botón "Descargar documentos" (storage + local dev), tabla `licitacion_documentos` con hash, endpoint análisis on-demand con proveedor MPM, chat sobre el análisis | El usuario baja los pliegos 1 vez y habla con la IA sobre ellos; los demás no re-descargan ni re-pagan | Pasos 2-3 |
| **2 — Match + vista ejecutiva + GO/NO GO formal** | Integración Census, match capacidades, estimación orientativa con disclaimer, vista ejecutiva, decisión GO/NO GO con motivo y cierre (evoluciona Colaboración) | "¿Podemos? ¿De cuánto? GO/NO GO con fundamento" | Pasos 4-5 |
| **3 — Generador de propuesta + avisos + Drive** | Catálogos (experiencias, certificaciones, capítulos), generador DOCX (10 capítulos, plantilla corporativa), notificación a personas marcadas (GO y NO GO), exportación a Drive | Propuesta generada y equipo avisado | Pasos 6-7 |

Criterio de corte por fase: prueba end-to-end con una licitación real + aceptación del
gerente comercial (HITL) antes de pasar a la siguiente.

---

## 8. Riesgos y decisiones abiertas

| Riesgo / abierto | Impacto | Mitigación / decisión necesaria |
|------------------|---------|--------------------------------|
| **Cupo de "Ver Adjuntos"** en la sesión MP | Descargas masivas o frecuentes agotan la sesión (403/enfriamiento) | Descarga on-demand + cache; monitorear cupo; alertar cuando quede bajo |
| **Tarifas sin tabla oficial** | La estimación de oferta puede no reflejar costos reales | Marcar SIEMPRE como orientativa; dejar interfaz para conectar tarifa oficial cuando exista |
| **Token Census expira ~5 min** | Match falla intermitentemente | Renovación por request + caché corto + alerta si falla (BUG conocido de la base) |
| **reCAPTCHA en adjuntos** | Alguna descarga puede fallar ocasionalmente | Reintentos + mensaje claro al usuario + fallback HTTP futuro |
| **Plantilla DOCX corporativa** | El generador depende del formato oficial | Validar con TIVIT la `tivit_proposal_template.docx` de la base (o entregar una nueva) |
| **Drive: cuenta individual vs. carpeta compartida** | Dónde caen los PDFs | Decidir en Fase 3 (default: carpeta personal del usuario vía OAuth/service account) |

---

## 9. Registro de routing (orchestrator)

- **Tipo de cambio**: modificación de pack existente → *governance (verificar) → diseño funcional (este doc) → spec api-first por módulo → qa-validation*.
- **Skills a activar en las siguientes fases**: `api-first-spec` (specs por módulo), `hu-template` (HUs), `tasks`, `api-first-backend`, `api-first-frontend`, `api-first-testing`, `qa-validation`, `converge`, `changelog`/`pull-request`.
- **Agentes**: design (specs/HUs) → delivery (implementación) → control (validación y go/no-go por fase).
- **Artefactos previos consumidos**: entendimiento validado con el negocio (rondas 2026-08-15), exploración de la base PRJ-001 (2 subagentes), verificación en vivo de ChileCompra.
- **Decisiones abiertas al cierre de este diseño**: tarifas oficiales, plantilla DOCX vigente, destino Drive (individual vs. compartido), autenticación Census en MPM.

---

## 10. Gobernanza del trabajo (decisión del negocio, 2026-08-15)

- **Todo el trabajo de este flujo se arma en una rama dedicada** (patrón del
  repo: ramas `NNN-slug` desde `dev` — ver `019-rediseno-frontend`,
  `029-fix-hallazgos-code-review-competidores-alertas`, `033-migracion-qwen-g4`).
- **Rama propuesta**: `036-flujo-comercial-ofertas` (base: `dev`). El número de
  spec se confirma al crear la rama; el CHANGELOG sigue la numeración por lote.
- **Regla**: no se mezcla con trabajo en curso de `dev` — al momento de la
  anotación `dev` tenía cambios sin commitear de otro lote (Competidores /
  ApiMpService / mpm-web), que no son parte de este flujo y no se tocan.
- Los artefactos de diseño (`docs/design/flujo-ofertas.md`) se incorporan a la
  rama dedicada en su primer commit.
