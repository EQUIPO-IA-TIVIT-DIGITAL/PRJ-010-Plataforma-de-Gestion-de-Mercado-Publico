# Quickstart: Inteligencia de competencia, alertas interactivas y canal de correo

## Prerrequisitos

- `docker compose up --build` corriendo local, o producción (`tivit-cu010`) con los fixes de `023-fix-bugs-produccion` ya desplegados.
- Al menos algunas licitaciones adjudicadas con ofertas ya recolectadas (correr el scraper con el módulo `cuadroOfertas.js` sobre una muestra) para poder probar US1 de punta a punta.
- Un usuario de prueba con Telegram vinculado (para US2) y con la opción de configurar un correo (para US3).

## Spike previo recomendado (antes de comprometerse al alcance completo de US1)

1. Tomar una muestra de 20-30 `codigo_externo` de licitaciones ya sincronizadas, de distintos tipos (Licitación Pública, Trato Directo, Convenio Marco, Compra Ágil) y organismos.
2. Para cada una, visitar manualmente (o con un script chico) su ficha pública y confirmar si el ícono "Cuadro de ofertas" existe y trae datos.
3. **Esperado**: confirmar el % de cobertura real antes de correr el scraper sobre las 126k completas — documentar el resultado en `research.md` R3 si cambia la conclusión.

## US1 — Panel de inteligencia de competencia

1. Con datos de ofertas ya recolectados, ir al panel de Competidores, buscar por nombre (ej. "Sonda").
2. **Esperado**: ver el listado de licitaciones donde ese nombre aparece como oferente, con monto y estado de cada oferta — sin ninguna llamada a Gemini todavía.
3. Elegir un rango de fechas, click en "Analizar con IA".
4. **Esperado**: antes de confirmar, se muestra cuántas licitaciones entrarían en el análisis (FR-006); al confirmar, se genera el análisis y se muestra.
5. Repetir la búsqueda con el mismo competidor y mismo rango.
6. **Esperado**: el resultado aparece de inmediato (<2s), sin generar un análisis nuevo — confirmar revisando que no hay una llamada nueva a Gemini (logs o contador de tokens).

## US2 — Telegram "Me interesa"

1. Disparar una alerta de prueba (`POST /api/v1/alertas/{id}/probar`) hacia un chat de Telegram vinculado.
2. **Esperado**: el mensaje llega con un botón inline "Me interesa".
3. Presionar el botón.
4. **Esperado**: en menos de 10s llega un segundo mensaje con el resumen (descripción, organismo, monto, fechas, requisitos) de esa licitación específica, sin haber generado ningún análisis de IA.

## US3 — Canal de correo

1. Configurar un correo de alertas para un usuario de prueba (nuevo endpoint análogo a `POST /api/v1/alertas/mi-telegram`).
2. Disparar una alerta de prueba para ese usuario.
3. **Esperado**: llega un correo con el mismo contenido informativo que la versión de Telegram.
4. Con el mismo usuario teniendo Telegram Y correo configurados, disparar otra alerta.
5. **Esperado**: llega por ambos canales; si se simula una falla en uno (ej. SMTP caído), el otro canal igual entrega.

## Verificación automatizable

Agregar al patrón ya usado en `022-qa-fixes-preproduccion/verify-live.sh`/`023-fix-bugs-produccion`:
- Chequeo de que un `POST` al endpoint de análisis de competidor con el mismo competidor+rango dos veces seguidas solo dispara una llamada real a Gemini (verificable por `cantidad_licitaciones`/timestamp de `competidores_analisis` sin cambiar entre ambas).
- Chequeo de que el webhook de Telegram responde 200 tanto a un `message` con `/start` como a un `callback_query` con `interesa:<id>`, sin romper el fail-closed existente (BUG-009).
