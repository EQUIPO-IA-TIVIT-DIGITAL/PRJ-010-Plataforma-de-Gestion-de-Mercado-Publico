# HU Sprint 1 — Mejoras Licitaciones (Filtro Monto, Presupuesto, Institución)

---

## MPM-LIC-001: Filtro por monto mínimo en listado de licitaciones
**Epic:** Licitaciones — Filtros Avanzados | **Layer:** FULL | **Repo:** MPM | **Sprint:** 1

### Historia
**Como** analista comercial de TIVIT  
**Quiero** filtrar las licitaciones para ocultar las que están por debajo de un monto mínimo definido  
**Para** no llenar de ruido visual la grilla y enfocarme solo en oportunidades relevantes (ej. > 50M CLP)

### Criterios de Aceptación
- [ ] **CA-01**: El `LicitacionFilterBar` incluye un nuevo campo numérico "Monto mínimo (CLP)" con placeholder "Ej: 50000000"
- [ ] **CA-02**: Al ingresar un valor y aplicar filtros, el listado solo muestra licitaciones con `monto_estimado >= valor_ingresado`
- [ ] **CA-03**: El filtro funciona combinado con los filtros existentes (estado, tipo, organismo, área, fechas, búsqueda)
- [ ] **CA-04**: El filtro acepta valores enteros positivos; valor vacío o 0 = sin filtro (comportamiento actual)
- [ ] **CA-05**: El valor del filtro persiste en la URL (query param `montoMinimo`) para compartir/bookmark
- [ ] **CA-06**: Botón "Reiniciar filtros" limpia también el campo monto mínimo
- [ ] **CA-07**: Respuesta API incluye `montoMinimo` aplicado en metadata para trazabilidad

### Reglas de Negocio
| Regla | Descripción |
|-------|-------------|
| RN-001 | El filtro compara contra `licitaciones.monto_estimado` (decimal, CLP) |
| RN-002 | Licitaciones con `monto_estimado = NULL` se excluyen si hay filtro activo (no cumplen >= X) |
| RN-003 | El parámetro API es opcional: `montoMinimo` (decimal, nullable) |
| RN-004 | Validación backend: `montoMinimo > 0` si se envía, sino 400 `VAL_001` |

### Datos de Prueba
| Escenario | Input (query params) | Output Esperado |
|-----------|---------------------|-----------------|
| Happy path | `?montoMinimo=50000000&page=1` | 200, solo licitaciones ≥ 50M CLP |
| Sin filtro | `?page=1` (sin montoMinimo) | 200, comportamiento actual (todas) |
| Valor 0 | `?montoMinimo=0` | 200, igual que sin filtro |
| Inválido (negativo) | `?montoMinimo=-100` | 400, `VAL_001` "Monto mínimo debe ser positivo" |
| Combinado con estado | `?montoMinimo=50000000&estado=1` | 200, Publicadas ≥ 50M |
| Combinado con área | `?montoMinimo=50000000&area=cloud` | 200, Cloud ≥ 50M |

### Dependencias Técnicas
- SP `usp_Licitaciones_Listar`: nuevo parámetro `p_monto_minimo` (decimal, nullable)
- Endpoint `GET /api/v1/licitaciones`: nuevo query param `montoMinimo`
- DTO `LicitacionResumen`: sin cambios (ya trae `montoEstimado`)
- Frontend: `LicitacionFilterBar.tsx`, `useLicitaciones.ts` hook

**Prioridad:** Alta | **Estimación:** S (½ día) | **Sprint:** 1

---

## MPM-LIC-002: Mostrar presupuesto (monto estimado) en tarjetas de listado
**Epic:** Licitaciones — Visualización | **Layer:** FULL | **Repo:** MPM | **Sprint:** 1

### Historia
**Como** analista comercial de TIVIT  
**Quiero** ver el monto estimado/presupuesto directamente en la grilla de licitaciones  
**Para** ordenar y priorizar visualmente sin tener que abrir cada detalle

### Criterios de Aceptación
- [ ] **CA-01**: Cada tarjeta de licitación en `LicitacionesPage` muestra "Presupuesto: $X" formateado (ej: "$45.000.000 CLP")
- [ ] **CA-02**: Si `monto_estimado` es NULL, muestra "Presupuesto: —" (sin romper layout)
- [ ] **CA-03**: El campo es ordenable: click en header "Presupuesto" → `sortBy=monto_estimado&sortDir=desc/asc`
- [ ] **CA-04**: Formato: separador de miles (punto), 2 decimales, sufijo " CLP" (ej: "1.250.000.000,00 CLP")
- [ ] **CA-05**: Responsive: en móvil (< 640px) muestra solo monto formateado sin label "Presupuesto:"

### Reglas de Negocio
| Regla | Descripción |
|-------|-------------|
| RN-001 | Usa `licitaciones.monto_estimado` ya existente en `usp_Licitaciones_Listar` |
| RN-002 | Ordenamiento por `monto_estimado DESC NULLS LAST` (mayores primero, NULLs al final) |
| RN-003 | Formato CLP: `new Intl.NumberFormat('es-CL', {style: 'currency', currency: 'CLP', minimumFractionDigits: 0})` |

### Datos de Prueba
| Escenario | Data BD | Render Esperado |
|-----------|---------|-----------------|
| Con monto | `monto_estimado = 45000000` | "$45.000.000 CLP" |
| Monto grande | `monto_estimado = 1250000000` | "$1.250.000.000 CLP" |
| NULL | `monto_estimado = NULL` | "—" |
| Cero | `monto_estimado = 0` | "$0 CLP" |

### Dependencias Técnicas
- SP `usp_Licitaciones_Listar`: ya retorna `monto_estimado` → solo agregar a `ORDER BY` seguro
- DTO `LicitacionResumen`: ya incluye `montoEstimado` → sin cambios
- Frontend: `LicitacionesPage.tsx` (columna/render), `LicitacionFilterBar` (sort option)

**Prioridad:** Alta | **Estimación:** S (½ día) | **Sprint:** 1

---

## MPM-LIC-003: Mostrar nombre de institución en tarjetas de listado
**Epic:** Licitaciones — Visualización | **Layer:** FULL | **Repo:** MPM | **Sprint:** 1

### Historia
**Como** analista comercial de TIVIT  
**Quiero** ver el nombre de la institución/organismo en cada licitación de la grilla  
**Para** identificar rápidamente quién compra sin abrir el detalle

### Criterios de Aceptación
- [ ] **CA-01**: Cada tarjeta muestra "Institución: {organismo}" bajo el nombre de la licitación
- [ ] **CA-02**: Si `organismo` es NULL/empty, muestra "Institución: —"
- [ ] **CA-03**: El texto es truncado con ellipsis a 1 línea (max-width responsive)
- [ ] **CA-04**: Tooltip nativo (`title`) muestra el nombre completo al hacer hover
- [ ] **CA-05**: El campo `organismo` ya existe en SP y DTO → solo rendering frontend

### Reglas de Negocio
| Regla | Descripción |
|-------|-------------|
| RN-001 | Usa `licitaciones.organismo` (varchar) ya retornado por `usp_Licitaciones_Listar` |
| RN-002 | No es ordenable ni filtro en este sprint (ya existe filtro `organismo` por texto libre) |

### Datos de Prueba
| Escenario | Data BD | Render Esperado |
|-----------|---------|-----------------|
| Con organismo | `organismo = 'Municipalidad de Santiago'` | "Institución: Municipalidad de Santiago" |
| Nombre largo | `organismo = 'Universidad de Chile - Facultad de Ciencias Físicas y Matemáticas'` | "Institución: Universidad de Chile - Facultad..." (tooltip completo) |
| NULL | `organismo = NULL` | "Institución: —" |
| Empty string | `organismo = ''` | "Institución: —" |

### Dependencias Técnicas
- SP `usp_Licitaciones_Listar`: ya retorna `organismo` → sin cambios
- DTO `LicitacionResumen`: ya incluye `organismo` → sin cambios
- Frontend: `LicitacionesPage.tsx` (render badge/texto)

**Prioridad:** Alta | **Estimación:** S (½ día) | **Sprint:** 1

---

## Resumen Sprint 1

| HU | Backend | Frontend | Esfuerzo Total |
|----|---------|----------|----------------|
| MPM-LIC-001 | SP param + endpoint query param | FilterBar + hook + URL sync | ½ día |
| MPM-LIC-002 | ORDER BY seguro | Tarjeta + sort header + formatter | ½ día |
| MPM-LIC-003 | — (ya existe) | Tarjeta + tooltip | ½ día |

**Total estimado: 1.5 días (1 dev) o 1 día (2 devs en paralelo)**

---

## Actualización a `docs/api-first/licitaciones.md` (Delta)

### 5. REST Endpoints — `GET /api/v1/licitaciones` (actualizado)

**Nuevos parámetros:**

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `montoMinimo` | decimal | No | Filtrar licitaciones con monto estimado >= valor (CLP) |
| `sortBy` | string | No | **Nuevo valor permitido:** `monto_estimado` |

**Response 200 — `LicitacionResumen` (sin cambios, ya incluye `montoEstimado` y `organismo`)**

### 6. Database Objects — `usp_Licitaciones_Listar` (actualizado)

```sql
-- Parámetros existentes +:
p_monto_minimo DECIMAL(18,2) DEFAULT NULL
```

**ORDER BY seguro actualizado:**
```sql
-- Agregar 'monto_estimado' a la lista blanca de columnas ordenables
-- ORDER BY CASE WHEN p_sort_by = 'monto_estimado' THEN monto_estimado END DESC NULLS LAST
```

### 8. Business Rules (nuevas)

| ID | Rule | Category |
|----|------|----------|
| `BUS_LIC_009` | Filtro `montoMinimo` excluye licitaciones con `monto_estimado < valor` o `NULL` | Filtros |
| `BUS_LIC_010` | Ordenamiento por `monto_estimado` usa `DESC NULLS LAST` | Ordenamiento |
| `BUS_LIC_011` | Campo `organismo` mostrado en listado es solo informativo (no nuevo filtro) | Visualización |