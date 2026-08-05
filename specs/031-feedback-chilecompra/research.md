# Research: Feedback ChileCompra (031)

Decisiones para resolver los puntos técnicos abiertos de `spec.md`, basadas en la auditoría real del código existente (ver hallazgos por módulo debajo de cada decisión).

## 1. Clasificación de licitaciones por área de negocio (US1)

**Decisión**: clasificación **en tiempo de consulta**, vía el `search_vector` (tsvector, GIN-indexed) que ya existe desde `V066`, contra un catálogo nuevo `areas_negocio(codigo, nombre, palabras_clave TEXT[])` con 3 filas semilla (Cloud, Ciberseguridad, Digital). Una licitación pertenece a un área si `EXISTS (SELECT 1 FROM unnest(palabras_clave) kw WHERE search_vector @@ plainto_tsquery('spanish', kw))`.

**Por qué**: evita crear una tabla de clasificación pre-computada o un job en background — el `search_vector` ya está mantenido por trigger en cada insert/update de `licitaciones` (mismo mecanismo que usa `usp_Licitaciones_BuscarNatural`), así que el filtro por área es solo una cláusula `EXISTS` más sobre infraestructura que ya existe y ya está probada en producción con 180k+ filas.

**Alternativas consideradas**:
- *Tabla de clasificación pre-computada (`licitaciones_areas_negocio`) llenada por un job*: se descartó por ahora — agrega una tabla, un job de reclasificación y un problema de sincronización (¿qué pasa si se edita `palabras_clave` después?) que la consulta en vivo no tiene. Se puede migrar a esto después si el volumen de "sin clasificar" hace evidente que el filtro léxico es insuficiente y hace falta IA (como en el buscador de spec 018).
- *Clasificación vía IA (Gemini), igual que `SinonimosIaService`*: se descartó para v1 por costo — clasificar 180k+ licitaciones contra IA no es "post-petición" acotado como en US5, es el mismo problema de volumen que motivó el filtro por área en primer lugar (ver transcripción: "vamos a hacer más de 183.000 llamadas"). Queda como mejora futura si el recall léxico resulta insuficiente.

## 2. Estadísticas por estado con drill-down (US2)

**Decisión**: nuevo SP `usp_Licitaciones_ContarPorEstado(p_area, p_sin_clasificar)`, `LEFT JOIN` de `estados_licitacion` a `licitaciones` (para incluir estados con conteo 0) reutilizando la misma cláusula `EXISTS` de área que US1. El "drill-down" no es una vista nueva — es el listado existente `usp_Licitaciones_Listar` con `p_estado` + `p_area` ya seteados, navegado por query string.

**Por qué**: `MPM.Modules.Catalogo` hoy es puramente descriptivo (`usp_Catalogos_EstadosLicitacion()` solo devuelve la definición del catálogo, no cuenta nada) — no hay nada que reutilizar ahí. El conteo pertenece a `MPM.Modules.Licitaciones` porque opera sobre la tabla `licitaciones`, no sobre el catálogo.

## 3. Orden del historial de análisis por fecha de adjudicación (US3)

**Decisión**: reescribir `usp_AnalisisWorkspaces_Listar` (actualmente ordena por `aw.created_at DESC`, ver `V113`) para exponer `l.fecha_adjudicacion` y ordenar por `COALESCE(l.fecha_adjudicacion, l.fecha_estimada_adjudicacion) DESC NULLS LAST, aw.created_at DESC`.

**Por qué**: el JOIN a `licitaciones` ya existe en el SP (hoy solo se usa para traer `nombre`), así que agregar `fecha_adjudicacion` a la proyección es un cambio acotado. El `NULLS LAST` cubre el edge case del spec (análisis de licitaciones sin fecha de adjudicación registrada quedan al final, no rompen el orden del resto).

**Alternativas consideradas**: agregar un parámetro `p_sort_by` para que el usuario elija el orden — se descartó porque el spec no pide un selector de orden, pide corregir el default confuso. Si se pide ordenamiento configurable después, es un cambio aditivo sobre esta misma base.

## 4. Actividad total de mercado de un competidor (US4)

Este es el punto más costoso de los cinco y el único donde el dato simplemente no existe hoy en el sistema.

**Hallazgo clave**: `licitaciones_ofertas` (V097) solo tiene filas de licitaciones donde **TIVIT participó** — el scraper (`tools/scraper-mp-v2/modulos/buscar.js`) navega la vista autenticada "licitaciones en las que he ofertado" de Mercado Público, no una búsqueda pública general. Dentro de esas licitaciones sí se capturan todos los oferentes (vía `cuadroOfertas.js`), pero nunca se visitan licitaciones donde TIVIT no ofertó — por eso hoy "informe de competidor" = subconjunto de la propia participación de TIVIT, nunca la actividad total del competidor.

**Decisión**: extender el scraper con un modo nuevo — búsqueda **pública** (sin el filtro "en las que he ofertado") acotada por: (a) el área de negocio del reporte (reutilizando la clasificación de US1, vía texto/keywords, para no procesar el universo completo de 183k+), y (b) el rango de fechas que el usuario ya pide en el informe ejecutivo. Sobre ese conjunto acotado, se reutiliza `cuadroOfertas.js` sin cambios (ya extrae todos los oferentes de una licitación, no solo a TIVIT). El resultado se cachea en una tabla nueva `competidores_actividad_mercado` (mismo patrón de cache que `competidores_analisis`, V098: clave = competidor + área + período), para no re-scrapear en cada vista del informe.

**Por qué acotar por área**: sin ese límite, generar el informe de un competidor implicaría scrapear un volumen comparable al de "todas las licitaciones desde 2025" — exactamente el problema de costo que Manuel describió en la reunión para justificar el análisis "post-petición". Acotar por área de negocio + rango de fechas mantiene el volumen adicional en el mismo orden de magnitud que lo que TIVIT ya scrapea hoy para sí mismo.

**Decisión — síncrono vs. asíncrono**: el disparo del scraping ocurre **en background** (igual que hoy `scraper-job` corre en Cloud Run Jobs, no en el request HTTP) — el endpoint de "actividad de mercado" devuelve el resultado cacheado si existe, o encola la generación y responde con un estado "generando" para que el frontend haga polling, igual al patrón ya usado para el análisis de documentos (`AnalisisBackgroundService`). Un scrape real de docenas de licitaciones no cabe en el timeout de un request HTTP síncrono (el ciclo completo del scraper ya toma ~98s para un volumen mucho menor).

**Riesgo documentado, no resuelto en este research**: el volumen real de "licitaciones del área X en el rango de fechas Y donde participó el competidor Z" no se conoce hasta implementarlo contra datos reales — si incluso acotado resulta demasiado grande, la mitigación de reserva es acotar también por región u organismo, o exigir que el usuario dispare el cálculo explícitamente por competidor (nunca automático para todos los competidores a la vez). Verificar con una corrida real antes de dar por cerrado el diseño de este punto.

---

### Actualización 2026-08-04 — hallazgo real vía investigación en vivo (Claude in Chrome, credenciales de `.env` autorizadas por el usuario)

El diseño de arriba asumía reutilizar `buscar.js` (navegación WebForms + postback) para la búsqueda pública. **Se encontró algo mejor en producción real**, investigando directamente `https://www.mercadopublico.cl/Home/BusquedaLicitacion`:

- La página de búsqueda pública embebe un iframe same-origin (`/BuscarLicitacion?IsFirstTableDesign=True`) que llama a **`POST /BuscarLicitacion/Home/Buscar`** — un endpoint JSON-in/HTML-out, **sin sesión ni login** (probado sin autenticar, con y sin credenciales de `.env`, mismo resultado). Payload real capturado en vivo:
  ```json
  {
    "textoBusqueda": "cloud computing", "idEstado": "-1", "codigoRegion": "-1", "idTipoLicitacion": "-1",
    "fechaInicio": "2026-01-01T00:00:00.000Z", "fechaFin": "2026-08-04T23:59:59.000Z",
    "registrosPorPagina": "10", "idTipoFecha": [], "idOrden": "1",
    "compradores": [], "garantias": null, "rubros": [], "proveedores": [],
    "montoEstimadoTipo": [0], "esPublicoMontoEstimado": null, "pagina": 0
  }
  ```
  Responde un fragmento HTML server-renderizado (no JSON estructurado) con tarjetas `.lic-bloq-wrap`, cada una con `ID Licitación: {codigo}`, nombre, organismo, montos y fechas — **confirmado con datos reales** ("cloud computing" → 147 resultados reales, incluyendo licitaciones donde TIVIT nunca participó).
- **Se probó el parámetro `proveedores`** esperando que filtrara directamente por competidor — no funcionó con texto libre (`["Entel"]`, `["Sonda"]`, `["Tivit"]` devolvieron todas la misma respuesta corta, sin resultados), probablemente porque espera un ID resuelto vía un autocomplete que no se llegó a capturar (la UI tiene un filtro "+ Agregar proveedor" visible, pero no se pudo terminar de inspeccionar su llamada de resolución en el tiempo disponible — no se insistió más para evitar quedar enganchado en la investigación en vivo).
- **Decisión revisada**: no se necesita ese filtro. El flujo de dos pasos ya planeado sigue siendo válido y ahora es más liviano: (1) `buscarPublico.js` llama a este endpoint por HTTP plano (`fetch` nativo de Node 20 + `cheerio` para parsear el HTML, **sin Playwright** para este paso — a diferencia de `buscar.js`, que sí necesita navegador por el postback de WebForms) para obtener la lista de códigos de licitación del área/período; (2) para cada código, se construye la URL directa de ficha `https://www.mercadopublico.cl/Procurement/Modules/RFB/DetailsAcquisition.aspx?idlicitacion={codigo}` (mismo template ya usado por `AdjuntosHttpExtractor.cs`, spec 016) como `licitacion.urlFicha`, y se reutiliza `extraerDatosLicitacion(page, context, licitacion)` + `extraerCuadroOfertas(fichaPage, context, datos, carpetaDestino)` **sin modificarlos** — ahí sí hace falta Playwright, porque la ficha completa (incluido el Cuadro de Ofertas) es una página WebForms real.
- **Impacto en el riesgo de volumen**: al no depender de Playwright para la búsqueda (solo para visitar las fichas que realmente hacen falta), el costo de "buscar" bajó a prácticamente cero — el costo real sigue siendo visitar cada ficha con navegador, exactamente igual que hoy. No cambia el riesgo de volumen del paso 2, pero elimina cualquier riesgo adicional del paso 1.

**Riesgo de volumen — RESUELTO 2026-08-04, corrida real end-to-end contra producción**: `competidor-mercado.js` implementado y corrido en vivo dentro del contenedor Docker de la API contra `www.mercadopublico.cl` real (competidor "Telefonica", área Cloud, 2026-01-01 a 2026-07-31, término "cloud"). Resultado: **7 licitaciones candidatas**, cada una visitada con Playwright en pocos segundos, ciclo completo terminado en menos de un minuto, resultado persistido correctamente en `competidores_actividad_mercado` (`estado='listo'`). La mitigación de "acotar por área+período" (en vez de buscar sobre las 183k+ licitaciones totales) es suficiente — no fue necesario acotar también por región/organismo. Bug real encontrado y corregido durante esta validación: el endpoint público `POST /BuscarLicitacion/Home/Buscar` responde `200` con lista vacía si no se manda antes una cookie de sesión obtenida con un `GET` a la página del buscador (no es login, es solo una cookie de sesión anónima) — `buscarPublico.js` ahora hace ese `GET` una vez por corrida y reutiliza la cookie.

## 5. Flujo colaborativo go/no-go (US5)

**Hallazgo clave**: ya existen primitivas reutilizables en `MPM.Modules.Mensajeria` que cubren "asignar a varias personas" + "comentarios visibles entre ellas" casi sin cambios:
- `conversaciones` (V013) ya tiene una columna opcional `licitacion_id BIGINT REFERENCES licitaciones(id)` — pensada exactamente para este caso, aunque nunca se usó.
- `conversacion_participantes` (V014) es el patrón de "asignación" (user↔conversación, con `rol` y soft-remove vía `left_at`).
- `mensajes` (V015) es el patrón de "comentario interno", con autor y fecha, y ya tiene push en tiempo real vía SignalR.

**Decisión**: no crear tablas de comentarios/asignación nuevas. Al marcar una licitación "de interés": (1) se crea (o reutiliza, si ya existe) el `analisis_workspaces` de esa licitación vía el flujo ya existente de Analisis; (2) se crea una `conversaciones` (`tipo='grupal'`, `licitacion_id` seteado) vía el endpoint ya existente de Mensajería; (3) se persiste el vínculo entre licitación, workspace y conversación en una tabla nueva y pequeña, `licitaciones_interes(licitacion_id UNIQUE, workspace_id, conversacion_id, marcado_por, created_at)`. Asignar trabajadores = agregar filas a `conversacion_participantes` (ya existe). Comentar = enviar `mensajes` a esa conversación (ya existe, con realtime).

**Por qué evita nuevo acoplamiento entre módulos**: la Constitución (Principio I) prohíbe que los módulos se referencien directamente entre sí. En vez de que un módulo nuevo llame en C# a `AnalisisService` y a `ConversacionService` de otros módulos, la orquestación de los 3 pasos ocurre en el **frontend** — cada paso es una llamada HTTP independiente a un endpoint que ya existe (o a los 1-2 endpoints nuevos y pequeños de la tabla `licitaciones_interes`), igual a como otros flujos multi-módulo del sistema ya se coordinan hoy (p. ej. Alertas dispara notificaciones sin llamar directamente a Notificaciones en proceso). `licitaciones_interes` vive en un módulo nuevo, pequeño, sin lógica de negocio prestada de otros módulos — solo el vínculo entre 3 IDs y quién lo marcó.

**Alternativa considerada**: definir interfaces compartidas en `MPM.Shared` (p. ej. `IWorkspaceOrchestrator`) para que un solo backend-call orqueste los 3 pasos en una transacción. Se descartó para v1 — agrega superficie nueva a `MPM.Shared` (que hoy solo tiene `TenantContext`/`IStorageService`, sin lógica de orquestación) por una ganancia de atomicidad menor (si el paso 2 o 3 falla, el frontend puede reintentar solo ese paso; no hay riesgo real de dato huérfano grave). Revisar si en la práctica los 3 pasos necesitan ser atómicos una vez que exista telemetría real de fallos parciales.

**Nota para `tasks.md`**: confirmar contra el código real, al implementar, el endpoint exacto de creación idempotente de `analisis_workspaces` (¿ya existe un "get-or-create por licitación", o hay que agregarlo?) y el de creación de `conversaciones` — esta ronda de research no verificó cada firma línea por línea, solo confirmó que las tablas/relaciones correctas ya existen.
