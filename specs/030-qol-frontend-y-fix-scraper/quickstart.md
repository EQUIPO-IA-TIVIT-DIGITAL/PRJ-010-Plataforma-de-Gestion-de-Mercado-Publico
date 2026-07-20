# Quickstart: Validación de Ajustes QoL de Frontend + Fix Scraper

## Prerrequisitos

- Stack corriendo local (`docker compose up --build`, API → `:5001`, Web → `:8181`) o `dotnet run --project src/MPM.Api` + `npm run dev` en `src/mpm-web`.
- Al menos un competidor con licitaciones ganadas/perdidas en la base (para US1), varios análisis con distintas fechas (para US4), y una notificación reciente conocida (para US2).

## US1 — Claridad "ganada(s)" en `/ejecutivo`

1. Entrar a `/ejecutivo`.
2. Ubicar un competidor con `vecesGanador > 0` en el ranking.
3. **Esperado**: el texto de la tarjeta deja explícito que esas victorias son del competidor (no de TIVIT), sin necesidad de abrir la tabla de detalle.
4. Abrir el detalle del competidor y comparar la cifra de "ganadas por el competidor" contra la tarjeta — deben coincidir.
5. Ubicar un competidor con `vecesGanador == 0` — la etiqueta de "ganada(s)" no debe aparecer.

## US2 — Fecha correcta en `/notificaciones`

1. Generar una notificación en un instante conocido (ej. disparar una alerta de prueba, o anotar la hora real de Chile al momento de la prueba).
2. Entrar a `/notificaciones` y ubicar esa notificación.
3. **Esperado**: la hora mostrada coincide con la hora real de Chile en el momento del evento, con margen de error menor a 1 minuto — independiente de la zona horaria configurada en el navegador de prueba.

## US3 — Scraper "0 licitaciones, código 0"

1. Ejecutar un ciclo real del scraper contra Mercado Público (`EjecutarCicloUnaVezAsync` o el ciclo programado).
2. **Esperado en condiciones normales**: el ciclo procesa un número de licitaciones mayor a 0, consistente con el volumen esperado.
3. Simular/forzar que los 5 estados de búsqueda fallen (ej. cortar la sesión o inyectar un error en `buscarLicitaciones`) y confirmar que el ciclo **no** termina con código 0 — debe reportarse como fallo, no como "0 resultados legítimos".
4. Confirmar en `/notificaciones` que un ciclo con 0 licitaciones nuevas pero lectura exitosa del sitio genera un mensaje distinto (severidad normal) al de un ciclo que no pudo leer ningún estado (severidad de error).

## US4 — Filtro, orden y fecha visible en `/analisis`

1. Entrar a `/analisis` con varios análisis de distintas fechas.
2. **Esperado**: aparecen ordenados de más reciente a más antiguo por defecto, y cada fila muestra su fecha sin abrir el detalle.
3. Aplicar un filtro de rango de fechas que incluya solo algunos análisis conocidos.
4. **Esperado**: la lista se acota exactamente a los análisis dentro de ese rango.

## US5 / US6 — Rediseño de `/analisis/:id` y `/analisis/:id/dashboard`

1. Abrir un análisis con documentos en distintos estados (pendiente, procesando, completado, error).
2. **Esperado**: se puede identificar el estado de cada documento y las acciones disponibles sin ambigüedad visual.
3. Abrir el dashboard de un análisis completado.
4. **Esperado**: ningún dato aparece repetido sin propósito visual claro; la jerarquía visual prioriza los hallazgos principales.
5. Confirmar que toda la funcionalidad previa (subir documento, abrir chat de análisis, ver hallazgos) sigue disponible sin pasos adicionales.

## US7 — Rediseño de `/alertas`

1. Crear una regla de alerta nueva (palabras clave + canal Telegram).
2. Editarla y luego desactivarla.
3. **Esperado**: el flujo completo funciona sin fricción adicional respecto a la versión anterior, con una presentación visual consistente con el resto del sistema.

## Regresión general

- Recorrer `/mensajes` y `/catalogos` y confirmar que no hay cambios de comportamiento (fuera de alcance de este spec, FR-015/FR-016).
