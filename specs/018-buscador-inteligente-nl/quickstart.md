# Quickstart: Buscador Inteligente en Lenguaje Natural

**Feature**: `018-buscador-inteligente-nl`

## Prerrequisitos

- Stack local corriendo (`docker compose up --build`, o `dotnet run --project src/MPM.Api` + `npm run dev` en `src/mpm-web`).
- `GOOGLE_CLOUD_PROJECT` configurado y ADC autenticado (mismo requisito que `SinonimosIaService` en Alertas — sin esto, el escenario 4 de degradación es el único que se puede validar).

## Escenario 1 — US1: consulta con sinónimos (SC-002)

1. Ir a `/licitaciones`, usar la nueva barra de búsqueda semántica (reemplaza o convive con `LicitacionFilterBar`).
2. Escribir `"ciberseguridad para el sector salud"`.
3. **Esperado**: la lista incluye licitaciones cuyo nombre/descripción mencionan "SOC", "seguridad de la información" o "protección de datos", sin requerir el término literal "ciberseguridad" — ordenadas por relevancia.
4. Verificación directa contra la API: `GET /api/v1/licitaciones/buscar-natural?q=ciberseguridad%20para%20el%20sector%20salud` → confirmar que `items` incluye resultados sin la palabra literal en `nombre`/`descripcion`.

## Escenario 2 — US1: filtro implícito de monto/fecha

1. Buscar `"licitaciones de cloud computing mayores a 10 millones"`.
2. **Esperado**: los resultados respetan `monto_estimado >= 10000000` sin que el usuario haya tocado ningún filtro visible de monto.

## Escenario 3 — US2: filtro por estado (P1)

1. Buscar `"telecomunicaciones"` con el selector de estado en "Adjudicadas".
2. **Esperado**: ningún resultado con estado activo aparece — el filtro explícito de UI tiene prioridad sobre cualquier inferencia de la IA.

## Escenario 4 — FR-005: degradación controlada

1. Simular indisponibilidad de Vertex AI (quitar `GOOGLE_CLOUD_PROJECT` del entorno o cortar red hacia `aiplatform.googleapis.com`).
2. Repetir el Escenario 1.
3. **Esperado**: la búsqueda sigue funcionando — cae a comportamiento literal (`plainto_tsquery`/`websearch_to_tsquery` sin expansión), sin error 500 ni timeout visible para el usuario. Confirma que no hay dependencia dura de Gemini en el camino crítico.

## Escenario 5 — US3: resumen sin descarga (SC-004)

1. Ejecutar cualquier búsqueda con resultados.
2. Abrir Network tab / `read_network_requests` mientras se renderizan las tarjetas de resultado.
3. **Esperado**: cero requests de descarga de PDF/adjuntos disparados solo por mostrar la lista — la descarga ocurre únicamente al hacer clic en "ver detalle" de una licitación puntual.

## Medición de SC-001 (latencia)

Ejecutar 20 búsquedas variadas (mezcla de consultas cortas y largas, con y sin filtros implícitos) y confirmar que el percentil 95 de tiempo de respuesta del endpoint es menor a 3 segundos, incluyendo la llamada a Gemini. Si no se cumple con el modelo Flash actual, revisar `research.md` — la alternativa de embeddings queda documentada como plan B.
