# Guía de Inicio Rápido — MPM (Mercado Público Management)

## Requisitos Previos

- Docker Desktop instalado y corriendo
- Archivo `.env` en la raíz del proyecto (ver sección Variables de Entorno)

## Inicio Rápido

```bash
# 1. Copiar y completar el archivo de entorno
cp .env.example .env
# Editar .env con tus credenciales (ver sección Variables de Entorno)

# 2. Construir y levantar todos los servicios
docker compose up --build -d

# 3. Ver logs en tiempo real
docker compose logs -f
```

## Accesos

| Servicio   | URL                          | Descripción          |
|------------|------------------------------|----------------------|
| Frontend   | http://localhost:8181        | Aplicación web React |
| API        | http://localhost:5001        | API REST .NET 8      |
| Swagger    | http://localhost:5001/swagger | Documentación API   |
| PostgreSQL | localhost:5433               | Base de datos        |
| Redis      | localhost:6379               | Cache y SignalR      |

## Credenciales de Prueba

```
admin@tivit.cl   /  test123   (rol Admin)
analista@tivit.cl / test123   (rol Analista)
```

## Variables de Entorno (`.env`)

```env
# Base de datos
DB_USER=mpm
DB_PASSWORD=mpm_password
DB_NAME=mpm_db

# Redis
REDIS_PASSWORD=redis_password

# JWT (mínimo 32 caracteres)
JWT_SECRET=CHANGE-THIS-IN-PRODUCTION-MIN-32-CHARS-LONG
JWT_ISSUER=MPM
JWT_AUDIENCE=MPM

# Gemini AI (requerido para análisis de licitaciones)
GEMINI_API_KEY=your-gemini-api-key

# Mercado Público (requerido para el scraper)
MP_RUT=12345678-9          # RUT del usuario en mercadopublico.cl
MP_PASSWORD=tu_password    # Contraseña del portal

# Scraper (opcional — false por defecto)
SCRAPER_ENABLED=false
SCRAPER_INTERVAL_HOURS=12
MP_ANALISIS_IA=true        # Activa análisis Gemini automático
MP_FECHA_DESDE=01-01-2025  # Fecha inicio de búsqueda de licitaciones

# Storage (local por defecto)
Storage__Provider=local
```

## Módulo de Análisis IA

El sistema extrae automáticamente las "Actas de Evaluación" de licitaciones adjudicadas
en Mercado Público y las analiza con Gemini AI.

### Activar el scraper automático

```bash
# En .env, cambiar:
SCRAPER_ENABLED=true
MP_RUT=tu-rut
MP_PASSWORD=tu-password
MP_ANALISIS_IA=true

# Reiniciar el servicio api
docker compose up -d api
```

El scraper corre 30 segundos después del arranque y luego cada `SCRAPER_INTERVAL_HOURS` horas.

### Ejecutar el scraper manualmente (fuera de Docker)

```bash
cd tools/scraper-mp-v2
cp .env.example .env    # completar con tus credenciales
npm install
npx playwright install chromium

# Solo scraping (sin análisis IA)
node agente-mp.js

# Con análisis IA
MP_ANALISIS_IA=true node agente-mp.js
```

### Verificar el pipeline

```bash
# Ver logs del scraper en tiempo real
docker compose logs -f api | grep -i scraper

# Verificar licitaciones scrapeadas
docker compose exec db psql -U mpm -d mpm_db -c \
  "SELECT codigo, nombre, estado FROM licitaciones ORDER BY created_at DESC LIMIT 10;"

# Verificar workspaces de análisis
docker compose exec db psql -U mpm -d mpm_db -c \
  "SELECT nombre, estado FROM analisis_workspaces ORDER BY created_at DESC LIMIT 10;"
```

### Health-check del scraper al arranque

Al iniciar, el `ScraperBackgroundService` loguea:
```
ScraperBackgroundService starting. Interval: 12h
```

Si Node.js no está disponible o el script no se encuentra, se crea una notificación
`scraper_config_error` visible en el frontend (campanita) y en los logs:
```
docker compose logs api | grep -i "config_error\|node.*not found\|script no encontrado"
```

## Módulo de Mensajería

- Chat 1-a-1 y grupal con SignalR
- Archivos adjuntos (límite 10 MB)
- Edición de mensajes (ventana de 15 min)
- Indicador de escritura y presencia online/offline

## Comandos Útiles

```bash
# Ver logs de un servicio
docker compose logs -f api
docker compose logs -f web

# Detener todos los servicios
docker compose down

# Detener y borrar datos (incluye volúmenes)
docker compose down -v

# Reconstruir imágenes
docker compose build --no-cache

# Reiniciar un servicio
docker compose restart api

# Consola PostgreSQL
docker compose exec db psql -U mpm -d mpm_db

# Ver migraciones aplicadas
docker compose exec db psql -U mpm -d mpm_db -c "SELECT * FROM _migrations ORDER BY installed_on DESC LIMIT 10;"
```

## Desarrollo Local

```bash
# Backend .NET
dotnet run --project src/MPM.Api

# Frontend React (http://localhost:3000)
cd src/mpm-web && npm install && npm run dev
```

## Estructura del Proyecto

```
MPM/
├── src/
│   ├── MPM.Api/                        # API REST .NET 8
│   ├── MPM.Core/                       # Lógica compartida
│   ├── MPM.Shared/                     # Modelos compartidos
│   ├── MPM.Modules.Auth/               # Autenticación JWT
│   ├── MPM.Modules.Licitaciones/       # Sync + Scraper
│   ├── MPM.Modules.Analisis/           # Workspaces + Gemini AI
│   ├── MPM.Modules.Mensajeria/         # Chat SignalR
│   ├── MPM.Modules.Notificaciones/     # Notificaciones in-app
│   ├── MPM.Modules.Catalogo/           # Datos de referencia
│   ├── MPM.Modules.Alertas/            # Alertas por keyword (Telegram/email)
│   ├── MPM.Modules.Competidores/       # Inteligencia de competidores
│   └── mpm-web/                        # Frontend React 18
├── tools/
│   └── scraper-mp-v2/                  # Scraper Node.js + Playwright
├── tests/                              # Tests xUnit + Playwright E2E
├── specs/                              # Especificaciones de features
├── docs/                               # Documentación técnica
└── docker-compose.yml
```

## Soporte

- Documentación técnica: `docs/`
- Especificaciones activas: `specs/001-analisis-fases-sdd/`
- Issues: reportar en el repositorio
