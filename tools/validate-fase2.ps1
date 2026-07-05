#Requires -Version 5.1
<#
.SYNOPSIS
    Validacion automatizada de Fase 2 - MPM CU010
.DESCRIPTION
    Verifica el pipeline completo: Docker, API, Auth, licitaciones, actas, analisis Gemini.
    Genera un log legible con estado PASS/FAIL/WARN/SKIP por cada check.
.PARAMETER ApiUrl
    URL base de la API (default: http://localhost:5001)
.PARAMETER SkipScraper
    Omitir el smoke-test del scraper aunque haya credenciales disponibles
.EXAMPLE
    .\validate-fase2.ps1
    .\validate-fase2.ps1 -SkipScraper
#>
param(
    [string]$ApiUrl = "http://localhost:5001",
    [switch]$SkipScraper
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "SilentlyContinue"

$AUTH_EMAIL    = "admin@tivit.cl"
$AUTH_PASSWORD = "test123"
$CONTAINER_API = "mpm-api"
$CONTAINER_DB  = "mpm-db"
$DB_USER       = "mpm"
$DB_NAME       = "mpm"

$script:Token   = $null
$script:Results = New-Object System.Collections.Generic.List[hashtable]
$script:StartAt = Get-Date

# ── Helpers ───────────────────────────────────────────────────────────────────
function Write-Section([string]$title) {
    $sep = "-" * 70
    Write-Host ""
    Write-Host $sep -ForegroundColor DarkGray
    Write-Host "  $title" -ForegroundColor Cyan
    Write-Host $sep -ForegroundColor DarkGray
}

function Write-Check {
    param([string]$Status, [string]$Name, [string]$Detail = "")
    $ts = (Get-Date).ToString("HH:mm:ss")

    switch ($Status) {
        "PASS" { $icon = "[PASS]"; $color = "Green"   }
        "FAIL" { $icon = "[FAIL]"; $color = "Red"     }
        "WARN" { $icon = "[WARN]"; $color = "Yellow"  }
        "SKIP" { $icon = "[SKIP]"; $color = "DarkGray"}
        "INFO" { $icon = "[INFO]"; $color = "Cyan"    }
        default{ $icon = "[????]"; $color = "White"   }
    }

    $pad = " " * [Math]::Max(0, 44 - $Name.Length)
    Write-Host -NoNewline "[$ts] " -ForegroundColor DarkGray
    Write-Host -NoNewline "$icon " -ForegroundColor $color
    Write-Host -NoNewline "$Name$pad" -ForegroundColor White
    if ($Detail) {
        Write-Host "-- $Detail" -ForegroundColor DarkGray
    } else {
        Write-Host ""
    }

    $script:Results.Add(@{ Status = $Status; Name = $Name; Detail = $Detail; Time = $ts })
}

function Invoke-Api {
    param([string]$Method, [string]$Path, [hashtable]$Body = @{}, [string]$AuthToken = "")
    $uri     = "$ApiUrl$Path"
    $headers = @{ "Content-Type" = "application/json" }
    if ($AuthToken) { $headers["Authorization"] = "Bearer $AuthToken" }
    try {
        $params = @{ Uri = $uri; Method = $Method; Headers = $headers; TimeoutSec = 10 }
        if ($Method -ne "GET" -and $Body.Count -gt 0) {
            $params["Body"] = ($Body | ConvertTo-Json -Depth 5)
        }
        $resp = Invoke-RestMethod @params
        return @{ Ok = $true; Data = $resp }
    } catch {
        $msg = $_.Exception.Message
        return @{ Ok = $false; Error = $msg }
    }
}

function Invoke-DbQuery([string]$Sql) {
    $out = docker exec $CONTAINER_DB psql -U $DB_USER -d $DB_NAME -t -c $Sql 2>&1
    return $out
}

function Get-FirstInt([object]$raw) {
    $str = "$raw"
    $m = [System.Text.RegularExpressions.Regex]::Match($str, '\d+')
    if ($m.Success) { return [int]$m.Value } else { return 0 }
}

# =============================================================================
# SECCION 1 - INFRAESTRUCTURA DOCKER
# =============================================================================
Write-Section "SECCION 1 - Infraestructura Docker"

foreach ($c in @("mpm-api","mpm-db","mpm-redis","mpm-web")) {
    $state = docker inspect --format="{{.State.Status}}" $c 2>$null
    if ($state -eq "running") {
        Write-Check "PASS" "Container $c" "running"
    } else {
        Write-Check "FAIL" "Container $c" "estado: $state (esperado: running)"
    }
}

$nodeVer = docker exec $CONTAINER_API node --version 2>&1
if ("$nodeVer" -match "v\d+\.\d+") {
    Write-Check "PASS" "Node.js en container API" "$nodeVer".Trim()
} else {
    Write-Check "FAIL" "Node.js en container API" "no encontrado -- reconstruir con --build"
}

docker exec $CONTAINER_API test -f /app/tools/agente-mp.js 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Check "PASS" "Scraper script /app/tools/agente-mp.js" "presente"
} else {
    Write-Check "FAIL" "Scraper script /app/tools/agente-mp.js" "no encontrado -- verificar Dockerfile COPY"
}

docker exec $CONTAINER_API test -d /app/tools/node_modules 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Check "PASS" "Scraper node_modules" "instalados"
} else {
    Write-Check "FAIL" "Scraper node_modules" "ausentes -- npm ci no corrio en build"
}

# =============================================================================
# SECCION 2 - API Y AUTENTICACION
# =============================================================================
Write-Section "SECCION 2 - API y Autenticacion"

$health = Invoke-Api "GET" "/health"
if ($health.Ok) {
    Write-Check "PASS" "API health check" "$ApiUrl/health responde"
} else {
    Write-Check "FAIL" "API health check" "$($health.Error)"
}

$loginResult = Invoke-Api "POST" "/api/v1/auth/login" @{ email = $AUTH_EMAIL; password = $AUTH_PASSWORD }
if ($loginResult.Ok -and $loginResult.Data.success) {
    $script:Token = $loginResult.Data.data.token
    $roles = $loginResult.Data.data.user.roles -join ", "
    Write-Check "PASS" "Login ($AUTH_EMAIL)" "rol: $roles"
} else {
    Write-Check "FAIL" "Login ($AUTH_EMAIL)" "$($loginResult.Error)"
}

# =============================================================================
# SECCION 3 - LICITACIONES (T017)
# =============================================================================
Write-Section "SECCION 3 - Licitaciones (T017)"

if (-not $script:Token) {
    Write-Check "SKIP" "Licitaciones API" "sin token (login fallo)"
    Write-Check "SKIP" "Actas en BD" "sin token"
    Write-Check "SKIP" "Actas con analisis completado" "sin token"
} else {
    $lics = Invoke-Api "GET" "/api/v1/licitaciones?page=1&pageSize=5" -AuthToken $script:Token
    if ($lics.Ok) {
        $totalPag = 0
        if ($lics.Data.pagination -ne $null) { $totalPag = $lics.Data.pagination.totalItems }
        if ($totalPag -eq 0 -and $lics.Data.data -ne $null) { $totalPag = $lics.Data.data.Count }
        if ($totalPag -gt 0) {
            Write-Check "PASS" "GET /licitaciones" "$totalPag licitacion(es) en BD"
        } else {
            Write-Check "WARN" "GET /licitaciones" "0 licitaciones -- scraper no ha corrido aun"
        }
    } else {
        Write-Check "FAIL" "GET /licitaciones" "$($lics.Error)"
    }

    $actasRaw   = Invoke-DbQuery "SELECT COUNT(*) FROM licitaciones_adjuntos WHERE tipo='acta_evaluacion';"
    $actasCount = Get-FirstInt $actasRaw
    if ($actasCount -gt 0) {
        Write-Check "PASS" "Actas en licitaciones_adjuntos" "$actasCount acta(s) registrada(s)"
    } else {
        Write-Check "WARN" "Actas en licitaciones_adjuntos" "0 registros -- ejecutar scraper primero (T015)"
    }

    $compRaw   = Invoke-DbQuery "SELECT COUNT(*) FROM licitaciones_adjuntos WHERE tipo='acta_evaluacion' AND analisis_estado='completado';"
    $compCount = Get-FirstInt $compRaw
    if ($compCount -gt 0) {
        Write-Check "PASS" "Actas con analisis=completado" "$compCount analizada(s) por Gemini"
    } else {
        Write-Check "WARN" "Actas con analisis=completado" "0 -- activar MP_ANALISIS_IA=true en el scraper"
    }
}

# =============================================================================
# SECCION 4 - ANALISIS WORKSPACES (T016)
# =============================================================================
Write-Section "SECCION 4 - Analisis Workspaces (T016)"

if (-not $script:Token) {
    Write-Check "SKIP" "Workspaces API" "sin token"
} else {
    $ws = Invoke-Api "GET" "/api/v1/analisis/workspaces" -AuthToken $script:Token
    if ($ws.Ok) {
        $wsCount = 0
        if ($ws.Data.data -ne $null) { $wsCount = $ws.Data.data.Count }
        if ($wsCount -gt 0) {
            Write-Check "PASS" "GET /analisis/workspaces" "$wsCount workspace(s) en BD"
        } else {
            Write-Check "WARN" "GET /analisis/workspaces" "0 workspaces -- ejecutar scraper con MP_ANALISIS_IA=true"
        }
    } else {
        Write-Check "FAIL" "GET /analisis/workspaces" "$($ws.Error)"
    }

    $wsCompRaw   = Invoke-DbQuery "SELECT COUNT(*) FROM analisis_workspaces WHERE estado='completado';"
    $wsCompCount = Get-FirstInt $wsCompRaw
    if ($wsCompCount -gt 0) {
        Write-Check "PASS" "Workspaces estado=completado" "$wsCompCount workspace(s) con analisis listo"
        $wsIdRaw = Invoke-DbQuery "SELECT id FROM analisis_workspaces WHERE estado='completado' ORDER BY id DESC LIMIT 1;"
        $wsId    = Get-FirstInt $wsIdRaw
        if ($wsId -gt 0) {
            $wsDetail = Invoke-Api "GET" "/api/v1/analisis/workspaces/$wsId" -AuthToken $script:Token
            if ($wsDetail.Ok -and $wsDetail.Data.data -ne $null) {
                Write-Check "PASS" "Dashboard workspace #$wsId" "detalle disponible en API"
            } else {
                Write-Check "WARN" "Dashboard workspace #$wsId" "workspace existe pero API no retorna detalle"
            }
        }
    } else {
        Write-Check "WARN" "Workspaces estado=completado" "0 -- pipeline Gemini no ha completado aun"
    }
}

# =============================================================================
# SECCION 5 - SCRAPER SYNC LOG
# =============================================================================
Write-Section "SECCION 5 - Scraper Sync Log"

$syncRaw   = Invoke-DbQuery "SELECT COUNT(*) FROM scraper_sync_log;"
$syncCount = Get-FirstInt $syncRaw
if ($syncCount -gt 0) {
    Write-Check "PASS" "scraper_sync_log" "$syncCount ejecucion(es) registradas"
    $lastRaw = Invoke-DbQuery "SELECT estado||' | lics:'||total_licitaciones||' | actas:'||total_con_acta||' | fecha:'||ejecutado_en::date FROM scraper_sync_log ORDER BY id DESC LIMIT 1;"
    $lastStr = ("$lastRaw" -split "`n" | Where-Object { $_ -match '\|' } | Select-Object -First 1)
    if ($lastStr) { Write-Check "INFO" "Ultima ejecucion" $lastStr.Trim() }
} else {
    Write-Check "WARN" "scraper_sync_log" "0 registros -- scraper nunca ha corrido en este contenedor"
}

# =============================================================================
# SECCION 6 - SCRAPER SMOKE TEST (T014/T015)
# =============================================================================
Write-Section "SECCION 6 - Scraper Smoke Test (T014/T015)"

$mpRut = $env:MP_RUT
$mpPwd = $env:MP_PASSWORD

if ($SkipScraper) {
    Write-Check "SKIP" "Scraper smoke test" "-SkipScraper activado"
} elseif (-not $mpRut -or -not $mpPwd) {
    Write-Check "SKIP" "Scraper smoke test" "MP_RUT / MP_PASSWORD no definidos en entorno"
    Write-Check "INFO" "Para activar" '$env:MP_RUT="rut"; $env:MP_PASSWORD="pwd"; .\validate-fase2.ps1'
} else {
    Write-Check "INFO" "Credenciales MP detectadas" "RUT: $mpRut"
    Write-Check "INFO" "Iniciando run incremental (MP_ANALISIS_IA=false, timeout 2 min)..." ""

    $scraperDir = Join-Path $PSScriptRoot "scraper-mp"
    $outFile    = Join-Path $env:TEMP "mpm-scraper-out.txt"
    $errFile    = Join-Path $env:TEMP "mpm-scraper-err.txt"

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName  = "node"
    $psi.Arguments = "agente-mp.js --incremental"
    $psi.WorkingDirectory = $scraperDir
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.EnvironmentVariables["MP_RUT"]        = $mpRut
    $psi.EnvironmentVariables["MP_PASSWORD"]   = $mpPwd
    $psi.EnvironmentVariables["MP_HEADLESS"]   = "true"
    $psi.EnvironmentVariables["MP_ANALISIS_IA"]= "false"
    $psi.EnvironmentVariables["API_BASE_URL"]  = $ApiUrl

    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    $null = $proc.Start()
    $out = $proc.StandardOutput.ReadToEnd()
    $proc.WaitForExit(120000) | Out-Null

    if ($out -match "\[LOGIN\] Login completado") {
        Write-Check "PASS" "T014 -- Login en Mercado Publico" "autenticacion exitosa"
    } else {
        Write-Check "FAIL" "T014 -- Login en Mercado Publico" "verificar MP_RUT/MP_PASSWORD"
    }

    $busqMatch = [System.Text.RegularExpressions.Regex]::Match($out, '\[BUSQUEDA\] (\d+) licitaciones')
    if ($busqMatch.Success) {
        Write-Check "PASS" "T014 -- Busqueda filtro Ofertado" "$($busqMatch.Groups[1].Value) licitaciones encontradas"
    } else {
        Write-Check "WARN" "T014 -- Busqueda filtro Ofertado" "sin resultado de busqueda en output"
    }

    if ($out -match "\[CICLO\] Proceso completado") {
        Write-Check "PASS" "T014 -- Ciclo completado" "exit: $($proc.ExitCode)"
    } else {
        Write-Check "WARN" "T014 -- Ciclo completado" "ciclo no finalizo (timeout o error)"
    }
}

# =============================================================================
# RESUMEN
# =============================================================================
$elapsed = [Math]::Round(((Get-Date) - $script:StartAt).TotalSeconds, 1)
$pass = ($script:Results | Where-Object { $_.Status -eq "PASS" }).Count
$fail = ($script:Results | Where-Object { $_.Status -eq "FAIL" }).Count
$warn = ($script:Results | Where-Object { $_.Status -eq "WARN" }).Count
$skip = ($script:Results | Where-Object { $_.Status -eq "SKIP" }).Count

$sep = "=" * 70
Write-Host ""
Write-Host $sep -ForegroundColor DarkGray
Write-Host "  RESUMEN -- Fase 2 Validation  ($elapsed s)" -ForegroundColor White
Write-Host $sep -ForegroundColor DarkGray
Write-Host "  PASS: $pass   FAIL: $fail   WARN: $warn   SKIP: $skip" -ForegroundColor White

if ($fail -gt 0) {
    Write-Host ""
    Write-Host "  FALLOS:" -ForegroundColor Red
    $script:Results | Where-Object { $_.Status -eq "FAIL" } | ForEach-Object {
        Write-Host "    [$($_.Time)] $($_.Name)" -ForegroundColor Red
        if ($_.Detail) { Write-Host "             $($_.Detail)" -ForegroundColor DarkGray }
    }
}

if ($warn -gt 0) {
    Write-Host ""
    Write-Host "  ADVERTENCIAS (se resuelven al correr el scraper con MP_ANALISIS_IA=true):" -ForegroundColor Yellow
    $script:Results | Where-Object { $_.Status -eq "WARN" } | ForEach-Object {
        Write-Host "    [$($_.Time)] $($_.Name)" -ForegroundColor Yellow
        if ($_.Detail) { Write-Host "             $($_.Detail)" -ForegroundColor DarkGray }
    }
}

Write-Host ""
if ($fail -eq 0 -and $warn -eq 0) {
    Write-Host "  >> TODAS LAS VALIDACIONES PASARON -- pipeline listo para demo" -ForegroundColor Green
} elseif ($fail -eq 0) {
    Write-Host "  >> Infraestructura OK -- warnings desaparecen despues de correr el scraper" -ForegroundColor Yellow
} else {
    Write-Host "  >> HAY FALLOS -- revisar items en rojo antes de la demo" -ForegroundColor Red
}
Write-Host $sep -ForegroundColor DarkGray
Write-Host ""

exit $fail
