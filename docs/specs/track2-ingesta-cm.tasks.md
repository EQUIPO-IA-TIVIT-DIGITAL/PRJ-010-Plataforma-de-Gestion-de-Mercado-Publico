# Tasks: Track 2 — Ingesta Datos Abiertos CM (Convenios Marco)

**Spec:** `docs/api-first/ingesta-datos-abiertos.md`
**Origen:** MPM-S2-001/003 (`docs/api-first/sprint2-plan.md`, pivotado a planillas CM)
**Sprint:** Track 2
**Total estimado:** ~37h (≈ 5 días, 1 dev)

> Regla de oro del spec: **nada se persiste sin pasar por el filtro de RUTs configurados**.
> Migraciones con placeholder **V15X** (última aplicada: V154); numerar definitivo al crear archivo.
> HITL-01…06 del spec deben estar resueltos antes de T11 (conciliación).

---

## Infraestructura y Base de Datos

### T1 — Migración V15X: tablas CM + índices + seed TIVIT
**Depende de:** none
**Estimación:** 2.5h
**Work:**
- Crear `src/MPM.Api/Database/Scripts/V15X__Ingesta_Cm_Tablas.sql` con:
  - `cm_ruts_configurados` (UK `rut`, seed ON CONFLICT: `76.130.712-6` / TIVIT / 'Propia')
  - `cm_ingesta_log` (UK `(anio, mes)`, IX `estado`, columnas según spec §2)
  - `cm_ordenes_compra` (FK a ingesta_log, IX `(rut_proveedor, anio)`, IX `(rut_comprador, anio)`, IX `(anio, mes)`, IX `codigo_oc`)
  - `cm_paridad_moneda` (UK `(moneda, anio, mes)`), `cm_oc_erroneas` (PK `codigo_oc`), `cm_complementos_estado`
- Sin vistas ni funciones aún (eso es T10)
- Comentario de cabecera con referencia al spec y fecha de fuente verificada (2026-08-20)
**Verify:**
```bash
docker-compose up -d cloudsql-proxy  # o BD local del compose dev
psql -f src/MPM.Api/Database/Scripts/V15X__Ingesta_Cm_Tablas.sql   # aplica limpio 2 veces (idempotente)
```
- [ ] `\d cm_ordenes_compra` muestra los 4 índices + FK
- [ ] `SELECT * FROM cm_ruts_configurados;` retorna TIVIT
- [ ] Re-ejecutar el script no duplica seed ni falla por objetos existentes

---

### T2 — Fixtures de prueba: recortes reales de planilla CM + complementos
**Depende de:** none
**Estimación:** 2h
**Work:**
- Descargar UNA planilla real (ej. `2026-6.zip`) y recortar a fixture pequeña:
  - `tests/fixtures/cm/2026-6.csv` (~50 filas): incluir ≥3 filas de TIVIT (buscar RutProveedor 761307126), ≥2 filas de otros proveedores, ≥1 fila con moneda ≠ CLP, ≥1 campo con salto de línea embebido y caracteres Windows-1252 (ñ, á)
  - `tests/fixtures/cm/2026-6.zip` — recomprimir el CSV en zip
  - `tests/fixtures/cm/hist_OC_erroneas_recorte.csv` (con 1 CodigoOC presente en la fixture principal)
  - `tests/fixtures/cm/ParidadMoneda_recorte.csv` (CLP implícito + USD; omitir una moneda presente para probar sin-paridad)
- Documentar en `tests/fixtures/cm/README.md`: origen, fecha de descarga, qué representa cada recorte, y **confirmar/corregir los encabezados reales contra la tabla de mapeo del spec §8** (si difieren, actualizar spec antes de seguir)
**Verify:**
- [ ] Los 4 fixtures existen y abren correctamente con encoding correcto (verificar ñ/á visibles)
- [ ] README documenta encabezados reales confirmados vs tabla de mapeo del spec
- [ ] Ninguna fixture contiene datos sensibles más allá de lo público (fuente es pública)

---

## Backend — Job de Ingesta

### T3 — Parser CSV robusto (Windows-1252, ';', saltos embebidos) + tests unitarios
**Depende de:** T2
**Estimación:** 3h
**Work:**
- Nuevo proyecto/carpeta de servicios del módulo (ubicación a decidir con delivery: `MPM.Modules.Licitaciones/Services/DatosAbiertos/` o módulo propio `MPM.Modules.DatosAbiertos`)
- `PlanillaCmParser.cs` sobre CsvHelper:
  - Delimitador `';'`, encoding Windows-1252 vía `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`
  - Lectura en streaming (`IEnumerable<PlanillaCmRow>`), nunca materializar el CSV completo
  - Validación de cabecera esperada al inicio: mismatch → `CabeceraInvalidaException` con detalle de columnas faltantes (riesgo R2 del spec)
  - Contadores: filas totales leídas, filas ilegibles
- Tests unitarios (`PlanillaCmParserTests.cs`) contra la fixture T2:
  - parsea 50 filas, contadores correctos
  - fila con salto de línea embebido se parsea como UNA fila
  - caracteres 1252 no corrompidos
  - cabecera alterada → lanza excepción con mensaje claro
**Verify:**
```bash
dotnet test tests/<modulo>.Tests --filter "FullyQualifiedName~PlanillaCmParserTests"
# 4+ tests pass
```

---

### T4 — Downloader de planillas (zip, 403→SinDatos, checksum SHA256) + tests
**Depende de:** none
**Estimación:** 2.5h
**Work:**
- `PlanillaCmDownloader.cs` (HttpClient inyectado, patrón `ApiMpService`):
  - `DescargarAsync(anio, mes, ct)` → stream a archivo temporal + SHA-256 calculado en vuelo + bytes
  - HTTP 403 → throw `SinDatosException` (semántica SIN_DATOS, no error)
  - 429/5xx/transitorio → throw HttpRequestException (deja que la política de retry del HttpClient actúe, hasta `CM_MAX_INTENTOS_PERIODO`)
  - URL: `{CM_BASE_URL}/{anio}-{mes}.zip` — mes SIN cero inicial
- Tests con `HttpMessageHandler` mock:
  - 200 con bytes → checksum correcto (SHA-256 conocido del fixture zip)
  - 403 → `SinDatosException`
  - 500 → HttpRequestException (reintentable)
  - URL generada: `2026-6.zip` (sin cero), `2016-1.zip`
**Verify:**
```bash
dotnet test tests/<modulo>.Tests --filter "FullyQualifiedName~PlanillaCmDownloaderTests"
# 4 tests pass
```

---

### T5 — Limpieza y conversión: filtro RUT, exclusión OC erróneas, conversión CLP + tests
**Depende de:** T1, T2
**Estimación:** 3h
**Work:**
- `NormalizadorRut.cs`: normaliza a `XXXXXXXX-X` (quita puntos, DV mayúscula) + validación módulo 11 (compartible con endpoints admin de T9)
- `LimpiadorFilasCm.cs`:
  - Filtro: `rut_proveedor ∈ ruts activos` (recibe set normalizado desde `cm_ruts_configurados`)
  - Exclusión: `codigo_oc ∉ cm_oc_erroneas` (set precargado)
  - Conversión: CLP→factor 1; lookup `(moneda, anio, mes)` en `cm_paridad_moneda`; sin paridad → `monto_neto_clp/monto_bruto_clp = NULL` + contador
  - Salida: filas listas para insert + contadores del log (`filas_filtradas_rut`, `filas_excluidas_oc_erroneas`, `filas_moneda_sin_paridad`)
- Tests unitarios (fixtures T2):
  - solo pasan filas TIVIT; filas ajenas descartadas
  - la OC errónea del fixture se excluye y cuenta
  - USD convertido con factor del fixture; moneda sin paridad queda NULL y contada
  - RUT con puntos/guion/K minúscula normaliza igual que canónico
**Verify:**
```bash
dotnet test tests/<modulo>.Tests --filter "FullyQualifiedName~LimpiadorFilasCmTests|NormalizadorRutTests"
# 6+ tests pass
```

---

### T6 — Orquestador `IngestaCmService` (por período, transaccional, checkpoint) + test integración
**Depende de:** T3, T4, T5
**Estimación:** 4h
**Work:**
- `IngestaCmService.EjecutarAsync(mode, ct)`:
  - Refresh complementos al inicio (paridad upsert, OC erróneas full-replace transaccional) + update `cm_complementos_estado`
  - Selección de períodos según modo (spec §4): `mensual` = actual+anterior+pendientes/fallidos/forzados; `backfill` = 2016-1→actual saltando COMPLETADO salvo forzado
  - Por período: guard anti-concurrencia (`UPDATE … SET estado='EN_CURSO' WHERE id=@id AND estado<>'EN_CURSO'`, 0 filas → skip), pipeline completo, transacción DELETE+INSERT por lotes de 500, log COMPLETADO con métricas + `version_ingesta++`
  - Un período FALLIDO no detiene la corrida; exit-code-style resultado final (lista de períodos con estado)
  - Reset de `EN_CURSO` huérfanos (`iniciado_at` > 2h) a PENDIENTE
  - Logs estructurados `evento=ingesta_cm.*` (spec §7)
- Test de integración (Postgres testcontainers o BD de test del repo, según convención existente):
  - 3 períodos simulados (2 zips válidos del fixture + 1 corrupto) → 2 COMPLETADO + 1 FALLIDO, sin datos parciales del corrupto
  - Re-ejecutar → idempotente (CA-03): mismas filas, version=2, cero duplicados
  - Período EN_CURSO huérfano se resetea
**Verify:**
```bash
dotnet test tests/<modulo>.Tests --filter "FullyQualifiedName~IngestaCmServiceIntegrationTests"
# 3+ tests pass; verificar en BD de test: SELECT anio, mes, estado, version_ingesta FROM cm_ingesta_log;
```

---

### T7 — Wiring Cloud Run Job: `WORKER_MODE=ingesta-cm` + Dockerfile 7-Zip + env vars
**Depende de:** T6
**Estimación:** 2h
**Work:**
- `Program.cs`: case `"ingesta-cm"` en `EjecutarWorkerAsync` → resolver `IngestaCmService` y ejecutar; respetar `CM_ENABLED=false` abortando limpio (patrón SCRAPER_ENABLED); registrar `CodePagesEncodingProvider` también aquí
- `src/MPM.Api/Dockerfile`: instalar `p7zip-full` (imagen lista para .7z futuro, Track 3)
- `docker-compose.prod.yml`: bloque de env vars CM_* (spec §7) con defaults seguros (`CM_ENABLED=false` hasta backfill)
- Doc breve de despliegue (comentario en compose o sección en el PR): comando `gcloud run jobs execute ingesta-cm-job` con overrides `CM_JOB_MODE=backfill|mensual`, recursos recomendados (spec §7)
**Verify:**
```bash
WORKER_MODE=ingesta-cm CM_JOB_MODE=mensual dotnet run --project src/MPM.Api   # corre un ciclo y termina exit 0
docker build -f src/MPM.Api/Dockerfile -t mpm-api:test . && docker run --rm mpm-api:test which 7z   # binario presente
```
- [ ] Exit code 0 con CM_ENABLED=false (aborta limpio)
- [ ] Exit code 1 si algún período FALLIDO (simular con fixture corrupto apuntando CM_BASE_URL a file/local si aplica)

---

## Backend — API Admin

### T8 — Endpoints admin: GET log, POST reprocesar, GET resumen + tests
**Depende de:** T1
**Estimación:** 3h
**Work:**
- Controller `IngestaCmAdminController` (`/api/v1/admin/ingesta-cm`, `[Authorize(Roles="Admin")]`):
  - `GET log` → `usp_IngestaCm_LogListar` (paginado, filtros anio/estado/sort seguro con lista blanca)
  - `POST {anio}/{mes}/reprocesar` → `usp_IngestaCm_MarcarReproceso`; validar rango (ING_003), EN_CURSO (ING_002), inexistente (ING_001)
  - `GET resumen?rut=` → `usp_CM_ResumenAnual` (validar RUT con NormalizadorRut → VAL_001)
- Funciones/SP correspondientes incluidos en migración V15X (o V15Y si V15X ya se aplicó — coordinar con delivery)
- Contrato estricto: `{success:true,data,meta}` / `{success:false,error:{code,message,details},meta}`
- Tests de integración API: happy paths + 404/409/422/400/403 (no-admin)
**Verify:**
```bash
dotnet test tests/<modulo>.Tests --filter "FullyQualifiedName~IngestaCmAdmin"
curl "https://localhost:5001/api/v1/admin/ingesta-cm/log?page=1&pageSize=5"          # success:true
curl -X POST "https://localhost:5001/api/v1/admin/ingesta-cm/2019/13/reprocesar"     # 422 ING_003
curl "https://localhost:5001/api/v1/admin/ingesta-cm/log" -H "Authorization: Bearer <token-no-admin>"  # 403 AUTH_002
```

---

### T9 — CRUD RUTs configurados + tests
**Depende de:** T1
**Estimación:** 2.5h
**Work:**
- Mismo controller: `GET/POST /ruts`, `PUT /ruts/{id}`, `DELETE /ruts/{id}`
  - POST: validar módulo 11 (ING_004), duplicado (ING_005, captura de unique violation)
  - PUT: rechazar cambio de `rut` (ING_004); permitir razonSocial/notas/activo
  - DELETE: soft-disable; si tiene filas en `cm_ordenes_compra` → 200 con `eliminado:false` + mensaje (spec ING-R033/R034)
- SPs `usp_IngestaCm_RutCrear/_Actualizar/_Eliminar`
- Tests: crear válido, crear inválido (DV mal), duplicado, PUT cambiando rut → 422, DELETE con datos → mensaje advertencia
**Verify:**
```bash
dotnet test tests/<modulo>.Tests --filter "FullyQualifiedName~IngestaCmRuts"
curl -X POST ".../admin/ingesta-cm/ruts" -d '{"rut":"76.130.712-60","razonSocial":"X"}'   # 422 ING_004 (DV inválido)
curl -X POST ".../admin/ingesta-cm/ruts" -d '{"rut":"76.130.712-6"}'                      # 409 ING_005
```

---

## Integración Estadísticas

### T10 — Vista `vw_cm_resumen_anual` + `usp_CM_ResumenAnual` + wiring dashboard
**Depende de:** T1 (función testeable con seed manual; wiring final depende de T11 para datos reales)
**Estimación:** 3h
**Work:**
- En migración (V15X/V15Y): vista agregada por `(rut_proveedor, anio)` con ambos montos CLP + `lineas_sin_conversion` (SQL del spec §10) y función `usp_CM_ResumenAnual(p_rut, p_anio_desde, p_anio_hasta)`
- Localizar el servicio que hoy calcula montos ganados del dashboard ejecutivo (**leer código vigente**, familia AnalisisService/handlers de estadísticas — NO asumir) y sumar el componente CM según HITL-01, con desglose visible por fuente (licitaciones vs convenios marco)
- Tooltip/nota de limitación "OC = compromiso de compra, no pago efectivo" en el dato expuesto (contrato para frontend)
- Test SP: insertar 3 filas seed manuales en BD de test → función retorna agregados correctos (incluye caso NULL conversion excluido de suma pero contado)
**Verify:**
```sql
INSERT INTO cm_ordenes_compra (...) VALUES (...seed de prueba...);
SELECT * FROM usp_CM_ResumenAnual('761307126', 2025, 2026);   -- agrega bien, acepta RUT con y sin formato
```
- [ ] Dashboard ejecutivo refleja total = licitaciones + CM (verificar con datos seed antes del backfill real)

---

## Operación y Despliegue

### T11 — Backfill inicial 2016→hoy + conciliación contra ficha oficial (CA-01)
**Depende de:** T7, T10, HITL-01…03 resueltos
**Estimación:** 3h (+ tiempo de cómputo del job, no cuenta como horas dev)
**Work:**
- Ejecutar Cloud Run Job one-shot: `CM_JOB_MODE=backfill`, recursos 2GiB/6h timeout
- Monitorear `cm_ingesta_log`: períodos FALLIDO/SIN_DATOS esperados (2016 temprano puede no tener planilla)
- Conciliación documentada (checklist en PR):
  - Elegir 3 años (ej. 2024, 2025, 2026 YTD) y comparar `usp_CM_ResumenAnual` vs ficha oficial de datos abiertos ChileCompra para RUT TIVIT
  - Diferencia dentro de tolerancia HITL-03 (propuesta ±1%); si excede → investigar (paridad, corte temporal, OC erróneas) ANTES de dar por bueno el dashboard
- Ajustar `CM_ENABLED=true` en prod solo tras conciliación aprobada
**Verify:**
- [ ] `SELECT estado, COUNT(*) FROM cm_ingesta_log GROUP BY estado;` — sin EN_CURSO, FALLIDO explicados uno a uno
- [ ] Tabla de conciliación año-por-año pegada en el PR con % diferencia vs ficha oficial
- [ ] CA-01 firmado por quien decide HITL-03

---

### T12 — Scheduler mensual + alertas de fallo
**Depende de:** T7, T11
**Estimación:** 1.5h
**Work:**
- Cloud Scheduler: cron día 5 06:00 `America/Santiago` (según HITL-05) → `gcloud run jobs execute ingesta-cm-job` con `CM_JOB_MODE=mensual`
- Failure notification policy del job (canal de alertas existente del proyecto) + verificar que el log estructurado `evento=ingesta_cm.job_fallido` sea visible en el logging de GCP
- Simular fallo (período forzado con URL inválida en staging) → alerta recibida
**Verify:**
- [ ] `gcloud scheduler jobs describe <job>` muestra cron correcto
- [ ] Ejecución manual de prueba en staging: COMPLETADO y log estructurado visible en Cloud Logging
- [ ] Alerta de fallo llega al canal configurado

---

## Frontend — Admin UI

### T13 — Página admin: estado de ingesta + CRUD RUTs
**Depende de:** T8, T9
**Estimación:** 4h
**Work:**
- Nueva ruta admin (seguir patrón de páginas admin existentes en `src/mpm-web`):
  - Tabla `cm_ingesta_log` (AntD): columnas período, estado (tag con color por estado), filas filtradas/insertadas, versión, duración, error (tooltip); filtros año/estado; paginación server-side
  - Acción por fila "Reprocesar" (Modal confirm → POST reprocesar → refrescar; deshabilitada si EN_CURSO)
  - Sección/tab RUTs: tabla + modal crear/editar (input RUT con máscara y validación DV client-side + server), toggle activo, eliminar con manejo del mensaje de advertencia
  - Estados: loading skeleton, error toast con reintentar, empty state ("Aún no hay ingestas — ejecutar backfill")
- Hook `useIngestaCm.ts` + tipos TS según contrato del spec (success/error wrapper)
**Verify:**
- [ ] Tabla carga períodos reales post-backfill (T11) con paginación funcional
- [ ] Reprocesar un período → flag visible en tabla; próxima corrida lo procesa (version++)
- [ ] Crear RUT inválido → error inline ING_004; duplicado → ING_005
- [ ] Eliminar RUT con datos → mensaje de advertencia, RUT queda inactivo
- [ ] Responsive básico (tabla con scroll horizontal en móvil)

---

## Documentación

### T14 — Índices, catálogo API y CHANGELOG
**Depende de:** T8, T9, T10, T13
**Estimación:** 1.5h
**Work:**
- `docs/api-first/README.md`: entrada del módulo `ingesta-datos-abiertos`
- `docs/API_CATALOG.md`: 6 endpoints admin (skill api-catalog)
- Marcar `docs/api-first/sprint2-plan.md` como superseded (nota de cabecera apuntando al nuevo spec, pivot mserv → planillas CM)
- `CHANGELOG.md`: entrada Track 2 (skill pull-request)
- Cierre de criterios: checklist CA-01…CA-09 del spec §13 con evidencia enlazada
**Verify:**
- [ ] `grep -n "ingesta-cm" docs/api-first/README.md docs/API_CATALOG.md` encuentra entradas
- [ ] CHANGELOG con entrada versionada
- [ ] Checklist CA del spec completa (o ítems pendientes justificados)

---

## Resumen de dependencias

```
T1 ──┬── T5 ──┐
T2 ──┼── T3 ──┼── T6 ── T7 ──┬── T11 ── T12
     │        │              ├── T10 (wiring final usa datos de T11)
T1 ──┼── T8 ──┼──────────────┤
     └── T9 ──┴── T13        │
T8+T9+T10+T13 ── T14
```

Paralelizables desde el inicio: T2 ∥ T4 ∥ T1. Un solo dev: orden secuencial T1→T2→T3→T4→T5→T6→T7→T8→T9→T10→T11→T12→T13→T14.
