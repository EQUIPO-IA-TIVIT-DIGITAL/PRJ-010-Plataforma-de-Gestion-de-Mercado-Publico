<#
.SYNOPSIS
  Script de despliegue y gestión para MPM (Mercado Público Manager).
  Soporta entorno local (dev - Docker Compose) y producción en GCP (prod - Cloud Run / Cloud Run Jobs).

.EXAMPLE
  .\scripts\deploy.ps1 dev all
  .\scripts\deploy.ps1 prod api up
  .\scripts\deploy.ps1 prod web up
  .\scripts\deploy.ps1 prod all up
  .\scripts\deploy.ps1 prod all status
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet('dev', 'prod')]
    [string]$Env,

    [Parameter(Position = 1, Mandatory = $false)]
    [ValidateSet('all', 'api', 'web', 'sync-job', 'scraper-job', 'redis')]
    [string]$Scope = 'all',

    [Parameter(Position = 2, Mandatory = $false)]
    [string]$Cmd = 'up'
)

$ErrorActionPreference = 'Stop'

$GCP_PROJECT = if ($env:GCP_PROJECT) { $env:GCP_PROJECT } else { 'tivit-cu010' }
$GCP_REGION = if ($env:GCP_REGION) { $env:GCP_REGION } else { 'us-central1' }
$RUN_API_SERVICE = if ($env:RUN_API_SERVICE) { $env:RUN_API_SERVICE } else { 'mpm-api' }
$RUN_WEB_SERVICE = if ($env:RUN_WEB_SERVICE) { $env:RUN_WEB_SERVICE } else { 'mpm-web' }
$RUN_SERVICE_SA = if ($env:RUN_SERVICE_SA) { $env:RUN_SERVICE_SA } else { "mpm-api-sa@$GCP_PROJECT.iam.gserviceaccount.com" }
$RUN_JOBS_SA = if ($env:RUN_JOBS_SA) { $env:RUN_JOBS_SA } else { "mpm-jobs-sa@$GCP_PROJECT.iam.gserviceaccount.com" }
$VPC_NETWORK = if ($env:VPC_NETWORK) { $env:VPC_NETWORK } else { 'vpc-cu010' }
$VPC_SUBNET = if ($env:VPC_SUBNET) { $env:VPC_SUBNET } else { 'sn-cu010-prd' }
$ARTIFACT_REPO = if ($env:ARTIFACT_REPO) { $env:ARTIFACT_REPO } else { 'mpm' }

$gitCommit = try { (git rev-parse --short HEAD 2>$null).Trim() } catch { 'latest' }
$IMAGE_TAG = if ($env:IMAGE_TAG) { $env:IMAGE_TAG } else { $gitCommit }
$API_IMAGE = "$GCP_REGION-docker.pkg.dev/$GCP_PROJECT/$ARTIFACT_REPO/mpm-api:$IMAGE_TAG"
$WEB_IMAGE = "$GCP_REGION-docker.pkg.dev/$GCP_PROJECT/$ARTIFACT_REPO/mpm-web:$IMAGE_TAG"

$REDIS_HOST = if ($env:REDIS_HOST) { $env:REDIS_HOST } else { '172.31.85.91' }
$REDIS_PORT = if ($env:REDIS_PORT) { $env:REDIS_PORT } else { '6379' }
$GCS_BUCKET = if ($env:GCS_BUCKET) { $env:GCS_BUCKET } else { 'tivit-cu010-mpm-adjuntos' }
$TELEGRAM_BOT_USERNAME = if ($env:TELEGRAM_BOT_USERNAME) { $env:TELEGRAM_BOT_USERNAME } else { 'CU010_bot' }

function Run-Dev {
    param([string]$targetScope, [string]$command)
    $service = if ($targetScope -eq 'all') { '' } else { $targetScope }

    switch ($command) {
        'up'      { docker compose up --build -d $service }
        'down'    { docker compose down $service }
        'restart' { docker compose restart $service }
        'logs'    { docker compose logs -f $service }
        'status'  { docker compose ps }
        'build'   { docker compose build $service }
        default   { Write-Error "Comando desconocido: $command" }
    }
}

function Build-And-Push {
    param(
        [string]$dockerfile,
        [string]$image,
        [string]$context
    )
    Write-Host "→ Build + push de imagen: $image" -ForegroundColor Cyan
    $tempConfig = [System.IO.Path]::GetTempFileName() + '.yaml'
    $yamlContent = "steps:`n  - name: 'gcr.io/cloud-builders/docker'`n    args: ['build', '-f', '$dockerfile', '-t', '$image', '.']`nimages: ['$image']`n"
    Set-Content -Path $tempConfig -Value $yamlContent -Encoding UTF8
    try {
        gcloud builds submit --project=$GCP_PROJECT --config=$tempConfig $context
    }
    finally {
        if (Test-Path $tempConfig) { Remove-Item $tempConfig -Force }
    }
}

function Get-CommonAppEnvVars {
    $corsOrigins = if ($env:CORS_ALLOWED_ORIGINS) { $env:CORS_ALLOWED_ORIGINS } else { 'https://mpm-web-6nnd6y6owa-uc.a.run.app,https://mpm-web-1082413868062.us-central1.run.app,http://localhost:3000,http://localhost:8181' }
    $smtpHost = if ($env:SMTP_HOST) { $env:SMTP_HOST } else { 'smtp-relay.brevo.com' }
    $smtpPort = if ($env:SMTP_PORT) { $env:SMTP_PORT } else { '587' }
    $smtpUser = if ($env:SMTP_USERNAME) { $env:SMTP_USERNAME } else { '66dcf8001@smtp-brevo.com' }
    $smtpFromEmail = if ($env:SMTP_FROM_EMAIL) { $env:SMTP_FROM_EMAIL } else { 'alertas@31032005.xyz' }
    $smtpFromName = if ($env:SMTP_FROM_NAME) { $env:SMTP_FROM_NAME } else { 'TIVIT Mercado Publico' }
    $smtpEnableSsl = if ($env:SMTP_ENABLE_SSL) { $env:SMTP_ENABLE_SSL } else { 'true' }

    return "ASPNETCORE_URLS=http://+:80##ConnectionStrings__Redis=${REDIS_HOST}:${REDIS_PORT}##Storage__Provider=gcs##Storage__Bucket=${GCS_BUCKET}##GOOGLE_CLOUD_PROJECT=${GCP_PROJECT}##Vertex__Region=${GCP_REGION}##JWT__Issuer=TIVIT.MPM##JWT__Audience=MPM.Users##Cors__AllowedOrigins=${corsOrigins}##Telegram__BotUsername=${TELEGRAM_BOT_USERNAME}##Smtp__Host=${smtpHost}##Smtp__Port=${smtpPort}##Smtp__Username=${smtpUser}##Smtp__FromEmail=${smtpFromEmail}##Smtp__FromName=${smtpFromName}##Smtp__EnableSsl=${smtpEnableSsl}##DB_SSL=true##Scraper__CompetidorMercadoScriptPath=/app/tools/competidor-mercado.js##Extraccion__ScriptDescargaPath=/app/tools/descargar-documentos.js"
}

function Get-CommonAppSecrets {
    $secrets = 'JWT__Secret=jwt-secret:latest,MP_TICKET=mp-ticket:latest,MP_RUT=mp-rut:latest,MP_PASSWORD=mp-password:latest,ConnectionStrings__PostgreSQL=postgresql-connection-string:latest,DB_HOST=db-host:latest,DB_PORT=db-port:latest,DB_NAME=db-name:latest,DB_USER=db-user:latest,DB_PASSWORD=db-password:latest'
    
    $hasTelegramToken = try { (gcloud secrets describe 'telegram-bot-token' --project=$GCP_PROJECT 2>$null) -ne $null } catch { $false }
    if ($hasTelegramToken) { $secrets += ',Telegram__BotToken=telegram-bot-token:latest' }

    $hasTelegramWebhook = try { (gcloud secrets describe 'telegram-webhook-secret' --project=$GCP_PROJECT 2>$null) -ne $null } catch { $false }
    if ($hasTelegramWebhook) { $secrets += ',Telegram__WebhookSecret=telegram-webhook-secret:latest' }

    $hasSmtpPass = try { (gcloud secrets describe 'smtp-password' --project=$GCP_PROJECT 2>$null) -ne $null } catch { $false }
    if ($hasSmtpPass) { $secrets += ',Smtp__Password=smtp-password:latest' }

    return $secrets
}

function Deploy-Api {
    Build-And-Push 'src/MPM.Api/Dockerfile' $API_IMAGE '.'
    Write-Host "→ gcloud run deploy $RUN_API_SERVICE" -ForegroundColor Green
    $envVars = '^##^RUN_INPROCESS_WORKERS=false##' + (Get-CommonAppEnvVars)
    $secrets = Get-CommonAppSecrets

    & gcloud run deploy $RUN_API_SERVICE `
        --project=$GCP_PROJECT `
        --region=$GCP_REGION `
        --image=$API_IMAGE `
        --service-account=$RUN_SERVICE_SA `
        --network=$VPC_NETWORK `
        --subnet=$VPC_SUBNET `
        --vpc-egress=private-ranges-only `
        --min-instances=1 `
        --no-cpu-throttling `
        --allow-unauthenticated `
        --port=80 `
        --startup-probe="tcpSocket.port=80,timeoutSeconds=60,periodSeconds=240,failureThreshold=10" `
        "--set-env-vars=$envVars" `
        "--set-secrets=$secrets"
}

function Deploy-Web {
    $apiUrl = (gcloud run services describe $RUN_API_SERVICE --project=$GCP_PROJECT --region=$GCP_REGION --format='value(status.url)' 2>$null)
    if (-not $apiUrl) {
        Write-Error "No se encontró el servicio $RUN_API_SERVICE. Despliégalo primero: .\scripts\deploy.ps1 prod api up"
    }
    Write-Host "→ API URL detectada: $apiUrl" -ForegroundColor Cyan

    Build-And-Push 'Dockerfile' $WEB_IMAGE 'src/mpm-web'
    Write-Host "→ gcloud run deploy $RUN_WEB_SERVICE" -ForegroundColor Green
    & gcloud run deploy $RUN_WEB_SERVICE `
        --project=$GCP_PROJECT `
        --region=$GCP_REGION `
        --image=$WEB_IMAGE `
        --allow-unauthenticated `
        "--set-env-vars=API_URL=$apiUrl"
}

function Deploy-Job {
    param([string]$jobName, [string]$workerMode)
    Build-And-Push 'src/MPM.Api/Dockerfile' $API_IMAGE '.'
    $apiUrl = (gcloud run services describe $RUN_API_SERVICE --project=$GCP_PROJECT --region=$GCP_REGION --format='value(status.url)' 2>$null)
    if (-not $apiUrl) {
        Write-Error "No se encontró el servicio $RUN_API_SERVICE. Despliégalo primero: .\scripts\deploy.ps1 prod api up"
    }

    $memory = if ($jobName -eq 'scraper-job') { '2Gi' } else { '512Mi' }
    $cpu = if ($jobName -eq 'scraper-job') { '2' } else { '1' }

    Write-Host "→ gcloud run jobs deploy $jobName (WORKER_MODE=$workerMode, memory=$memory, cpu=$cpu)" -ForegroundColor Green
    $envVars = "^##^WORKER_MODE=$workerMode##API_BASE_URL=$apiUrl##" + (Get-CommonAppEnvVars)
    $secrets = Get-CommonAppSecrets

    & gcloud run jobs deploy $jobName `
        --project=$GCP_PROJECT `
        --region=$GCP_REGION `
        --image=$API_IMAGE `
        --service-account=$RUN_JOBS_SA `
        --network=$VPC_NETWORK `
        --subnet=$VPC_SUBNET `
        --vpc-egress=private-ranges-only `
        --memory=$memory `
        --cpu=$cpu `
        "--set-env-vars=$envVars" `
        "--set-secrets=$secrets" `
        --max-retries=1 `
        --task-timeout='60m'
}

if ($Env -eq 'dev') {
    Run-Dev -targetScope $Scope -command $Cmd
}
elseif ($Env -eq 'prod') {
    if ($Scope -eq 'all') {
        if ($Cmd -eq 'status') {
            Write-Host '=== SERVICIOS CLOUD RUN ===' -ForegroundColor Cyan
            gcloud run services list --project=$GCP_PROJECT --region=$GCP_REGION
            Write-Host "`n=== JOBS CLOUD RUN ===" -ForegroundColor Cyan
            gcloud run jobs list --project=$GCP_PROJECT --region=$GCP_REGION
        }
        elseif ($Cmd -eq 'up') {
            Deploy-Api
            Deploy-Web
            Deploy-Job -jobName 'sync-job' -workerMode 'sync'
            Deploy-Job -jobName 'scraper-job' -workerMode 'scraper'
            Write-Host '✓ Despliegue completo a producción finalizado exitosamente.' -ForegroundColor Green
        }
        else {
            Write-Error "Comando '$Cmd' no soportado para scope 'all'"
        }
    }
    elseif ($Scope -eq 'api') {
        if ($Cmd -eq 'up') { Deploy-Api }
        elseif ($Cmd -eq 'logs') { gcloud run services logs read $RUN_API_SERVICE --project=$GCP_PROJECT --region=$GCP_REGION --limit=100 }
        elseif ($Cmd -eq 'status') { gcloud run services describe $RUN_API_SERVICE --project=$GCP_PROJECT --region=$GCP_REGION }
        else { Write-Error "Comando desconocido: $Cmd" }
    }
    elseif ($Scope -eq 'web') {
        if ($Cmd -eq 'up') { Deploy-Web }
        elseif ($Cmd -eq 'logs') { gcloud run services logs read $RUN_WEB_SERVICE --project=$GCP_PROJECT --region=$GCP_REGION --limit=100 }
        elseif ($Cmd -eq 'status') { gcloud run services describe $RUN_WEB_SERVICE --project=$GCP_PROJECT --region=$GCP_REGION }
        else { Write-Error "Comando desconocido: $Cmd" }
    }
    elseif ($Scope -eq 'sync-job') {
        if ($Cmd -eq 'up') { Deploy-Job -jobName 'sync-job' -workerMode 'sync' }
        elseif ($Cmd -eq 'execute') { gcloud run jobs execute 'sync-job' --project=$GCP_PROJECT --region=$GCP_REGION --wait }
        elseif ($Cmd -eq 'logs') { gcloud run jobs executions list --job 'sync-job' --project=$GCP_PROJECT --region=$GCP_REGION --limit=5 }
        elseif ($Cmd -eq 'status') { gcloud run jobs describe 'sync-job' --project=$GCP_PROJECT --region=$GCP_REGION }
        else { Write-Error "Comando desconocido: $Cmd" }
    }
    elseif ($Scope -eq 'scraper-job') {
        if ($Cmd -eq 'up') { Deploy-Job -jobName 'scraper-job' -workerMode 'scraper' }
        elseif ($Cmd -eq 'execute') { gcloud run jobs execute 'scraper-job' --project=$GCP_PROJECT --region=$GCP_REGION --wait }
        elseif ($Cmd -eq 'logs') { gcloud run jobs executions list --job 'scraper-job' --project=$GCP_PROJECT --region=$GCP_REGION --limit=5 }
        elseif ($Cmd -eq 'status') { gcloud run jobs describe 'scraper-job' --project=$GCP_PROJECT --region=$GCP_REGION }
        else { Write-Error "Comando desconocido: $Cmd" }
    }
}
