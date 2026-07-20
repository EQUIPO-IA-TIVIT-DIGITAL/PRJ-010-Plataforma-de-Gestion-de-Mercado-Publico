# ⚠️ DEPRECADO (2026-07-16)

Este scraper (v1) fue reemplazado por **`tools/scraper-mp-v2/`**, que es el que ahora copia
el Dockerfile del API (`src/MPM.Api/Dockerfile`) y ejecuta `ScraperBackgroundService`.

Mejoras de v2 sobre v1 (ver detalle en el historial y en `specs/026-robustez-sincronizacion-tipos-reales/`):

- **Sesión persistida en BD** (`scraper_session`): reutiliza cookies y salta el login de
  Keycloak entre ciclos; se invalida sola tras login fallido o bloqueo robot.
- **Aserción positiva de login**: exige `#lnkEndSession` en `Menu.aspx` en vez de heurísticas
  de URL (que daban falso "login exitoso" con el modal de organización aún abierto).
- **Fix del postback colgado de ASP.NET**: la causa real de que v1 "no trajera nada" — en
  búsquedas consecutivas el UpdatePanel quedaba colgado y v1 leía la tabla vieja. v2 detecta
  el fin del postback por la transición de `get_isInAsyncPostBack()` y recarga la página
  entre estados.
- **Nuevo headless de Chromium** (`channel: 'chromium'`): corre sin pantalla y sin Xvfb, sin
  disparar reCAPTCHA (validado en vivo 2026-07-16). Xvfb queda como plan B.
- Detección de bloqueo robot también en login + marcadores stdout `LOGIN_FALLIDO: true` /
  `BLOQUEO_ROBOT: true` para las alertas del wrapper .NET.

No agregar features aquí. Esta carpeta se conserva solo como referencia histórica
(spikes `spike-*.js`, `exportar-sesion.js` que aún referencia `MpSessionProvider.cs`, y
lotes descargados en `descargas/`).

## Adenda: "0 licitaciones, código 0" reapareció en v2 (030-qol-frontend-y-fix-scraper, 2026-07-20)

El fix del postback colgado de arriba cubre el cuelgue *dentro* de un intento de búsqueda, pero
no el caso en que **los 5 estados fallaran sus 2 intentos cada uno** (ej. una racha de cuelgues
más larga que el retry, sesión inválida, o un cambio de estructura no cubierto por el resto de
las heurísticas). En ese escenario, `buscarLicitaciones()` (`scraper-mp-v2/modulos/buscar.js`)
tragaba el error de cada estado en un `console.log` de advertencia y, al no tener éxito ninguno,
retornaba `[]` — indistinguible de "0 licitaciones nuevas legítimas". Además, en modo `--daemon`,
el `catch` de `cycle()` (`scraper-mp-v2/modulos/scheduler.js`) solo logueaba el error sin marcar
`process.exitCode`, así que el proceso terminaba con el código de salida por defecto de Node (0)
aunque el ciclo hubiera fallado por completo. Combinado, esto reproducía exactamente el patrón
"El scraper terminó con código 0. Licitaciones: 0, Actas: 0" para una falla real, indistinguible
de un día sin licitaciones nuevas.

Fix: `buscarLicitaciones()` ahora cuenta cuántos de los 5 estados tuvieron éxito y lanza un error
real si son 0 de 5; `scheduler.js` marca `process.exitCode = 1` en su catch. El wrapper .NET
(`ScraperBackgroundService.NotificarResultadoAsync`) distingue ahora tres casos: éxito con
licitaciones nuevas, éxito sin licitaciones nuevas (tipo `scraper_sin_resultados`, tono neutro) y
fallo real (tipo `scraper_error`, `exitCode != 0`).
