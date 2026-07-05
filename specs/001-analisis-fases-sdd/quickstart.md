# Quickstart: Validación del Pipeline de Automatización (Fase 2)

**Feature**: MPM CU010 — Fase 2: Automatización del Scraping
**Date**: 2026-06-23

---

## Prerequisitos

1. Docker Desktop corriendo
2. Archivo `.env` en el root del proyecto con las variables de Mercado Público (ver [contracts/scraper-pipeline.md](./contracts/scraper-pipeline.md))
3. Navegadores Playwright instalados (se instalan automáticamente en el build Docker)

---

## Setup

```bash
# 1. Clonar/abrir el repositorio
cd "CU010 - Mercado Público"

# 2. Verificar que .env tiene las vars requeridas
cat .env | grep -E "MP_RUT|MP_PASSWORD|SCRAPER_ENABLED|MP_ANALISIS_IA|API_BASE_URL"

# 3. Levantar el stack completo
docker compose up --build -d

# 4. Verificar que todos los servicios arrancan
docker compose ps
```

---

## Escenario 1: Verificar que el scraper puede correr manualmente

```bash
# Ejecutar el scraper una vez (fuera de Docker, desde el directorio del scraper)
cd tools/scraper-mp
MP_RUT=<rut_tivit> MP_PASSWORD=<password> MP_HEADLESS=false MP_ANALISIS_IA=false node agente-mp.js

# Resultado esperado:
# [CICLO] Login exitoso
# [BUSQUEDA] Configurando filtros... Estado=Adjudicada, Radio=Ofertado
# [CICLO] N licitaciones encontradas
# [ADJUNTOS] Acta encontrada: "..." 
# [CICLO] Proceso completado.
```

---

## Escenario 2: Verificar pipeline completo (con API corriendo)

```bash
# Con Docker levantado, ejecutar el scraper apuntando a la API local
cd tools/scraper-mp
MP_RUT=<rut> MP_PASSWORD=<pwd> \
MP_HEADLESS=false \
MP_ANALISIS_IA=true \
API_BASE_URL=http://localhost:5001 \
JWT_SECRET=<mismo_valor_que_env> \
DB_HOST=localhost DB_PORT=5433 DB_NAME=mpm DB_USER=mpm DB_PASSWORD=<pwd> \
node agente-mp.js

# Resultado esperado en la API logs:
docker compose logs api | grep -i "analisis\|workspace\|gemini"
# → "Iniciando análisis background para workspace N"
# → "Análisis completado para workspace N"

# Verificar en la BD:
docker compose exec db psql -U mpm -c "SELECT id, estado FROM analisis_workspaces ORDER BY id DESC LIMIT 5;"
docker compose exec db psql -U mpm -c "SELECT id, analisis_estado, analisis_workspace_id FROM licitaciones_adjuntos WHERE tipo='acta_evaluacion' ORDER BY id DESC LIMIT 5;"
```

---

## Escenario 3: Verificar que el ScraperBackgroundService arranca en Docker

```bash
# Después de docker compose up --build
docker compose logs api | grep -i "scraper"

# Resultado esperado (si SCRAPER_ENABLED=true):
# "ScraperBackgroundService starting. Interval: 12h"
# "Scraper cycle triggered at ..."

# Resultado si SCRAPER_ENABLED=false (default actual):
# "ScraperBackgroundService disabled (SCRAPER_ENABLED=false)"
```

---

## Escenario 4: Verificar en el Frontend que un análisis se muestra

```bash
# 1. Abrir http://localhost:8181
# 2. Login: admin@tivit.cl / test123
# 3. Ir a Análisis → debe aparecer el workspace creado por el scraper
# 4. Abrir el workspace → Dashboard debe mostrar el análisis de Gemini
# 5. Enviar una pregunta al chat: "¿Por qué perdimos?"
```

---

## Troubleshooting

| Síntoma | Causa probable | Solución |
|---|---|---|
| `Error: node not found` en logs API | Dockerfile no tiene Node.js | Reconstruir imagen con `docker compose build --no-cache api` |
| `ScraperScriptPath not found` | Path incorrecto en contenedor | Verificar `Scraper__ScriptPath` apunta a `/app/tools/agente-mp.js` |
| `Login failed` en scraper | Credenciales MP incorrectas o expiradas | Verificar `MP_RUT` y `MP_PASSWORD` en `.env` |
| `API 401` al crear workspace | JWT secret incorrecto | Verificar que `JWT_SECRET` es el mismo en `.env` y en el scraper |
| `acta no encontrada` | Licitación no tiene acta de evaluación | Normal; el scraper registra `sin Acta` y continúa |
| `Gemini timeout` | PDF muy grande o Gemini lento | El análisis puede tomar 10-60s; revisar logs de la API |
| `DB connection refused` desde scraper local | Puerto 5433 bloqueado o Docker no levantado | Verificar con `docker compose ps db` |
