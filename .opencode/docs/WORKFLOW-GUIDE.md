# Guía de Workflows — Cómo Trabajar Módulo por Módulo

Esta guía explica cómo usar el Framework Agéntico de TIVIT Digital para diferentes escenarios de desarrollo.

---

## Concepto Clave: Incremental, No Repetitivo

**La regla de oro del framework:**

> **NO repites todo el proceso cada vez.**  
> Solo se activan las skills de lo que realmente cambió.

### ¿Por qué es importante?

- **Eficiencia**: No regeneras código que ya funciona
- **Control**: Cambios quirúrgicos, no masivos
- **Seguridad**: Menos riesgo de romper lo que ya funciona
- **Tiempo**: Actualizaciones en minutos, no horas

---

## Tabla de Decisión Rápida

| Si cambias... | Skills que se activan | Skills que NO se activan | Tiempo |
|--------------|----------------------|--------------------------|--------|
| **Solo DB** | `database-sp`, `data-access` | Frontend, tests UI | ~5 min |
| **Solo API** | `backend-api`, `swagger` | DB (si no cambia), Frontend | ~5 min |
| **Solo UI** | `react`, `design-system` | DB, Backend | ~5 min |
| **Todo** | `agent-fullstack` | Ninguna (todas se ejecutan) | ~15 min |

---

## Workflows por Caso de Uso

### Caso 1: Proyecto Nuevo Desde Cero

**Cuándo usar**: Primera vez que trabajas en un vertical, no existe nada aún.

**Qué escribir en el chat**:

```
Quiero crear un nuevo proyecto agéntico.

Vertical: [banca / gestión de contratos / recursos humanos / etc.]

Tecnologías:
- Backend: .NET 8
- Frontend: React con TypeScript
- Database: SQL Server
```

**Skills activadas** (flujo completo de 25 niveles):

| Fase | Niveles | Skills |
|------|---------|--------|
| Gobierno y dominio | 1-4 | `framework-governance` → `framework-discovery` → `framework-conception` → `framework-pack-design` |
| Arquitectura agéntica | 5-9 | `framework-architecture` → `framework-core-design` → `framework-data-memory-compliance` → `framework-security` → `framework-platform` |
| Scaffold y proyecto | 10-13 | `framework-scaffold-implementation` → `project-bootstrap` → `repo-structure` → `project-architecture` |
| Especificación | 14 | `api-first-spec` |
| Backend | 15-18 | `database-sp` → `data-access` → `backend-api` → `swagger` |
| Frontend | 19-21 | `typescript` → `react-hooks` → `react` |
| Calidad | 22-23 | `framework-qa-validation` → `playwright` |
| Operación y release | 24-25 | `framework-operations-evolution` → `pull-request` |

**Importante**: Los **Niveles 1-13** se ejecutan **una vez por vertical**. Para módulos adicionales del mismo pack, se arranca directamente en Nivel 14.

**Resultado**: Proyecto agéntico con primer módulo funcionando end-to-end.

---

### Caso 2: Módulo Completo (Fullstack)

**Cuándo usar**: Quieres crear un módulo nuevo con base de datos, API y UI completos.

**Qué escribir en el chat**:

```
Quiero crear el módulo de Gestión de Contratos.

Debe poder listar, crear, editar, eliminar y aprobar contratos.

Cada contrato tiene:
- ID
- Número de contrato
- Proveedor (referencia a tabla Proveedores)
- Monto
- Estado (Borrador, En Revisión, Aprobado, Rechazado)
- Fecha de creación
- Fecha de vencimiento
```

**Skills activadas automáticamente**:

| Fase | Skills | Qué hace |
|------|--------|----------|
| **Spec** | `api-first-spec` | Define estructura completa (ERD, endpoints, DTOs, reglas de negocio) |
| **Database** | `database-sp`, `database-modeling`, `database-audit` | Crea tabla + SPs + auditoría |
| **Backend** | `data-access`, `backend-api`, `swagger` | Handlers + Endpoints + Docs |
| **Frontend** | `api-first-frontend`, `react-hooks`, `react`, `design-system` | Types + Hooks + Components + Estilos |
| **Testing** | `agent-qa`, `playwright` | Tests E2E y API |
| **Docs** | `readme`, `changelog` | Documentación actualizada |
| **PR** | `pull-request`, `code-review` | Pull Request listo |

**Tiempo estimado**: 15-30 minutos de interacción.

**Resultado**: Módulo completo funcionando end-to-end.

---

### Caso 3: Solo Backend (Sin Frontend)

**Cuándo usar**: El frontend ya existe o lo harás después. Solo necesitas API.

**Qué escribir en el chat**:

```
Necesito implementar solo el backend para el módulo de Notificaciones.

Debe tener endpoints para:
- Enviar notificación (email, SMS, push)
- Listar notificaciones por usuario
- Marcar como leída

No toques el frontend por ahora.
```

**Skills activadas**:
- `api-first-spec` → Define endpoints
- `agent-backend` → Meta-skill que ejecuta:
  - `database-sp` → Tabla + SPs
  - `data-access` → Handlers
  - `backend-api` → Endpoints
  - `swagger` → Documentación API
- `code-review` → Revisión antes de commit

**Skills NO activadas**: Frontend, tests UI.

**Tiempo estimado**: 10-15 minutos.

---

### Caso 4: Solo Frontend (Backend Ya Existe)

**Cuándo usar**: El backend ya está implementado y documentado en OpenAPI. Solo necesitas UI.

**Qué escribir en el chat**:

```
Necesito crear la interfaz de usuario para el módulo de Notificaciones.

El backend ya existe y está documentado en:
http://localhost:8080/swagger

Necesito:
- Componente NotificationList (lista de notificaciones)
- Componente NotificationBell (campana con contador)
- Botón para marcar como leída
```

**Skills activadas**:
- `agent-frontend` → Meta-skill que ejecuta:
  - `api-first-frontend` → Genera types desde OpenAPI
  - `react-hooks` → Crea hooks (useNotificationsQuery, useMarkAsRead...)
  - `react` → Crea componentes
  - `design-system` → Aplica estilos
- `playwright` → Tests E2E de UI

**Skills NO activadas**: DB, Backend.

**Tiempo estimado**: 10-15 minutos.

---

### Caso 5: Actualizar Módulo Existente

**Cuándo usar**: El módulo ya existe pero necesitas agregar/modificar funcionalidad.

**Qué escribir en el chat**:

```
En el módulo de Contratos, necesito:
- Agregar campo "Categoría" (Servicios, Bienes, Consultorías)
- Agregar endpoint GET /api/contratos/estadisticas
- Actualizar la UI para mostrar filtro por categoría
```

**Skills activadas** (solo lo necesario):

| Cambio solicitado | Skills activadas |
|-------------------|------------------|
| Agregar campo "Categoría" | `database-sp` → Altera tabla<br>`data-access` → Actualiza handlers<br>`backend-api` → Actualiza DTOs<br>`swagger` → Actualiza docs<br>`api-first-frontend` → Regenera types<br>`react` → Actualiza formularios |
| Nuevo endpoint `/estadisticas` | `database-sp` → Nuevo SP<br>`data-access` → Nuevo handler<br>`backend-api` → Nuevo endpoint<br>`swagger` → Documenta endpoint |
| UI filtro por categoría | `react` → Actualiza componente de filtros<br>`react-hooks` → Actualiza query params |

**Resultado**: **NO repite todo el módulo**, solo actualiza lo que cambió.

**Tiempo estimado**: 5-10 minutos por cambio.

---

### Caso 6: Bug Fix

**Cuándo usar**: Detectaste un bug y necesitas corregirlo.

**Qué escribir en el chat**:

```
Hay un bug en el módulo de Contratos:

El filtro de búsqueda no funciona con nombres que tienen acentos (ñ, á, é, etc.).

Por ejemplo, buscar "Señor" no encuentra "Señor López".
```

**Skills activadas**:
1. `code-review` → Identifica causa raíz (problema en SP con COLLATE)
2. `database-sp` → Corrige stored procedure (agrega COLLATE Latin1_General_CI_AI)
3. `playwright` → Agrega test E2E con acentos
4. `changelog` → Registra fix
5. `pull-request` → Crea PR con fix

**Skills NO activadas**: Frontend (si el bug es solo en DB), otros módulos.

**Tiempo estimado**: 5-10 minutos.

---

## Ejemplo Concreto: Módulo de Usuarios en 3 Sesiones

### Sesión 1: Crear Módulo Completo

**Chat**:
```
Quiero crear el módulo de Gestión de Usuarios.

Debe poder listar, crear, editar y eliminar usuarios.

Cada usuario tiene:
- ID
- Nombre completo
- Email (único)
- Rol (Admin, Usuario, Invitado)
- Estado (Activo, Inactivo)
- Fecha de registro
```

**Framework ejecuta**:
```
api-first-spec → Define estructura
database-sp → Tabla Users + 5 SPs
data-access → UserHandler.cs
backend-api → UserEndpoints.cs (5 endpoints)
swagger → Documenta /api/users/*
api-first-frontend → UserTypes.ts
react-hooks → useUsersQuery, useCreateUser, useUpdateUser, useDeleteUser
react → UserList, UserForm, UserCard
design-system → Estilos aplicados
agent-qa → Tests E2E
readme → Docs actualizadas
pull-request → PR listo
```

**Resultado**: Módulo completo en 1 sesión (~20 minutos).

---

### Sesión 2: Agregar Campo "Departamento"

**Chat**:
```
En el módulo de Usuarios, necesito agregar un campo "Departamento" (TI, Finanzas, Operaciones, Ventas).

Debe aparecer en:
- Tabla de la base de datos
- API (GET, POST, PUT)
- Formulario de usuario
- Lista de usuarios (como columna)
```

**Framework ejecuta** (solo lo necesario):
```
database-sp → ALTER TABLE Users ADD Departamento
database-sp → Actualiza SPs (Create, Update, List)
data-access → Actualiza UserHandler
backend-api → Actualiza CreateUserRequest, UpdateUserRequest, UserResponse
swagger → Actualiza documentación
api-first-frontend → Regenera UserTypes.ts
react → Actualiza UserForm (agrega dropdown de departamento)
react → Actualiza UserList (agrega columna departamento)
changelog → Registra cambio
pull-request → Crea PR
```

**Resultado**: Campo agregado SIN repetir todo (~10 minutos).

**No regeneró**: Tests E2E (no cambiaron), configuración base, otros módulos.

---

### Sesión 3: Bug en Búsqueda de Email

**Chat**:
```
El filtro de búsqueda de usuarios por email no funciona.

Cuando busco "juan@tivit.com" no encuentra nada, pero sé que ese usuario existe.
```

**Framework ejecuta**:
```
code-review → Identifica problema en SP_Users_Search (falta LIKE '%@%')
database-sp → Corrige búsqueda:
   WHERE Email LIKE '%' + @SearchTerm + '%'
playwright → Agrega test de búsqueda por email
changelog → Registra fix
pull-request → Crea PR con fix
```

**Resultado**: Bug resuelto sin tocar frontend (~5 minutos).

**No regeneró**: Tabla Users, handlers, componentes UI.

---

## Recomendaciones de Workflow

### HACER

| Práctica | Por qué |
|----------|---------|
| **Módulo por módulo** | Cambios pequeños, PRs revisables |
| **Spec primero** | `api-first-spec` define claramente qué construir |
| **Commit frecuente** | 1 módulo = 1 PR (no mezclar) |
| **Tests incluidos** | No dejar "para después" |
| **Documentar en el momento** | README, Swagger, Changelog actualizados |
| **Revisar antes de aprobar** | Lee las propuestas del agente |

### NO HACER

| Anti-patrón | Por qué es malo |
|-------------|----------------|
| **Saltar api-first-spec** | Sin spec, no hay claridad en qué construir |
| **Mezclar múltiples módulos en 1 PR** | PRs imposibles de revisar |
| **Omitir code-review** | Bugs y vulnerabilidades pasan desapercibidos |
| **Hardcodear valores** | Usa tokens de design-system, variables de entorno |
| **Repetir manualmente** | Usa meta-skills (`agent-fullstack`, `agent-backend`, `agent-frontend`) |
| **Crear todo de una vez** | Proyectos grandes → divide en módulos pequeños |

---

## Regla de Oro Expandida

### Matriz de Cambios → Skills Activadas

| Cambio | DB | Backend | Frontend | Tests | Docs | Tiempo |
|--------|-----|---------|----------|-------|------|--------|
| **Nueva tabla** | | | | | | ~15 min |
| **Nuevo campo** | | | | (actualiza) | | ~10 min |
| **Nuevo endpoint** | (si necesita SP) | | | (API test) | | ~5 min |
| **Nuevo componente UI** | | | | (E2E) | | ~5 min |
| **Bug en SP** | | (si afecta handler) | | (regresión) | | ~5 min |
| **Bug en UI** | | | | (E2E) | | ~5 min |
| **Refactor** | Depende | Depende | Depende | (validar) | | Variable |

**Leyenda**:
- Se ejecuta completo
- Solo actualiza lo necesario
- No se ejecuta

---

## Patterns Avanzados

### Pattern 1: Módulo con Relaciones

**Chat**:
```
Crea el módulo de Pedidos.

Cada pedido tiene:
- Relación con Cliente (tabla Clientes)
- Relación con múltiples Productos (tabla PedidoDetalle)
- Estado (Pendiente, Procesando, Completado, Cancelado)
```

**Framework entiende relaciones** y crea:
- Tabla `Pedidos` + `PedidoDetalle` (relación 1:N)
- Foreign keys a `Clientes`
- JOINs en los SPs
- DTOs anidados en backend
- Componentes relacionados en frontend

---

### Pattern 2: Módulo con Validaciones de Negocio

**Chat**:
```
En el módulo de Pedidos:

Regla de negocio:
- No se puede crear un pedido si el cliente tiene pedidos pendientes de pago
- El monto total debe ser mayor a $100
- Solo usuarios con rol "Vendedor" o "Admin" pueden crear pedidos
```

**Framework genera**:
- Validación en SP (bloquea en DB)
- Validación en handler (regresa error específico)
- Validación en frontend (deshabilita botón)
- Mensaje de error claro en UI

---

### Pattern 3: Módulo Read-Only (Solo Consultas)

**Chat**:
```
Crea el módulo de Reportes de Ventas.

Solo lectura (no permite crear/editar/eliminar).

Debe mostrar:
- Ventas por mes
- Top 10 productos
- Ventas por vendedor
```

**Framework entiende** y crea:
- SPs de solo lectura (SELECT)
- Endpoints GET únicamente
- Componentes de visualización (tablas, gráficos)
- Sin formularios de edición

---

## Flujo Recomendado para Proyectos Grandes

### Fase 1: MVP (Minimum Viable Product)

1. **Identificar módulo core** (ejemplo: Usuarios, Autenticación)
2. **Crear módulo completo** con `agent-fullstack`
3. **Verificar que funciona** end-to-end
4. **Commit + PR**

### Fase 2: Módulos Secundarios

5. **Agregar módulo 2** (ejemplo: Productos)
6. **Agregar módulo 3** (ejemplo: Pedidos)
7. **Cada uno en su propio PR**

### Fase 3: Refinamiento

8. **Agregar campos faltantes** a módulos existentes
9. **Agregar validaciones de negocio**
10. **Agregar features avanzadas** (búsqueda, filtros, paginación)

### Fase 4: Calidad

11. **Ejecutar `code-review` en todos los módulos**
12. **Completar cobertura de tests**
13. **Optimización de performance** (si es necesario)

**Resultado**: Proyecto completo, modular, revisable, mantenible.

---

## Debugging del Workflow

### ¿Cómo sé qué skills se activaron?

El agente **siempre indica** en su respuesta qué skills está ejecutando.

Ejemplo de respuesta:
```
Voy a crear el módulo de Tareas. Ejecutaré:

1. api-first-spec → Definir estructura
2. database-sp → Crear tabla Tasks
3. backend-api → Crear endpoints
...
```

### ¿Qué hago si se activó una skill equivocada?

Dile al agente:
```
"No necesito el frontend aún, solo crea el backend"
```

El framework **ajusta** qué skills ejecutar.

### ¿Puedo ver el log de skills ejecutadas?

Sí, el agente mantiene un log. Pregunta:
```
"¿Qué skills ejecutaste en el último cambio?"
```

---

## Siguientes Pasos

- **FAQ**: [FAQ.md](FAQ.md) — Preguntas frecuentes
- **Catálogo**: [SKILLS-MANIFEST.md](../framework/SKILLS-MANIFEST.md) — 58 skills disponibles
- **Ejemplos**: [examples/](../examples/) — Casos ejecutables
- **Troubleshooting**: [TROUBLESHOOTING.md](TROUBLESHOOTING.md) — Solución de problemas

---

## Soporte

- **Problemas técnicos**: [Manuel Aliaga](mailto:manuel.aliaga@tivit.com)
- **Revisión de diseño**: [Miguel Martinez](mailto:miguel.martinez@tivit.com)

---

**Tiempo ahorrado con estos workflows**: 80-95% vs desarrollo manual tradicional.
