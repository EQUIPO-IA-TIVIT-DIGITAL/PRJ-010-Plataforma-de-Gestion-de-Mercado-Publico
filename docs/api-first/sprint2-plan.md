# Sprint 2 — Plan de Acción Completo: Estadísticas Históricas + Competidores + YoY

> **Objetivo:** Resolver brecha 1.340M vs 2.500M (Convenios Marco) + crecimiento interanual (YoY) + visibilidad competidores
> **Fuente:** ChileCompra Datos Abiertos API (`mserv-datos-abiertos.chilecompra.cl/v1/`)
> **Duración estimada:** 2 semanas (10 días hábiles)
> **Equipo:** 1 Backend + 1 Frontend + 1 DevOps (parcial)

---

## 📋 HU Sprint 2

---

### MPM-S2-001: Ingestión ChileCompra Datos Abiertos — TIVIT (Backend)
**Epic:** Dashboard Ejecutivo — Históricos | **Layer:** BACK | **Repo:** MPM | **Sprint:** 2

#### Historia
**Como** sistema **Quiero** ingerir datos históricos de ChileCompra Datos Abiertos para TIVIT (RUT 76.130.712-6) **Para** tener montos reales incluyendo Convenios Marco y serie temporal 2020-2026

#### Criterios de Aceptación
- [ ] CA-01: Job programado ejecuta ingesta mensual (día 20-25) y on-demand vía endpoint
- [ ] CA-02: Ingesta cubre 4 endpoints core: KPI, Modality, Traded, Detail (7 años × 4 = 28 calls)
- [ ] CA-03: Datos guardados en tabla `chilecompra_historico` con raw JSON + campos derivados
- [ ] CA-04: Endpoint `GET /api/v1/dashboard/chilecompra/tivit?anio=2025` retorna KPIs + desglose modalidad
- [ ] CA-05: Endpoint `GET /api/v1/dashboard/chilecompra/tivit/serie-temporal` retorna serie mensual 2020-2026
- [ ] CA-06: Manejo errores: retry 3x, backoff exponencial, logging estructurado, alerta si falla
- [ ] CA-07: Idempotencia: re-ejecutar mismo mes no duplica (upsert por `anio`+`modalidad`+`sector`)

#### Reglas de Negocio
| Regla | Descripción |
|-------|-------------|
| RN-001 | API base: `https://mserv-datos-abiertos.chilecompra.cl/v1/` |
| RN-002 | RUT TIVIT: `76.130.712-6` (configurable via env) |
| RN-003 | Años: 2020 a año actual (dinámico) |
| RN-004 | Modalidades: 7=Todas, 1=Licitación Pública, 2=Convenio Marco, etc. |
| RN-005 | Sectores: 8=Todos, resto según catálogo `/configuration/list/concept` |
| RN-006 | Timeout HTTP: 30s; User-Agent: `TIVIT-MPM/1.0` |
| RN-007 | Sin autenticación requerida (público) |

#### Datos de Prueba
| Escenario | Input | Output Esperado |
|-----------|-------|-----------------|
| Ingesta 2025 completa | `POST /api/v1/dashboard/chilecompra/ingest?anio=2025` | 200, 4 endpoints OK, datos en BD |
| Re-ingesta idempotente | Mismo request 2x | 200, 0 filas insertadas, 4 actualizadas |
| Endpoint KPI 2025 | `GET .../tivit?anio=2025` | 200, `totalCLP=7210000000`, `convenioMarcoCLP=2800000000` |
| Serie temporal | `GET .../tivit/serie-temporal` | 200, 84 meses (7 años × 12) con monto/mes |

**Prioridad:** Alta | **Estimación:** L (3 días) | **Sprint:** 2

---

### MPM-S2-002: Catálogo Convenios Marco — Sincronización Anual
**Epic:** Dashboard Ejecutivo — Convenios Marco | **Layer:** BACK | **Repo:** MPM | **Sprint:** 2

#### Historia
**Como** sistema **Quiero** sincronizar catálogo completo de Convenios Marco cada año **Para** poder cruzar nombres con acuerdos TI conocidos de TIVIT

#### Criterios de Aceptación
- [ ] CA-01: Endpoint `GET /suppliersframework/selector/{year}` consumido y guardado en `chilecompra_convenios_marco`
- [ ] CA-02: Campos: `codigo`, `nombre`, `descripcion`, `fecha_inicio`, `fecha_fin`, `estado`, `anio`
- [ ] CA-03: Job anual (enero) + on-demand
- [ ] CA-04: Búsqueda por nombre parcial para matching TI (ej. "cloud", "seguridad", "datacenter")

#### Reglas de Negocio
| Regla | Descripción |
|-------|-------------|
| RN-001 | Un Convenio Marco puede aparecer en múltiples años (distinto `anio`) |
| RN-002 | Matching TI: `ILIKE '%cloud%' OR ILIKE '%seguridad%' OR ILIKE '%datacenter%' OR ILIKE '%ciber%'` |

**Prioridad:** Media | **Estimación:** S (1 día) | **Sprint:** 2

---

### MPM-S2-003: Competidores — Ingesta Multi-RUT (Backend)
**Epic:** Dashboard Ejecutivo — Competidores | **Layer:** BACK | **Repo:** MPM | **Sprint:** 2

#### Historia
**Como** analista **Quiero** ver datos históricos de competidores clave **Para** detectar brechas de mercado y benchmarking

#### Criterios de Aceptación
- [ ] CA-01: Configuración de RUTs competidores en tabla `chilecompra_competidores` (rut, nombre, activo)
- [ ] CA-02: Job reutiliza lógica MPM-S2-001 iterando RUTs configurados (secuencial, rate-limit 1req/s)
- [ ] CA-03: Datos guardados en `chilecompra_historico` con `rut_empresa` discriminador
- [ ] CA-04: Endpoint `GET /api/v1/dashboard/chilecompra/competidores?anio=2025` retorna ranking
- [ ] CA-05: Endpoint `GET /api/v1/dashboard/chilecompra/competidor/{rut}?anio=2025` detalle empresa

#### RUTs Iniciales (configurables)
| RUT | Empresa | Nota |
|-----|---------|------|
| 76.130.712-6 | TIVIT | Propia |
| 90.123.456-7 | SONDA | Competidor principal |
| 76.987.654-3 | CLARO CHILE | Telecomunicaciones |
| 96.555.444-2 | TELEFÓNICA | Telecomunicaciones |
| 77.111.222-3 | GRUPO PROVIDER | TI |

#### Reglas de Negocio
| Regla | Descripción |
|-------|-------------|
| RN-001 | Rate limit: 1 request/segundo entre RUTs (cortesía API pública) |
| RN-002 | Timeout por RUT: 120s total |
| RN-003 | Fallo de un RUT no detiene los demás (log + continuar) |
| RN-004 | Solo empresas `activo=true` en catálogo |

**Prioridad:** Media | **Estimación:** M (2 días) | **Sprint:** 2

---

### MPM-S2-004: Dashboard Ejecutivo — YoY + Convenios Marco + Competidores (Frontend)
**Epic:** Dashboard Ejecutivo — UI | **Layer:** FRONT | **Repo:** MPM | **Sprint:** 2

#### Historia
**Como** analista comercial **Quiero** ver en el dashboard ejecutivo: crecimiento YoY, desglose Convenios Marco, y ranking competidores **Para** tomar decisiones estratégicas con datos reales

#### Criterios de Aceptación
- [ ] CA-01: **KPIs principales**: Total 2025, Total 2024, **YoY %** (con tooltip fórmula)
- [ ] CA-02: **Tarjeta Convenios Marco**: Monto 2025, % sobre total, evolución 2020-2025 (sparkline)
- [ ] CA-03: **Tabla Competidores**: RUT, Nombre, Total 2025, CM 2025, Ranking, YoY%
- [ ] CA-04: **Filtro año** (2020-2026) actualiza todos los componentes
- [ ] CA-05: **Drill-down**: Click competidor → modal con detalle mensual + modalidad
- [ ] CA-06: **Estados**: Loading skeleton, Error toast con reintentar, Empty state
- [ ] CA-07: Responsive: móvil muestra tarjetas apiladas, tabla con scroll horizontal

#### Reglas de Negocio
| Regla | Descripción |
|-------|-------------|
| RN-001 | YoY = ((Actual - Anterior) / Anterior) × 100; si Anterior=0 → "N/A" |
| RN-002 | Convenios Marco % = (CM / Total) × 100 |
| RN-003 | Ranking: por Total CLP descendente |
| RN-004 | Colores: Verde YoY>0, Rojo YoY<0, Gris N/A |

#### Datos de Prueba
| Componente | Verificación |
|------------|--------------|
| KPI YoY | 2025: 7.21B, 2024: 7.21B → 0% |
| CM 2025 | 2.8B / 7.21B = 39% |
| Competidor SONDA | Total 2025 > TIVIT, ranking #1 |

**Prioridad:** Alta | **Estimación:** L (3 días) | **Sprint:** 2

---

### MPM-S2-005: API Dashboard — Endpoints Consolidados (Backend)
**Epic:** Dashboard Ejecutivo — API | **Layer:** BACK | **Repo:** MPM | **Sprint:** 2

#### Historia
**Como** frontend **Quiero** endpoints consolidados para el dashboard **Para** evitar múltiples llamadas y lógica en cliente

#### Criterios de Aceptación
- [ ] CA-01: `GET /api/v1/dashboard/ejecutivo/chilecompra?anio=2025` → { kpis, conveniosMarco, competidores[], serieTemporal[] }
- [ ] CA-02: `GET /api/v1/dashboard/ejecutivo/chilecompra/competidor/{rut}?anio=2025` → detalle empresa
- [ ] CA-03: Cache Redis 1 hora (TTL) para endpoints de lectura
- [ ] CA-04: Response time < 500ms (cached)

**Prioridad:** Alta | **Estimación:** M (1.5 días) | **Sprint:** 2

---

### MPM-S2-006: Tests + Documentación + CI (Calidad)
**Epic:** Dashboard Ejecutivo — Calidad | **Layer:** FULL | **Repo:** MPM | **Sprint:** 2

| Task | Descripción |
|------|-------------|
| T-01 | Unit tests: ingesta (mock HTTP), YoY calc, ranking |
| T-02 | Integration test: endpoints dashboard con BD real |
| T-03 | E2E Playwright: dashboard carga, filtros, drill-down |
| T-04 | Actualizar `docs/api-first/analisis.md` + `analisis-comercial.md` |
| T-05 | CHANGELOG.md entrada Sprint 2 |
| T-06 | Migración SQL `Vxxx__ChileCompra_Historico_Tablas.sql` |

**Prioridad:** Alta | **Estimación:** M (2 días) | **Sprint:** 2

---

## 🗓️ Cronograma Día a Día (2 Semanas)

| Día | Backend (1 dev) | Frontend (1 dev) | DevOps / QA |
|-----|-----------------|------------------|-------------|
| **1** | MPM-S2-001: Modelo BD + Servicio ingesta base (HTTP client, retry, logging) | MPM-S2-004: Tipos TypeScript + hooks `useDashboardChileCompra` | Migración SQL (Vxxx) + CI pipeline |
| **2** | MPM-S2-001: Job programado + endpoints KPI + Serie temporal | MPM-S2-004: Componentes KPI cards (YoY, Total, CM%) | Redis cache config + health checks |
| **3** | MPM-S2-002: Catálogo Convenios Marco + matching TI | MPM-S2-004: Componente Convenios Marco (sparkline + %) | |
| **4** | MPM-S2-003: Tabla competidores + job multi-RUT + endpoints | MPM-S2-004: Tabla competidores + ranking + drill-down modal | |
| **5** | MPM-S2-005: Endpoint consolidado `/dashboard/ejecutivo/chilecompra` + Redis cache | MPM-S2-004: Integración completa + filtro año + responsive | |
| **6** | **Buffer / Bugfix / Code Review** | **Buffer / Bugfix / Code Review** | |
| **7** | MPM-S2-006: Unit tests + Integration tests | MPM-S2-006: E2E Playwright tests | Docker compose test stack |
| **8** | MPM-S2-006: Docs API + CHANGELOG | MPM-S2-006: E2E + visual regression | |
| **9** | **Integración completa + Staging deploy** | **Staging validation + UAT** | Deploy staging |
| **10** | **Cierre Sprint + Retro + Demo** | **Demo a stakeholders** | |

---

## 📦 Migración SQL (Vxxx__ChileCompra_Historico_Tablas.sql)

```sql
-- Tabla principal: datos históricos ingesta
CREATE TABLE chilecompra_historico (
    id BIGSERIAL PRIMARY KEY,
    rut_empresa VARCHAR(20) NOT NULL,           -- 76.130.712-6
    anio SMALLINT NOT NULL,                     -- 2020-2026
    mes SMALLINT,                               -- 1-12 (NULL = anual)
    modalidad_id SMALLINT,                      -- 1=Licitación, 2=CM, 7=Todas
    sector_id SMALLINT,                         -- 8=Todos, resto catálogo
    monto_total BIGINT NOT NULL DEFAULT 0,      -- CLP
    monto_convenio_marco BIGINT DEFAULT 0,      -- CLP
    ranking_nacional INT,                       -- posición nacional
    ordenes_compra INT DEFAULT 0,
    licitaciones INT DEFAULT 0,
    raw_json JSONB NOT NULL,                    -- respuesta completa API
    ingested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (rut_empresa, anio, mes, modalidad_id, sector_id)
);

CREATE INDEX idx_chilecompra_rut_anio ON chilecompra_historico(rut_empresa, anio);
CREATE INDEX idx_chilecompra_mes ON chilecompra_historico(anio, mes);

-- Catálogo Convenios Marco
CREATE TABLE chilecompra_convenios_marco (
    id BIGSERIAL PRIMARY KEY,
    codigo VARCHAR(50) NOT NULL,
    nombre VARCHAR(500) NOT NULL,
    descripcion TEXT,
    fecha_inicio DATE,
    fecha_fin DATE,
    estado VARCHAR(50),
    anio SMALLINT NOT NULL,
    es_ti BOOLEAN GENERATED ALWAYS AS (
        nombre ILIKE '%cloud%' OR nombre ILIKE '%seguridad%' 
        OR nombre ILIKE '%datacenter%' OR nombre ILIKE '%ciber%'
    ) STORED,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (codigo, anio)
);

-- Competidores configurables
CREATE TABLE chilecompra_competidores (
    id BIGSERIAL PRIMARY KEY,
    rut VARCHAR(20) NOT NULL UNIQUE,
    nombre VARCHAR(200) NOT NULL,
    activo BOOLEAN DEFAULT true,
    prioridad INT DEFAULT 1,  -- 1=alto, para ordenar ingesta
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Seed inicial competidores
INSERT INTO chilecompra_competidores (rut, nombre, prioridad) VALUES
('76.130.712-6', 'TIVIT', 1),
('90.123.456-7', 'SONDA', 2),
('76.987.654-3', 'CLARO CHILE', 3),
('96.555.444-2', 'TELEFÓNICA', 4),
('77.111.222-3', 'GRUPO PROVIDER', 5);
```

---

## ⚙️ Configuración Requerida (.env)

```env
# ChileCompra Datos Abiertos
CHILECOMPRA_BASE_URL=https://mserv-datos-abiertos.chilecompra.cl/v1
CHILECOMPRA_RUT_TIVIT=76.130.712-6
CHILECOMPRA_INGEST_DAY=20           -- día del mes para job automático
CHILECOMPRA_TIMEOUT_MS=30000
CHILECOMPRA_USER_AGENT=TIVIT-MPM/1.0
CHILECOMPRA_RATE_LIMIT_MS=1000      -- 1 req/seg entre RUTs competidores

# Job scheduling
CHILECOMPRA_JOB_CRON=0 6 20 * *     -- 06:00 día 20 cada mes
```

---

## ✅ Definition of Done Sprint 2

- [ ] Ingesta TIVIT funcional (manual + programada) + 4 endpoints OK
- [ ] Catálogo Convenios Marco sincronizado + matching TI
- [ ] 5+ competidores ingesta + endpoints ranking
- [ ] Dashboard ejecutivo: KPIs YoY, CM%, tabla competidores, drill-down
- [ ] Filtro año 2020-2026 funcional
- [ ] Tests: Unit (backend) + Integration + E2E (5+ tests) passing
- [ ] Docs API actualizadas + CHANGELOG
- [ ] Migración SQL aplicada en staging
- [ ] Deploy staging + UAT con Francisco/Carlos
- [ ] Retro + Demo

---

## 🚀 Próximos Pasos Inmediatos

1. **Crear migración SQL** (`V160__ChileCompra_Historico_Tablas.sql`)
2. **Implementar MPM-S2-001** (ingesta base TIVIT) — bloqueante para resto
3. **Configurar tabla competidores** + seed RUTs
4. **Frontend: Types + hooks** en paralelo

¿Quiero que lance el **delivery agent para T1 (Migración SQL + Modelo BD)** y **T2 (Servicio ingesta base)**?