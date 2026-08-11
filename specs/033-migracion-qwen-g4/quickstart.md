# Quickstart: Validación de la migración Gemini → Qwen

**Spec**: `specs/033-migracion-qwen-g4/spec.md` | **Date**: 2026-08-11

Guía de validación end-to-end. Los detalles de implementación viven en `tasks.md`; acá solo cómo **probar** que la feature funciona.

## Prerrequisitos

- Stack local levantado (API :5001, Web :8181, DB :5433 — Docker Compose).
- Proveedor por defecto: `AI:Provider=gemini` (o tabla `system_ai_provider` vacía → fallback env).
- Para el camino Qwen: servidor OpenAI-compatible con Qwen 3.7 G4 (dev: `docker run` con vLLM/Ollama; **en producción: URL entregada por el equipo**; contrato en `contracts/openai-compat-api.md`).
- Usuario super admin: `admin@tivit.cl` (rol SuperAdmin, seed V042).
- Documentos reales de licitaciones **fuera del repo** (GCS o ruta local documentada; nunca commitear).

## Escenario 1 — Regresión del camino Gemini (US1)

```bash
dotnet build src/MPM.Api -c Debug
docker compose up -d   # si no está levantado
```

1. Ejecutar la suite de tests: `dotnet test` (unit por módulo + integración en tests/MPM.Tests).
2. E2E (opcional): `cd src/mpm-web && npm run test:e2e`.
3. En la UI, analizar una licitación con PDFs reales → el análisis, puntuaciones y chat funcionan **idéntico a antes**.
4. Verificar en BD: `SELECT modelo_usado FROM analisis ORDER BY id DESC LIMIT 1;` → `gemini-2.5-pro`.

**Resultado esperado**: cero cambios de comportamiento, suite verde, mismo modelo persistido.

## Escenario 2 — Resolución dinámica del proveedor por configuración (US1)

1. Sin tocar la tabla (vacía), setear en entorno:
   ```powershell
   $env:AI__Provider = "openai"
   $env:AI__Endpoint = "http://localhost:8000/v1"   # servidor Qwen dev
   $env:AI__Model = "qwen3.7-g4"
   ```
2. Levantar la API y consultar `GET /api/system/ai-provider` → `provider: openai`, `resolvedFrom: "environment"`.
3. Probar una consulta de chat de análisis: responde con Qwen.
4. Restaurar `AI__Provider = "gemini"`, reiniciar → todo vuelve a Gemini.

**Resultado esperado**: el cambio es solo configuración; `modelo_usado` refleja el modelo activo.

## Escenario 3 — Benchmark de calidad (US2)

```bash
dotnet run --project tools/BenchmarkLlm -- \
  --provider-a gemini --provider-b openai \
  --model-b qwen3.7-g4 --endpoint http://localhost:8000/v1 \
  --docs <ruta-o-gcs-con-documentos-reales> --salida benchmark-qwen-g4.md
```

1. El harness corre el mismo prompt de análisis contra ambos proveedores sobre ≥ 10 documentos.
2. Informe generado con: paridad campo a campo (fechas, montos, criterios, puntuaciones), tasa de JSON válido, tasa de truncamiento, latencia p50/p95, recomendación go/no-go contra **umbral ≥ 90% + revisión manual** (montos/criterios primero).

**Resultado esperado**: informe reproducible guardado como evidencia; decisión go/no-go explícita.

## Escenario 4 — Switch del super admin en la UI (US4)

1. Login como `admin@tivit.cl` → navegar a la página de administración (`/admin/ia`): se ve el estado actual (`gcloud` / `gemini-2.5-pro`, `resolvedFrom`).
2. Cambiar el switch a **qwen** (con `AI:Endpoint` y `AI:Model` de Qwen) → confirmar → `GET /api/system/ai-provider` refleja `openai`, `resolvedFrom: "database"`, `lastChange.updatedBy = admin@tivit.cl`.
3. **Sin reiniciar nada**, iniciar un análisis → `modelo_usado = qwen3.7-g4`.
4. Reiniciar la API → el proveedor sigue en `openai` (persistido).
5. Volver el switch a **gcloud** → análisis siguiente con `gemini-2.5-pro` de nuevo.
6. Login como usuario sin rol SuperAdmin → la página no existe en el menú y `GET /api/system/ai-provider` responde 403.

**Resultado esperado**: alternancia gcloud/qwen sin reinicio, auditada y solo para SuperAdmin.

## Escenario 5 — Análisis completo con Qwen (US3)

1. Con el switch en qwen (o `AI:Provider=openai` en dev), ejecutar el análisis de una licitación **nueva** con PDFs reales (incluir uno escaneado si hay disponible).
2. Verificar en BD: `modelo_usado` = `qwen3.7-g4` y JSON persistido con todos los campos del contrato.
3. Abrir la licitación en la UI → misma estructura de pantalla que con Gemini.
4. Matar el servidor Qwen e intentar un análisis → error tipado, estado `error` reintentable, sin datos corruptos.
5. Reiniciar el servidor y reintentar → el análisis se completa.

**Resultado esperado**: flujo completo operativo con Qwen, contrato de errores intacto, frontend sin cambios.

## Escenario 6 — Cutover y rollback en ambiente que replica producción (US5)

1. En staging: cambiar el switch a qwen (URL de staging del equipo); validar análisis reales por 1 día.
2. Ejecutar el runbook de cutover (`docs/infraestructura-cu010-v6.md`): confirmar switch en UI + verificación post-cambio → operación con Qwen sin uso de Google.
3. **Drill de rollback**: volver el switch a gcloud → operación con Gemini en < 30 min, sin pérdida de análisis pendientes. Simular además el fallback por entorno (si la UI no está disponible: `AI:Provider=gemini` + reinicio).

**Resultado esperado**: ambos procedimientos documentados, probados y con tiempos medidos.

## Criterios de aceptación transversales

- [ ] Suite completa verde con proveedor default (gemini o tabla vacía).
- [ ] `modelo_usado` persiste el modelo real en cada análisis.
- [ ] Switch solo SuperAdmin (403 para otros), efecto < 1 min sin reinicio, auditado.
- [ ] Informe de benchmark guardado como evidencia (go/no-go contra umbral 90%).
- [ ] Rollback probado en < 30 minutos.
- [ ] Frontend sin cambios salvo la nueva página de administración.
