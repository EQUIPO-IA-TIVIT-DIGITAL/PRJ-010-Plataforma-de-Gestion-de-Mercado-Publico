# Scraper Mercado Publico - TIVIT

Scripts para extraer datos y documentos de licitaciones desde Mercado Publico (mercadopublico.cl).

## Herramientas

| Script | Descripcion | Modo |
|--------|-------------|------|
| `agente-mp.js` | Agente automatizado completo | Automatico |
| `extraer.js` | Scraper manual interactivo | Manual |

## Requisitos

- Node.js 18+
- npm
- Playwright Chromium: `npx playwright install chromium`

## Instalacion

```bash
cd tools/scraper-mp
npm install
npx playwright install chromium
```

## Agente Automatizado (`agente-mp.js`)

Ejecuta todo el flujo automaticamente: login, busqueda, extraccion y descarga de Actas de Evaluacion.

### Configuracion (.env)

```env
# Credenciales Mercado Publico
MP_RUT=73058136
MP_PASSWORD=Tivit2025.

# Modo de ejecucion
MP_HEADLESS=false          # true para background, false para debug visual
MP_DELAY_MS=2000            # Milisegundos entre cada licitacion
MP_MAX_REINTENTOS=3
MP_ANALISIS_IA=false        # Invocar doc-processor despues de descargar
MP_FECHA_DESDE=01-01-2026  # Fecha desde para busqueda (formato DD-MM-YYYY)
```

### Ejecucion

```bash
# Modo visible (debug) - ver el navegador
npm run agente:debug

# Modo headless (background) - sin interfaz grafica
npm run agente:headless

# Por defecto (visible con delay de 2s)
npm run agente
```

### Flujo del Agente

```
1. Abre navegador y navega a mercadopublico.cl
2. Click "Iniciar Sesion" → Tab "Extranjero" → Ingresa RUT + password
3. Selecciona organizacion "TIVIT CHILE TERCERIZACION..."
4. Navega a "Licitaciones" → "Busqueda de Licitaciones para Ofertar"
5. Configura filtros: Region=Todas, Estado=Adjudicada, Desde=01/01/2026
6. Selecciona radio "Licitaciones en las que he ofertado"
7. Ejecuta busqueda y extrae lista de licitaciones
7. Para cada licitacion:
   a. Abre ficha (OpenGlobalPopup)
   b. Extrae datos: codigo, nombre, descripcion, demandante, fechas
   c. Busca "ver adjuntos"
   d. Busca documento tipo "Acta de Evaluacion"
   e. Si existe → descarga el archivo
   f. Si no existe → registra como "Sin Acta"
8. (Opcional) Ejecuta analisis IA con doc-processor
9. Genera reporte final en descargas/lote-YYYY-MM-DD/
```

### Estructura de salida

```
tools/scraper-mp/descargas/
└── lote-2026-06-09/
    ├── resumen.json          # Resumen de todas las licitaciones
    ├── reporte.txt           # Reporte en texto plano
    ├── 1213444-15-CO26/
    │   ├── datos.json        # Datos estructurados de la licitacion
    │   └── acta-evaluacion.pdf  # Acta de Evaluacion (si existe)
    ├── 3374-4-L126/
    │   ├── datos.json
    │   └── ...
    └── ...
```

## Scraper Manual (`extraer.js`)

Modo interactivo donde tu navegas y el script extrae bajo comando.

```bash
node extraer.js
> login      # Abre el navegador
> listo      # Confirma login manual
> extraer    # Extrae la pagina actual
> listar     # Ver extracciones
> salir      # Cierra
```

## Selectores del sitio (verificados)

| Elemento | Selector | Valor |
|----------|----------|-------|
| Boton "Iniciar Sesion" | `button.btn.btn-xl.btn-pri` | - |
| Tab "Extranjero" | `#liExtranjero` | - |
| Input RUT | `#username-re` | - |
| Input Password | `#password-re` | - |
| Boton "Ingresar Ahora" | `#kc-login-re` | - |
| Radio organizacion | `input[id*="rdbOrg"]` | - |
| Link "Ingresar" (org) | `a:has-text("Ingresar")` | - |
| Region "Todas" | `#cboRegion` | value=" " |
| Estado "Adjudicada" | `#cboState` | value="8" |
| Fecha desde | `#calFrom` | DD-MM-YYYY |
| Fecha hasta | `#calTo` | DD-MM-YYYY |
| Radio "Licitaciones en las que he ofertado" | `#radLicitacionOfertado` | - |
| Boton Buscar | `#btnSearch` | - |
| Link licitacion (ver ficha) | `a[onclick*="OpenGlobalPopup"]` | - |

## Notas

- El login usa Keycloak (heimdall.mercadopublico.cl)
- La password debe incluir el punto final: `Tivit2025.`
- El sitio usa WebForms con __doPostBack para los links de licitaciones
- Cada ficha se abre via OpenGlobalPopup() en nueva ventana
- Los adjuntos se acceden desde la ficha via "Ver Adjuntos"
- Delay de 2 segundos entre cada licitacion para evitar bloqueos