# Feature Specification: Medición de Scoring reCAPTCHA y Hardening de Descarga de Archivos

**Feature Branch**: `034-medicion-recaptcha-hardening-descargas`

**Created**: 2026-08-19

**Status**: In Progress

**Input**: User description: "Diagnosticar y resolver el bloqueo 403 de Mercado Público que ocurre exclusivamente en la descarga de archivos (ViewAttachment.aspx) en producción, midiendo empíricamente el scoring de reCAPTCHA y comparando configuraciones de Playwright para elevar el score a nivel humano (>= 0.7) sin usar proxies."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Benchmark y Medición de Scoring de Detección de Bots (Priority: P1)

Como desarrollador/operador de la plataforma, quiero ejecutar un script de diagnóstico que mida numéricamente el score de Google reCAPTCHA v3 / Enterprise ($0.0$ a $0.9$) y evalúe 15 vectores de huella digital de navegador bajo diferentes configuraciones de Playwright, para identificar con datos duros qué técnicas de evasión elevan el score a rango humano.

**Why this priority**: Sin métricas empíricas de scoring, cualquier cambio de código en producción se hace a ciegas. Medir el score contra endpoints de prueba permite validar objetivamente la efectividad de las defensas.

**Independent Test**: Se ejecuta `node tools/scraper-mp-v2/test_fingerprint_benchmark.js` y se verifica que entrega una tabla comparativa con scores reales de reCAPTCHA y estado de comprobaciones de fingerprint (webdriver, WebGL, plugins, etc.).

**Acceptance Scenarios**:
1. **Given** un navegador en modo headless básico, **When** consulta el endpoint de reCAPTCHA v3, **Then** el score obtenido es bajo ($\le 0.3$) y `navigator.webdriver` marca detectable.
2. **Given** un navegador con `playwright-extra` + plugin `stealth`, **When** consulta el endpoint de reCAPTCHA v3, **Then** el score sube a $\ge 0.7$ y `navigator.webdriver` es `undefined`.

---

### User Story 2 - Hardening del Navegador en el Módulo de Descargas (Priority: P2)

Como sistema de backend (MPM.Api / AdjuntoDescargaService), quiero que el scraper de descargas (`descargar-documentos.js` y `adjuntos.js`) aplique emulación de interacción humana (mouse jitter, hover previo al click en `#imgAdjuntos`) y mantenga el contexto de navegación y `window.opener` intactos, para que `ViewAttachment.aspx` resuelva su reCAPTCHA y redirija exitosamente a `ViewAttachmentLC.aspx` sin recibir 403.

**Why this priority**: Es la solución al punto de dolor principal del usuario: la descarga de actas y anexos falla en producción con 403.

**Independent Test**: Se ejecuta `node tools/scraper-mp-v2/descargar-documentos.js --codigo="1191449-18-LE26" --licitacionId=1` y se verifica la descarga física del archivo en disco con salida `[DESCARGA] resultado=exito`.

**Acceptance Scenarios**:
1. **Given** una licitación con documentos adjuntos en Mercado Público, **When** se invoca `descargar-documentos.js` con el browser hardening activo, **Then** el popup `#imgAdjuntos` abre `ViewAttachment.aspx`, pasa el gate de reCAPTCHA y descarga los archivos sin disparar `BLOQUEO_ROBOT` ni 403.
2. **Given** un error de navegación o cambio de estructura en el portal, **When** falla la apertura, **Then** se captura un screenshot de diagnóstico y se reporta el motivo sin quedar colgado.

---

### User Story 3 - Compatibilidad Transparente con Despliegue en Cloud Run (Priority: P3)

Como DevOps / Infraestructura, quiero que el contenedor Docker de `MPM.Api` aproveche automáticamente `xvfb-run` y las librerías stealth instaladas sin necesidad de configurar proxies externos ni modificar variables de red en GCP, manteniendo el flujo de despliegue con `scripts/deploy.sh prod api`.

**Why this priority**: Asegura que el cambio se preserve en los despliegues de CI/CD y en los Cloud Run Jobs sin configuraciones adicionales.

**Acceptance Scenarios**:
1. **Given** el Dockerfile reconstruido con las nuevas dependencias, **When** se ejecuta en Cloud Run, **Then** `AdjuntoDescargaService` detecta `xvfb-run` y lanza Playwright en modo headed virtual con las capas stealth activas.

---

## Edge Cases

- **Timeout de reCAPTCHA**: Si la red tarda más de 20s en resolver `ViewAttachment.aspx`, se debe reintentar con delay antes de marcar fallo definitivo.
- **Licitación sin documentos**: Si el portal responde legítimamente con tabla vacía o mensaje "no existen registros", debe reportarse como `completado` con 0 documentos, no como error 403.
- **Múltiples popups simultáneos**: Si el portal abre diálogos modales o popups de alerta, deben cerrarse automáticamente sin interrumpir la captura del evento de descarga.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001**: El benchmark MUST evaluar al menos 3 configuraciones de Playwright y mostrar los puntajes de reCAPTCHA v3.
- **FR-002**: `browser.js` MUST utilizar `playwright-extra` con `puppeteer-extra-plugin-stealth` para eliminar señales de automatización.
- **FR-003**: `descargar-documentos.js` MUST emular movimiento de cursor y hover previo al click de apertura de `#imgAdjuntos`.
- **FR-004**: `descargar-documentos.js` MUST preservar la relación de ventana y referer original al capturar la página de adjuntos.
- **FR-005**: Todo fallo en la extracción MUST persistir screenshot de diagnóstico con timestamp en la carpeta de salida.
