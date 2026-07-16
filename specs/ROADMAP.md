# Roadmap MPM — Repriorización 2026-07-03

**Origen**: Repriorización solicitada por el cliente en dos reuniones de alcance, más una segunda ronda de ajustes directos del 2026-07-03 (deploy GCP como N1, roadmap 8-18 en pausa, rediseño frontend como paralelo de baja prioridad).
- **2026-06-09** — "Revisión Alcance Mercado Público". Transcript: `info/Revisión Alcance Mercado Público - 2026_06_09 10_58 GMT-05_00 - Anotações do Gemini.pdf`
- **2026-07-01** — "[CU010] - Revisión de Alcance". Transcript: `info/[CU010] - Revisión de Alcance _ 2026_07_01 14_59 GMT-05_00 - Notas de Gemini.md`
- **2026-07-03** — Ajuste directo del cliente en sesión de trabajo (sin transcript): confirma la secuencia Alertas → Buscador → Pipeline, eleva Despliegue en GCP a N1, pausa el resto del roadmap (Fases 8-18), y agrega Rediseño Frontend como workstream paralelo de baja prioridad.
- **2026-07-06** — "[EQUIPO-IA] Reunión General" (interna TIVIT, no es reunión con el cliente). Transcript: `info/[EQUIPO-IA] REUNIÓN GENERAL _ 2026_07_06 08_56 GMT-05_00 - Notas de Gemini.md`. Manuel Aliaga (gerencia) confirma deadline exacto para CU010 (jueves 9 de julio, 7:59 a.m.) y agrega una instrucción interna nueva: integrar las alertas con un bot de Telegram (ver pedido #12 abajo).

Este documento es la fuente única de verdad sobre secuencia y prioridad vigente. Las fechas ("Semana estimada") dentro de cada `specs/0XX-.../spec.md` pueden quedar desactualizadas tras esta repriorización — en caso de conflicto, **este roadmap manda**. Export tabular: [`specs/roadmap.csv`](roadmap.csv).

---

## Qué pidió el cliente (resumen de las tres rondas)

| # | Pedido | Cuándo | Quién |
|---|--------|--------|-------|
| 1 | Analizar licitaciones adjudicadas perdidas: comparar la razón de pérdida contra los documentos realmente enviados | 2026-07-01 | Francisco |
| 2 | Buscador que asocie conceptos/sinónimos, no solo palabras clave literales, sobre licitaciones activas/cerradas/adjudicadas | 2026-06-09 | Francisco, Ariel |
| 3 | Alertas automáticas de licitaciones nuevas por palabras clave + sinónimos IA (cloud, ciberseguridad, SOC, data center, telecom, cámaras) | 2026-07-01 | Francisco, Manuel |
| 4 | Cada alerta con resumen accionable: requisitos, competidores, presupuesto, forma de pago, multas, señal de renovación/proveedor actual | 2026-07-01 | Francisco |
| 5 | Notificar a los dos account managers de gobierno, no solo a quien creó la regla | 2026-07-01 | Francisco |
| 6 | Motor de validación de completitud de una oferta antes de enviarla | 2026-07-01 | Francisco |
| 7 | Inteligencia de mercado: entender por qué competidores (ej. Sonda) ganan, para replicar en otros sectores | 2026-06-09 | Francisco |
| 8 | Reuniones de seguimiento semanales (miércoles 16:00, 20-30 min) | ambas | Francisco |
| 9 | **Despliegue en producción en GCP como prioridad N1**, por delante de todo lo demás | 2026-07-03 | Cliente |
| 10 | Rediseño frontend, explícitamente sin competir por prioridad con lo funcional | 2026-07-03 | Cliente |
| 11 | Fases 8-18 del roadmap original quedan en pausa — foco exclusivo en el punto de dolor del cliente | 2026-07-03 | Cliente |
| 12 | Integrar el sistema de Alertas con un bot de Telegram para notificación inmediata (adicional al in-app) — **instrucción interna de gerencia (Manuel), no pedido directo del cliente** | 2026-07-06 | Manuel (interno) |
| 13 | Reforzar la explicabilidad del análisis histórico (`017`, ya implementado) — el equipo debe poder justificar cada campo mostrado ante preguntas de los stakeholders, no solo mostrarlo | 2026-07-06 | Manuel (interno) |

---

## Estado y secuencia vigente

### ✅ Hecho

| Feature | Spec | Cubre el pedido # |
|---|---|---|
| Análisis histórico / validación documental (razón de pérdida vs. documentos enviados) | [`017-ajustes-urgentes-cliente`](017-ajustes-urgentes-cliente/spec.md) | 1 |

### 🟠 Backlog técnico — no bloquea N1-N4, pero habilita trabajo futuro

| Feature | Spec | Estado real | Por qué importa |
|---|---|---|---|
| Extracción de documentos (Acta de Evaluación) vía HTTP directo, sin navegador por licitación | [`016-extraccion-documentos-api`](016-extraccion-documentos-api/spec.md) | **Actualizado 2026-07-06**: implementada a nivel de código (`WebFormsParser`, `MpSessionProvider`, `AdjuntosHttpExtractor`, `DocumentExtractionService`), pero el spike en vivo (T004, credenciales reales) encontró que el listado de adjuntos está protegido por **reCAPTCHA Enterprise** — irresoluble por HTTP puro, solo un navegador real lo pasa. `Extraccion:Modo` queda en `solo_navegador` indefinidamente. Detalle en `contracts/internal-api.md`. | Ya NO es el habilitador de la migración a Cloud Run como se pensó — resultó no reducir el uso de Chromium. La migración a Cloud Run + Cloud Run Jobs (`002-fase5-deploy-gcp`) sigue viable igual, porque Cloud Run Jobs no throttlean CPU por inactividad (a diferencia de Services) y pueden correr el ciclo completo del scraper con navegador sin problema. |

### 🔴 N1 — Ahora, antes que cualquier otro ítem

| Feature | Spec | Cubre el pedido # | Cambio por repriorización |
|---|---|---|---|
| Fase 5 — Despliegue en GCP | [`002-fase5-deploy-gcp`](002-fase5-deploy-gcp/spec.md) | 9 | **Reescrito 2026-07-03**: pasa de "On-Premise + Huawei Cloud" a Google Cloud, reutilizando el proyecto `tivit-cu010` y el bucket GCS `tivit-cu010-mpm-adjuntos` ya existentes. Elevado a **N1 explícito**. **Pivote 2026-07-06**: de Compute Engine a **Cloud Run + Cloud Run Jobs** (restricciones de infraestructura TIVIT: sin IP pública en VM/Cloud SQL, VPC custom obligatoria). Ahora **depende de `016-extraccion-documentos-api`** (ver Backlog técnico abajo) antes de poder desplegar `scraper-job` — la fecha de N1 se corre en función de cuándo se complete 016. |

### 🔜 N2 — Julio 2026, en curso

| Feature | Spec | Cubre el pedido # | Cambio por repriorización |
|---|---|---|---|
| Fase 6 — Alertas Inteligentes por Palabras Clave | [`003-fase6-alertas-keywords`](003-fase6-alertas-keywords/spec.md) | 3, 4, 5, 12 | Alcance ampliado 2026-07-03: User Story 3 (sinónimos vía IA) y User Story 4 (notificación enriquecida a account managers). Complejidad Media → Media-Alta; estimación 1 → 1.5 semanas. **Ampliado de nuevo 2026-07-06**: User Story 5 (bot de Telegram, pedido #12, instrucción interna) + función de "disparar alerta de prueba" para demo. |

### ✅ N3 — Cerrado 2026-07-16

| Feature | Spec | Cubre el pedido # | Estado |
|---|---|---|---|
| Buscador Inteligente en Lenguaje Natural sobre Licitaciones | [`018-buscador-inteligente-nl`](018-buscador-inteligente-nl/spec.md) | 2 | **Implementado y cerrado 2026-07-16**, validado en vivo contra Docker + Vertex AI real (US1/US2/US3, incluida degradación FR-005 confirmada con un 429 real de cuota). Reemplaza el endpoint `buscar-natural` literal por interpretación semántica vía Gemini 2.5 Flash-Lite. Detalle en `tasks.md` del spec. |

### 🔜 N4 — Julio-Agosto 2026

| Feature | Spec | Cubre el pedido # | Cambio por repriorización |
|---|---|---|---|
| Fase 7 — Pipeline de Oportunidades (incluye motor de validación de completitud, US2) | [`004-fase7-pipeline-oportunidades`](004-fase7-pipeline-oportunidades/spec.md) | 6 | Última fase priorizada del bloque funcional urgente. El "motor de validación antes de enviar" ya estaba cubierto por la US2 existente — no requirió spec nueva. |

### 🟡 Paralelo — sin fecha fija, nunca desplaza N1-N4

| Feature | Spec | Cubre el pedido # | Cambio por repriorización |
|---|---|---|---|
| Rediseño Frontend de MPM | [`019-rediseno-frontend`](019-rediseno-frontend/spec.md) | 10 | **Spec nueva**, creada 2026-07-03 vía spec-kit. Se ejecuta en tiempo disponible del equipo, en paralelo a N1-N4 o después de cerrar Pipeline (N4) — explícitamente nunca compite por prioridad con el bloque funcional. |

### ⏸️ En pausa — Fases 8-18

El cliente pidió foco exclusivo en su punto de dolor actual. Estas fases quedan **especificadas pero pausadas**, sin trabajo ni fecha, hasta nueva instrucción:

| Feature | Spec |
|---|---|
| Fase 8 — Análisis IA de Bases de Licitación | [`005-fase8-analisis-bases`](005-fase8-analisis-bases/spec.md) |
| Fase 9 — Reportes Ejecutivos Automáticos | [`006-fase9-reportes-ejecutivos`](006-fase9-reportes-ejecutivos/spec.md) |
| Fase 10 — Notificaciones Multicanal | [`007-fase10-notificaciones-multicanal`](007-fase10-notificaciones-multicanal/spec.md) |
| Fase 11 — Inteligencia Competitiva Avanzada | [`008-fase11-inteligencia-competitiva`](008-fase11-inteligencia-competitiva/spec.md) |
| Fase 12 — Gestión de Garantías | [`009-fase12-garantias`](009-fase12-garantias/spec.md) |
| Fase 13 — CRM de Organismos Compradores | [`010-fase13-crm-organismos`](010-fase13-crm-organismos/spec.md) |
| Fase 14 — Predictor de Éxito | [`011-fase14-predictor-exito`](011-fase14-predictor-exito/spec.md) |
| Fase 15 — Pricing Intelligence | [`012-fase15-pricing-intelligence`](012-fase15-pricing-intelligence/spec.md) |
| Fase 16 — Portal de Revisión Externa | [`013-fase16-portal-colaboracion`](013-fase16-portal-colaboracion/spec.md) |
| Fase 17 — Gestión Documental de Propuestas | [`014-fase17-gestion-documental`](014-fase17-gestion-documental/spec.md) |
| Fase 18 — Integración ERP (SAP / Oracle) | [`015-fase18-integracion-erp`](015-fase18-integracion-erp/spec.md) |

Fase 11 (Inteligencia Competitiva) sigue siendo la candidata más probable a reactivarse primero si el cliente lo pide — es el único pedido de este bloque (#7) mencionado explícitamente en las reuniones de alcance.

---

## Decisiones tomadas y su justificación

1. **GCP entra como N1, por delante de Alertas**: el cliente lo pidió como "lo que haremos ahora" el 2026-07-03. Tiene sentido de producto además: ninguna fase funcional (Alertas, Buscador, Pipeline) sirve si el sistema no está en un lugar donde el equipo comercial pueda usarlo fuera del laptop de un desarrollador.
2. **Alertas (003) sigue antes que Buscador (018)**: aunque el Buscador fue lo primero que pidió el cliente (reunión de junio), en la reunión de julio se re-secuenciaron explícitamente: análisis histórico → alertas → validación. Esa decisión se mantiene sin cambios en esta ronda.
3. **No se creó un módulo nuevo para "validación de completitud de ofertas"**: ya existe como User Story 2 de Fase 7. Se documentó la relación en vez de duplicar alcance.
4. **Fases 8-18 se pausan, no se eliminan**: siguen especificadas (`spec.md` + `plan.md` existentes) para retomarlas sin perder el trabajo de definición ya hecho, pero no se ejecuta trabajo de implementación en ellas hasta que el cliente lo pida.
5. **Rediseño Frontend es explícitamente no-bloqueante**: se especificó con un criterio de éxito propio (SC-004: "nunca desplaza Alertas/Buscador/Pipeline") para que quede claro en el spec mismo, no solo en este roadmap, que no debe competir por tiempo del equipo con el bloque funcional urgente.
6. **Reuniones semanales de seguimiento** (pedido #8): no es una feature de producto, es gobernanza — se registra como recordatorio operativo. Coordinar con Francisco Lopez Balart, miércoles 16:00, 20-30 min.
7. **`016-extraccion-documentos-api` se mueve de "Hecho" a "Backlog técnico"** (2026-07-06): estaba mal marcada como completada. Se mantiene fuera de la secuencia N1-N4 porque no es un pedido directo del cliente, pero se documenta su relación con `002-fase5-deploy-gcp`: es el prerequisito técnico para eventualmente migrar de Compute Engine a Cloud Run + Cloud Run Jobs (elimina el uso continuo de Chromium/Playwright embebido, que hoy es el principal motivo por el que Cloud Run no es viable). No bloquea N1 — Compute Engine sigue siendo la decisión correcta para el deploy inmediato.
8. **Deadline exacto de CU010 confirmado 2026-07-06**: jueves 9 de julio, 7:59 a.m. — todo el sistema (Fase 5 + módulo de Alertas) debe estar operativo. Fuente: reunión interna "[EQUIPO-IA] Reunión General", no una fecha límite comunicada directamente por Francisco en las actas anteriores, pero tratada internamente como firme. **Aclaración del usuario (2026-07-06)**: Manuel sigue una cadencia semanal de presentar una funcionalidad nueva cada jueves — la de esta semana es el módulo de Alertas (003). Esto significa que 003 no es "trabajo en paralelo mientras se espera a Nicolás", es la entrega concreta que se presenta el jueves, con la misma urgencia que el deploy.
9. **Bot de Telegram (pedido #12) se agrega a `003-fase6-alertas-keywords` como User Story 5, prioridad P2**: es instrucción interna de Manuel, no bloquea el "go/no-go" del cliente (ya cubierto por notificación in-app), pero se pide explícitamente para la demo del jueves — no se crea una spec nueva porque encaja naturalmente como un canal adicional de la misma feature de Alertas.

## Próximos pasos inmediatos

- [ ] Ejecutar `/speckit-plan` (research.md) sobre `002-fase5-deploy-gcp` para decidir Compute Engine vs. Cloud Run y Cloud SQL vs. Postgres en contenedor — es el bloqueante inmediato del N1.
- [ ] Ejecutar `/speckit-tasks` sobre `002-fase5-deploy-gcp`, `003-fase6-alertas-keywords` (alcance ampliado) y `018-buscador-inteligente-nl` para generar `tasks.md` antes de implementar.
- [ ] Confirmar región GCP, presupuesto mensual aprobado y dominio a usar para el despliegue (bloqueantes listados en `002-fase5-deploy-gcp/plan.md`).
- [ ] Validar con Francisco en la próxima reunión semanal si Inteligencia Competitiva (Fase 11) debe ser la primera en reactivarse cuando se retome el bloque pausado.
- [ ] Confirmar el número de migración exacto (`VXXX`) al implementar cada fase — la última aplicada es V077.
