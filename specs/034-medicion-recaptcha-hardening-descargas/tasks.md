# Tasks: Medición de Scoring reCAPTCHA y Hardening de Descarga de Archivos

**Feature**: `034-medicion-recaptcha-hardening-descargas`

## Phase 1: Setup & Dependencias
- [x] T001 Instalar `playwright-extra` y `puppeteer-extra-plugin-stealth` en `tools/scraper-mp-v2/package.json`.
- [x] T002 Crear script de benchmark `tools/scraper-mp-v2/test_fingerprint_benchmark.js`.

## Phase 2: Medición y Benchmarking
- [x] T003 Ejecutar `test_fingerprint_benchmark.js` y medir scores contra Google reCAPTCHA v3 demo y Sannysoft.
- [x] T004 Comparar resultados de las 4 configuraciones (Headless base, Chromium channel, Stealth, Stealth + Jitter).

## Phase 3: Implementación del Hardening en Scraper
- [x] T005 [P] Actualizar `tools/scraper-mp-v2/modulos/browser.js` para usar `playwright-extra` con plugins stealth.
- [x] T006 [P] Actualizar `tools/scraper-mp-v2/modulos/adjuntos.js` con mouse jitter y emulación humana.
- [x] T007 [P] Actualizar `tools/scraper-mp-v2/descargar-documentos.js` con fix de popup y referer context.

## Phase 4: Verificación
- [x] T008 Ejecutar prueba unitaria de descarga en `tools/scraper-mp-v2`.
- [x] T009 Registrar resultados en walkthrough y actualizar documentación.
