# MPM — Roadmap de Funcionalidades: Resumen y Procedimiento

**Generado**: 2026-06-24 | **Sistema**: MPM CU010 — Mercado Público TIVIT

---

## Qué se creó en esta sesión

### 1. Archivo Excel (CSV)
`docs/MPM-Roadmap-Funcionalidades.csv`

Abre con Excel. Contiene las 14 funcionalidades planificadas con:
- Fase y semana de implementación
- Nombre, descripción y módulo
- Impacto, complejidad y dependencias
- Valor de negocio y carpeta spec vinculada

**Para abrir en Excel con formato correcto:**
1. Doble clic en el archivo `.csv`
2. Si los datos aparecen en una sola columna: Datos → Texto en columnas → Delimitado por `;`
3. Para formato visual completo: copiar los datos a una hoja nueva y aplicar estilos de tabla

---

### 2. Carpetas de especificación (14 fases)

Cada fase tiene su propia carpeta en `specs/` con:
- **`spec.md`** — Especificación completa con user stories, escenarios de aceptación y definición de hecho
- **`plan.md`** — Plan técnico con módulo, estructura de archivos y constitution check

| # | Carpeta | Funcionalidad |
|---|---------|--------------|
| 5 | `specs/002-fase5-deploy-gcp/` | Deploy GCP + CI/CD |
| 6 | `specs/003-fase6-alertas-keywords/` | Alertas por Palabras Clave |
| 7 | `specs/004-fase7-pipeline-oportunidades/` | Pipeline de Oportunidades |
| 8 | `specs/005-fase8-analisis-bases/` | Análisis IA de Bases de Licitación |
| 9 | `specs/006-fase9-reportes-ejecutivos/` | Reportes Ejecutivos Automáticos |
| 10 | `specs/007-fase10-notificaciones-multicanal/` | Notificaciones Multicanal |
| 11 | `specs/008-fase11-inteligencia-competitiva/` | Inteligencia Competitiva Avanzada |
| 12 | `specs/009-fase12-garantias/` | Gestión de Garantías |
| 13 | `specs/010-fase13-crm-organismos/` | CRM de Organismos Compradores |
| 14 | `specs/011-fase14-predictor-exito/` | Predictor de Éxito |
| 15 | `specs/012-fase15-pricing-intelligence/` | Pricing Intelligence |
| 16 | `specs/013-fase16-portal-colaboracion/` | Portal de Revisión Externa |
| 17 | `specs/014-fase17-gestion-documental/` | Gestión Documental de Propuestas |
| 18 | `specs/015-fase18-integracion-erp/` | Integración ERP (SAP/Oracle) |

---

## Cómo proceder con cada fase

El flujo estándar por fase es el siguiente (usa el sistema speckit ya configurado):

### Paso 1 — Seleccionar la fase a implementar

Leer el `spec.md` de la fase para confirmar que el alcance está alineado con las prioridades del negocio. Si hay cambios, editar el spec antes de continuar.

### Paso 2 — Actualizar el CLAUDE.md

En `CLAUDE.md`, actualizar la sección de fase actual:

```
Current feature: Fase X — [Nombre]
Spec: specs/00X-faseX-slug/spec.md
```

### Paso 3 — Generar el plan completo

```
/speckit-plan
```

Esto genera (para la fase seleccionada):
- `research.md` — decisiones técnicas y trade-offs
- `data-model.md` — entidades y relaciones de BD
- `contracts/` — especificación de endpoints REST
- `quickstart.md` — escenarios de validación end-to-end

### Paso 4 — Generar el task breakdown

```
/speckit-tasks
```

Genera `tasks.md` con todas las tareas en orden de ejecución, agrupadas por user story, con IDs y marcadores de paralelismo.

### Paso 5 — Implementar

```
/speckit-implement
```

Ejecuta todas las tareas del `tasks.md` en orden. Las marca `[X]` al completarlas.

### Paso 6 — Verificar

```
/verify
```

Verifica que los cambios funcionan correctamente en el sistema corriendo.

### Paso 7 — Code review (opcional, recomendado antes de demo ejecutiva)

```
/code-review
```

---

## Mapa de dependencias

```
Fase 5 (Deploy GCP)  ←── PREREQUISITO DE TODAS
    │
    ├── Fase 6 (Alertas Keywords)
    │       └── Fase 10 (Multicanal)
    │
    ├── Fase 7 (Pipeline)
    │       ├── Fase 9 (Reportes)
    │       ├── Fase 12 (Garantías)
    │       ├── Fase 13 (CRM)
    │       ├── Fase 17 (Documentos)
    │       └── Fase 18 (ERP)
    │
    └── Fase 8 (Bases IA)
            ├── Fase 11 (Competidores)
            │       ├── Fase 14 (Predictor)
            │       └── Fase 15 (Pricing)
            └── Fase 16 (Portal)
```

---

## Criterios de priorización

| Criterio | Peso | Fases con mayor puntaje |
|----------|------|------------------------|
| Impacto negocio directo (win rate, visibilidad ejecutiva) | Alto | 5, 6, 7, 8, 11, 14, 15 |
| Ahorro de tiempo operativo | Medio | 8, 9, 17 |
| Reducción de riesgo (descalificaciones, garantías) | Medio | 8, 12, 17 |
| Diferenciador tecnológico (IA, ML) | Alto | 8, 11, 14, 15 |
| Complejidad baja / retorno rápido | Alto | 6, 8, 9, 10, 16 |

**Recomendación de orden MVP:**
1. Fase 5 (deploy) — habilita todo
2. Fase 6 (alertas) — bajo esfuerzo, alto valor inmediato
3. Fase 7 (pipeline) — operación comercial estructurada
4. Fase 8 (bases IA) — diferenciador clave, habilita 4 fases futuras

---

## Estado actual del sistema (Fases 1–4 completadas)

| Fase | Nombre | Estado |
|------|--------|--------|
| 1 | Auth + Infraestructura base | ✅ Completado |
| 2 | Scraping automático + Pipeline Gemini IA | ✅ Completado |
| 3 | Dashboard Ejecutivo Comparativo | ✅ Completado |
| 4 | Seguimiento activo + Notificaciones in-app | ✅ Completado |
| **5–18** | **Roadmap de nuevas funcionalidades** | **📋 Planificado** |
