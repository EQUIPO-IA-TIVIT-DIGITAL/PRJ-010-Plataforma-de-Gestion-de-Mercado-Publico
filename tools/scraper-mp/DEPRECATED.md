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
