# Quickstart: Validación de los 18 fixes

Prerrequisitos: stack levantado (`docker compose up --build`, o API + `npm run dev` en `src/mpm-web`), acceso a la base de datos de dev/staging.

## 1. FR-001 — `codigo_estado` no se resetea a un valor inválido

1. Tomar una licitación real con `codigo_estado = 8` (Adjudicada).
2. Ejecutar manualmente el merge (`usp_SyncEngine_MergeLicitaciones` o el flujo de sync completo) simulando un `codigo_estado` entrante inválido (ej. `99`, que no existe en `estados_licitacion`).
3. **Esperado**: la licitación conserva `codigo_estado = 8` después del merge, y el log de la aplicación muestra un `LogWarning` de `SyncEngineHandler` (`MergeLicitaciones: algunos items del lote fallaron: ...`) referenciando esa licitación con el código rechazado (vía `p_error_msg`, no una tabla separada).
4. **Antes del fix**: el mismo escenario dejaba `codigo_estado = 1` sin ningún registro de auditoría.

## 2. FR-002 — Búsqueda NL encuentra licitaciones anteriores a 2026

1. Ir a `/licitaciones`, usar el buscador en lenguaje natural.
2. Buscar algo con fecha explícita anterior a 2026 (ej. "licitaciones de servicios cloud de 2025").
3. **Esperado**: retorna resultados reales de 2025 si existen en la base.
4. **Antes del fix**: la lista siempre quedaba vacía para cualquier consulta de un período anterior a 2026-01-01.

## 3. FR-003 — Análisis de competidor con respuesta bloqueada no rompe la página

1. En un entorno de prueba, forzar (mock o dato que dispare el filtro de seguridad de Gemini) una respuesta sin `candidates` para un competidor.
2. Solicitar el análisis desde `/competidores`.
3. **Esperado**: la UI muestra un mensaje de error claro ("no fue posible generar el análisis..."), no una pantalla rota ni un toast genérico de "error inesperado". El request responde 422 con `error: gemini_contenido_bloqueado`.
4. **Antes del fix**: 500 sin contexto, cero manejo en frontend.

## 4. FR-004 — Enriquecimiento en caliente no toca licitaciones eliminadas

1. Marcar (soft-delete, `deleted_at`) una licitación de prueba.
2. Disparar el flujo de matching de Alertas de forma que intente enriquecerla en caliente (organismo vacío + match de keyword).
3. **Esperado**: la licitación sigue con `deleted_at` no nulo y sin cambios en `organismo`/`monto_estimado`/`raw_data`.
4. **Antes del fix**: el UPDATE la modificaba igual, ignorando el borrado lógico.

## 5. FR-005 — Tabla de ofertas con columnas reordenadas no corrompe datos

1. Con un HTML de prueba (fixture) de "Cuadro de Ofertas" cuyas columnas estén en un orden distinto al esperado (ej. `Estado` antes que `Total Oferta`), correr el parser de `cuadroOfertas.js` sobre ese fixture.
2. **Esperado**: la fila se descarta/loggea como no reconocida, no se guarda `monto_oferta`/`estado_oferta` en la columna equivocada.
3. **Antes del fix**: los valores se guardaban desplazados silenciosamente.

## 6. FR-006 — Cliente Gemini compartido y mismo límite de tokens

1. Revisar que `CompetidorGeminiService` y `GeminiService` invocan el mismo `VertexGeminiClient` de `MPM.Shared`.
2. Generar un análisis de competidor con una respuesta larga (muchas recomendaciones/patrones) y confirmar que no se trunca antes de completar el JSON.
3. **Esperado**: `maxOutputTokens` efectivo es el mismo que usa Análisis (65536), sin duplicación de la lógica de armado/parseo de request en dos archivos.

## 7. FR-007 — Monto $0 se distingue de dato faltante

1. En `/competidores`, ver una oferta con `montoOferta = 0` (o simular el dato).
2. **Esperado**: se muestra `$0`, no `—`.
3. Ver una oferta con `montoOferta = null`.
4. **Esperado**: sigue mostrando `—`.

## 8. FR-008 — Fallback del scraper no apunta al deprecado

1. Sin configurar `Extraccion:ExportarSesionScriptPath`, revisar el valor de fallback resuelto por `MpSessionProvider.cs` (test unitario o inspección de config resuelta).
2. **Esperado**: apunta a `tools/scraper-mp-v2/exportar-sesion.js`, no a `tools/scraper-mp/exportar-sesion.js`.

## 9. FR-009 (QA BUG-002) — Filtro de fecha normal ya no da 500

1. Ir a `/licitaciones` (no el buscador NL), aplicar un filtro "Desde" y/o "Hasta" con un rango donde se sepa que existen licitaciones reales.
2. Revisar la pestaña Network (F12) mientras se aplica el filtro.
3. **Esperado**: la petición a `/api/v1/licitaciones` responde 200 con resultados reales dentro del rango.
4. **Antes del fix**: la petición respondía 500, y la tabla mostraba "No hay datos" sin ningún aviso de error.
5. Repetir combinando el filtro de fecha con estado/tipo/organismo a la vez — debe seguir funcionando en combinación (edge case de la spec).
6. Para confirmar el manejo de error real (no solo el caso ya arreglado): simular un 500 genuino (ej. apagar temporalmente la BD en un entorno de prueba) y confirmar que el frontend muestra un mensaje de error visible, no una tabla vacía silenciosa.

## 10. SC-008 (QA BUG-001) — Regresión: filtro "Estado" sin duplicados

1. Ir a `/licitaciones`, desplegar el filtro "Estado".
2. **Esperado**: aparecen exactamente 5 opciones (Publicada, Cerrada, Desierta, Adjudicada, Revocada — o los nombres reales vigentes), sin duplicados.
3. Seleccionar cada una y confirmar que todas filtran la tabla correctamente.
4. **Si falla**: no se asume resuelto por `V108` — se reclasifica como bug abierto (prioridad Alto, igual que en el reporte de QA original) y se investiga por qué la migración no tuvo el efecto esperado en ese entorno.

## 11. FR-010 (QA BUG-003) — Licitaciones del import masivo muestran tipo/organismo real

1. Antes del backfill: en `/licitaciones`, aplicar el filtro Tipo = "Trato Directo" (o "Convenio Marco"/"Compra Ágil") y confirmar que hoy no trae resultados (reproduce el bug).
2. Correr el job de backfill sobre un subconjunto de prueba de licitaciones del import masivo.
3. **Esperado**: las licitaciones procesadas quedan con `tipo` real (no genérico "Licitacion") y `organismo` poblado cuando es recuperable.
4. Repetir el filtro Tipo = "Trato Directo": **esperado** que ahora devuelva resultados reales.
5. Confirmar que una licitación ya auto-corregida al abrir su detalle (antes del backfill) no queda peor ni duplica trabajo tras correr el backfill sobre ella.

## 12. FR-011 (QA BUG-005) — "Analizar todo" procesa todos los documentos

1. Crear un workspace y subir 4+ documentos de una misma licitación con datos distintos entre sí (ej. un monto que solo aparece en el 2º o 3º documento).
2. Usar "Analizar todo".
3. **Esperado**: el dashboard resultante incluye información verificable de más de un documento (ej. el monto que solo estaba en el 3º documento aparece reflejado).
4. **Antes del fix**: solo se reflejaba información del primer documento de la lista.

## 13. FR-012 (QA BUG-010) — El análisis advierte sobre documentos revocados

1. Subir dos versiones de una resolución sobre la misma licitación, donde la segunda revoca explícitamente a la primera (o usar el caso real documentado por QA: REX N°280 vs. la Resolución de Acta de Adjudicación final).
2. Analizar el workspace con "Analizar todo" (depende de FR-011).
3. **Esperado**: el dashboard advierte que existe un documento posterior que deja sin efecto a la resolución revocada, en vez de presentar la conclusión revocada ("TIVIT: Inadmisible") como vigente.

## 14. FR-013 (QA BUG-008) — Moneda real, no dólares por defecto

1. Analizar el Informe de Evaluación de LP-4609 (o un documento equivalente con una cifra en CLP explícita).
2. Ver el dashboard, campo "Monto adjudicado".
3. **Esperado**: se muestra en pesos chilenos, no con símbolo `US$`.
4. Analizar un documento que sí reporta una cifra en dólares explícitamente: **esperado** que se siga mostrando correctamente en dólares (no se debe invertir el problema).

## 15. FR-014 (QA BUG-009) — Solo se marca "Inadmisible" a quien el documento declara así

1. Analizar el Informe de Evaluación de LP-4609.
2. Ver la tabla de "Ofertantes" del dashboard.
3. **Esperado**: Kepler Latam SPA y Tichile Reventa de Software y Hardware SPA aparecen como admisibles, igual que en el documento fuente. Adaptive Security SPA sigue apareciendo como inadmisible (caso correcto que no debe romperse).

## 16. FR-015 (QA BUG-006) — "Monto estimado" no se confunde con monto ofertado

1. Analizar un workspace cuyo documento reporte tanto el presupuesto del organismo como los montos ofertados por participantes.
2. Ver "Monto estimado" en el dashboard.
3. **Esperado**: no coincide exactamente con "Monto ofertado" de TIVIT ni de ningún competidor, salvo que el documento confirme que son, en efecto, el mismo valor.

## 17. FR-016 (QA BUG-004) — Notificación de análisis completado es global

1. Iniciar un análisis en un workspace.
2. Navegar a otra página/otro workspace mientras se procesa.
3. Esperar a que el análisis termine sin volver a esa página.
4. **Esperado**: la notificación "Análisis completado" aparece de todas formas, sin importar en qué parte de la app esté el usuario.
5. **Antes del fix**: la notificación nunca aparecía si el usuario no estaba físicamente en la página del workspace en el momento exacto del cambio de estado.

## 18. FR-017 (QA BUG-007) — Formato y coherencia interna del dashboard

1. Generar cualquier análisis y revisar el dashboard completo de principio a fin.
2. **Esperado**: el mismo hecho numérico no aparece con señalización de color contradictoria en dos tarjetas distintas; el formato de moneda es consistente; un badge de estado (ej. "✓ Coherente") no es contradicho por el texto de la misma sección.

## 19. FR-018 (QA BUG-011) — Filtro de año del Dashboard Ejecutivo usa fecha real

1. Con licitaciones analizadas cuya fecha real (publicación o adjudicación) sea de 2024 o 2025, ir a Dashboard Ejecutivo y abrir el filtro de año.
2. **Esperado**: 2024/2025 aparecen como opciones, no solo el año en que se ejecutó el análisis.

## 20. FR-019 (QA BUG-012) — Se puede crear una conversación directa

1. Ir a Mensajes → Nueva conversación.
2. Seleccionar Tipo = "Directa (1 a 1)", elegir un participante real, hacer clic en "Crear".
3. **Esperado**: la conversación se crea exitosamente y aparece en la lista, con exactamente ese participante y el usuario actual.
4. Repetir con Tipo = "Grupal" (ya funcionaba): **esperado** que siga funcionando sin regresión.
