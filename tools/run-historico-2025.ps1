# ============================================================
# run-historico-2025.ps1
# Ejecuta el scraper de Mercado Público sobre TODAS las
# licitaciones adjudicadas desde 01-01-2025 en las que
# TIVIT participó, con análisis Gemini activado.
#
# PREREQUISITOS:
#   - Docker stack levantado: docker compose up -d
#   - .env en el root del proyecto con MP_RUT, MP_PASSWORD,
#     JWT_SECRET, JWT_ISSUER, JWT_AUDIENCE, GEMINI_API_KEY
#
# TIEMPO ESTIMADO: 4–10 horas (ver estimación al final)
# COSTO ESTIMADO:  $3–$9 USD en Gemini API
#
# USO:
#   cd "C:\Users\menca\Desktop\CU010 - Mercado Público"
#   .\tools\run-historico-2025.ps1
#
# Para dry-run (solo scraping, sin análisis IA):
#   .\tools\run-historico-2025.ps1 -SinIA
#
# Para reanudar después de una interrupción:
#   .\tools\run-historico-2025.ps1 -Reanudar
#   (El scraper es incremental — retoma desde el último sync completado)
# ============================================================

param(
    [switch]$SinIA,
    [switch]$Reanudar,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  MPM — Scraper Histórico 2025" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# ── Cargar .env ──────────────────────────────────────────────
$EnvFile = Join-Path $RepoRoot ".env"
if (-not (Test-Path $EnvFile)) {
    Write-Host "[ERROR] No se encontró .env en: $RepoRoot" -ForegroundColor Red
    Write-Host "        Crea el archivo copiando .env.example y rellenando las variables." -ForegroundColor Red
    exit 1
}

Write-Host "[1/4] Cargando variables desde .env..." -ForegroundColor Yellow

$envVars = @{}
Get-Content $EnvFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith('#') -and $line -match '^([^=]+)=(.*)$') {
        $key   = $Matches[1].Trim()
        $value = $Matches[2].Trim().Trim('"').Trim("'")
        $envVars[$key] = $value
    }
}

# ── Validar variables críticas ────────────────────────────────
$required = @('MP_RUT', 'MP_PASSWORD', 'JWT_SECRET', 'JWT_ISSUER', 'JWT_AUDIENCE', 'GEMINI_API_KEY')
$missing  = @()
foreach ($var in $required) {
    if (-not $envVars.ContainsKey($var) -or [string]::IsNullOrWhiteSpace($envVars[$var])) {
        $missing += $var
    }
}

if ($missing.Count -gt 0) {
    Write-Host "[ERROR] Faltan variables en .env:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "        - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "        Variables OK: MP_RUT, JWT, GEMINI_API_KEY cargadas." -ForegroundColor Green

# ── Verificar Docker stack ───────────────────────────────────
Write-Host "[2/4] Verificando que el stack Docker está levantado..." -ForegroundColor Yellow

$apiRunning = docker compose -f (Join-Path $RepoRoot "docker-compose.yml") ps --format json 2>$null |
    ConvertFrom-Json |
    Where-Object { $_.Service -eq 'api' -and $_.State -eq 'running' }

if (-not $apiRunning) {
    Write-Host "[ERROR] El contenedor 'api' no está corriendo." -ForegroundColor Red
    Write-Host "        Ejecuta: docker compose up -d" -ForegroundColor Red
    exit 1
}

Write-Host "        Stack OK — contenedor api corriendo." -ForegroundColor Green

# ── Parámetros de esta corrida ────────────────────────────────
$FechaDesde  = "01-01-2025"
$AnalisisIA  = if ($SinIA)  { "false" } else { "true" }
$ModoLog     = if ($Reanudar) { "REANUDACION" } else { "INICIAL" }

if ($DryRun) {
    Write-Host ""
    Write-Host "[DRY RUN] Los siguientes comandos se ejecutarían:" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "  docker compose exec -e MP_RUT=*** -e MP_PASSWORD=*** \" -ForegroundColor White
    Write-Host "    -e MP_FECHA_DESDE=$FechaDesde \" -ForegroundColor White
    Write-Host "    -e MP_ANALISIS_IA=$AnalisisIA \" -ForegroundColor White
    Write-Host "    -e MP_HEADLESS=true \" -ForegroundColor White
    Write-Host "    -e API_BASE_URL=http://localhost:80 \" -ForegroundColor White
    Write-Host "    -e JWT_SECRET=*** \" -ForegroundColor White
    Write-Host "    api node /app/tools/agente-mp.js" -ForegroundColor White
    Write-Host ""
    Write-Host "[DRY RUN] Fin. Ejecuta sin -DryRun para proceder." -ForegroundColor Magenta
    exit 0
}

# ── Confirmación del usuario ──────────────────────────────────
Write-Host ""
Write-Host "[3/4] Configuración de esta corrida:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Fecha desde   : $FechaDesde" -ForegroundColor White
Write-Host "  Fecha hasta   : $(Get-Date -Format 'dd-MM-yyyy')" -ForegroundColor White
Write-Host "  Análisis IA   : $AnalisisIA" -ForegroundColor White
Write-Host "  Modo          : $ModoLog" -ForegroundColor White
Write-Host "  Script        : /app/tools/agente-mp.js (dentro del contenedor api)" -ForegroundColor White
Write-Host "  API interna   : http://localhost:80" -ForegroundColor White
Write-Host ""
Write-Host "  Tiempo estimado : 4–10 horas" -ForegroundColor DarkYellow
Write-Host "  Costo estimado  : \$3–\$9 USD (Gemini 2.5 Pro)" -ForegroundColor DarkYellow
Write-Host ""

$confirm = Read-Host "¿Proceder? (s/n)"
if ($confirm -notmatch '^[sS]$') {
    Write-Host "Cancelado." -ForegroundColor Gray
    exit 0
}

# ── Ejecutar ──────────────────────────────────────────────────
Write-Host ""
Write-Host "[4/4] Iniciando scraper histórico..." -ForegroundColor Yellow
Write-Host "      Para monitorear en otra terminal:" -ForegroundColor Gray
Write-Host "      docker compose logs -f api | grep -iE 'SCRAPER|CICLO|ANALISIS|GEMINI|ERROR'" -ForegroundColor Gray
Write-Host ""
Write-Host "  Inicio: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date

docker compose -f (Join-Path $RepoRoot "docker-compose.yml") exec `
    -e "MP_RUT=$($envVars['MP_RUT'])" `
    -e "MP_PASSWORD=$($envVars['MP_PASSWORD'])" `
    -e "MP_FECHA_DESDE=$FechaDesde" `
    -e "MP_ANALISIS_IA=$AnalisisIA" `
    -e "MP_HEADLESS=true" `
    -e "MP_MAX_REINTENTOS=3" `
    -e "MP_DELAY_MS=2000" `
    -e "API_BASE_URL=http://localhost:80" `
    -e "JWT_SECRET=$($envVars['JWT_SECRET'])" `
    -e "JWT_ISSUER=$($envVars.ContainsKey('JWT_ISSUER') ? $envVars['JWT_ISSUER'] : 'TIVIT.MPM')" `
    -e "JWT_AUDIENCE=$($envVars.ContainsKey('JWT_AUDIENCE') ? $envVars['JWT_AUDIENCE'] : 'MPM.Users')" `
    -e "DB_HOST=db" `
    -e "DB_PORT=5432" `
    -e "DB_NAME=$($envVars.ContainsKey('DB_NAME') ? $envVars['DB_NAME'] : 'mpm')" `
    -e "DB_USER=$($envVars.ContainsKey('DB_USER') ? $envVars['DB_USER'] : 'mpm')" `
    -e "DB_PASSWORD=$($envVars.ContainsKey('DB_PASSWORD') ? $envVars['DB_PASSWORD'] : '')" `
    -e "GEMINI_API_KEY=$($envVars['GEMINI_API_KEY'])" `
    api node /app/tools/agente-mp.js

$elapsed = (Get-Date) - $startTime
Write-Host ""
Write-Host "  Fin: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  (duración: $([math]::Round($elapsed.TotalMinutes, 1)) min)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Para verificar los resultados:" -ForegroundColor Yellow
Write-Host '  docker compose exec db psql -U mpm -c "SELECT COUNT(*), estado FROM analisis_workspaces GROUP BY estado;"' -ForegroundColor White
Write-Host '  docker compose exec db psql -U mpm -c "SELECT COUNT(*), analisis_estado FROM licitaciones_adjuntos WHERE tipo='"'"'acta_evaluacion'"'"' GROUP BY analisis_estado;"' -ForegroundColor White
