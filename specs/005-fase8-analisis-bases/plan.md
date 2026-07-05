# Implementation Plan: Fase 8 — Análisis IA de Bases de Licitación

**Branch**: `005-fase8-analisis-bases` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 4 (Agosto 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Extensión del módulo `MPM.Modules.Analisis` existente para soportar un segundo tipo de análisis: `tipo = 'bases'`. El scraper descarga el PDF de bases junto al acta de evaluación. Un nuevo prompt Gemini especializado extrae: objeto, requisitos técnicos, documentos exigidos, criterios de evaluación, fechas clave y cláusulas de riesgo. El resultado se muestra como ficha en el frontend y se integra con el Pipeline de Oportunidades (Fase 7).

---

## Technical Context

**Lenguaje**: .NET 8 + Node.js (scraper) + React 18
**Extensiones al módulo Analisis existente**: nuevo tipo de workspace `'bases'`
**Prompt Gemini**: especializado para extracción estructurada de bases de licitación
**Storage**: GCS para PDFs de bases (mismo bucket que actas)
**Estimación**: 1 semana | **Complejidad**: Media

---

## Module Structure

**Extensión del módulo existente** `MPM.Modules.Analisis`:

```text
src/MPM.Modules.Analisis/
├── Services/
│   └── AnalisisBasesService.cs       ← Nuevo: orquesta análisis de bases
├── Models/
│   └── BasesLicitacionDto.cs         ← Nuevo: estructura del JSON de bases
└── (resto sin cambios)

tools/scraper-mp/modulos/
└── bases.js                          ← Nuevo: descarga PDF de bases

src/MPM.Api/Database/Scripts/
└── V077__Add_analisis_tipo_bases.sql ← Nuevo tipo permitido en workspace

src/mpm-web/src/
├── pages/BasesAnalisisPage.tsx       ← Nuevo: ficha de bases
└── components/BasesRiesgosPanel.tsx  ← Nuevo: panel de riesgos
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | Extensión del módulo existente |
| **II. Stored Procedures First** | ✅ Sin violación | Reutiliza SPs de Analisis existentes |
| **III. Migraciones SQL** | ✅ Aplicar | V077 para nuevo tipo |
| **IV. Multi-Tenancy** | ✅ Sin violación | Hereda contexto del módulo padre |

---

## Artefactos pendientes

- [ ] `research.md` — prompt engineering para extracción de bases vs. actas
- [ ] `data-model.md` — JSON schema del análisis de bases
- [ ] `quickstart.md` — escenario: licitación nueva → bases descargadas → análisis generado
- [ ] `tasks.md` — generado con `/speckit-tasks`
