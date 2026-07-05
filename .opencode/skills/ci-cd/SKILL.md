---
name: ci-cd
description: 'CI/CD pipeline design: GitHub Actions, Azure DevOps, stages (lint →
  test → build → deploy), environment strategy, secrets management, artifact versioning,
  deployment gates, rollback. Trigger: When designing or configuring CI/CD pipelines.'
metadata:
  phase:
  - operations
  layer:
  - infrastructure
  enforcement: recommended
  depends_on: []
  consumed_by:
  - agent-backend
  - agent-fullstack
  - agent-qa
  agent_roles:
  - delivery-agent
  validation_profile: architecture-consistency
  mcp_usage: none
---

## Propósito

Diseñar pipelines CI/CD repetibles, seguros y auditable: ejecución por etapas, promoción entre entornos, manejo de secretos, versionado de artefactos, deployment gates y rollback automático.

## Objetivo

1. ¿Cómo se estructura un pipeline CI/CD por etapas?
2. ¿Cómo se gestionan secretos sin exponerlos en logs o artefactos?
3. ¿Cómo se versionan artefactos para trazabilidad?
4. ¿Cómo se implementan deployment gates (manuales y automáticos)?
5. ¿Cómo se diseña una estrategia de rollback?
6. ¿Cómo se promueve un artefacto entre entornos (dev → qa → prod)?

## Relación con otras skills

- `framework-platform` define la infraestructura donde los pipelines despliegan.
- `framework-qa-validation` define los gates que los pipelines ejecutan (lint, tests, security scan).
- `security` define políticas de secretos, firma de artefactos y compliance.
- `docker-local` define cómo se construyen y versionan imágenes de contenedor.

## Qué debe hacer el agente

1. Diseñar pipeline con etapas secuenciales: lint → test → build → security scan → publish → deploy.
2. Configurar secretos desde el proveedor (GitHub Secrets / Azure Key Vault), nunca en código.
3. Versionar artefactos con hash + semver desde git tag o commit SHA.
4. Implementar gates: tests exitosos, code review, security scan pass.
5. Diseñar rollback automático (revertir tag de imagen o reiniciar versión anterior).
6. Promover el mismo artefacto entre entornos, no reconstruir.
7. Bloquear deploys a prod con aprobación manual (protected environment).
8. Registrar evidencia de cada deploy (commit, artefacto, resultado, responsable).

## Alcance

Incluye: pipeline YAML, secretos, artifact registry, environments, gates, rollback, approvals.
No incluye: infraestructura como tal (Terraform/Pulumi), monitoreo post-deploy, SLOs.

## Principios

- El mismo artifact binario que pasa tests en CI es el que se despliega en todos los entornos.
- Los secretos se inyectan en runtime, no se copian en imágenes ni se loguean.
- El pipeline debe fallar rápido (fail fast) en cada etapa.
- Un deploy a producción requiere al menos una aprobación manual.
- El rollback debe ser más rápido que el fix.
- Cada deploy deja un artefacto inmutable identificable por commit SHA.

## Technical Design

### GitHub Actions — Full pipeline

```yaml
name: CI/CD

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet restore && dotnet format --verify-no-changes

  test:
    needs: lint
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet test --configuration Release --logger trx
      - uses: dorny/test-reporter@v1
        if: always()
        with:
          name: Test Results
          path: '**/TestResults/*.trx'
          reporter: dotnet-trx

  build:
    needs: test
    runs-on: ubuntu-latest
    outputs:
      version: ${{ steps.version.outputs.version }}
    steps:
      - uses: actions/checkout@v4
      - id: version
        run: echo "version=$(git rev-parse --short HEAD)" >> $GITHUB_OUTPUT
      - run: dotnet publish -c Release -o ./out
      - uses: actions/upload-artifact@v4
        with:
          name: app-${{ steps.version.outputs.version }}
          path: ./out

  deploy-dev:
    needs: build
    environment: dev
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
        with:
          name: app-${{ needs.build.outputs.version }}
      - run: echo "Deploying ${{ needs.build.outputs.version }} to dev"

  deploy-prod:
    needs: deploy-dev
    if: github.ref == 'refs/heads/main'
    environment: prod
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
        with:
          name: app-${{ needs.build.outputs.version }}
      - run: echo "Deploying ${{ needs.build.outputs.version }} to prod"
```

### Azure DevOps — Multi-stage

```yaml
trigger:
  - main

variables:
  dockerRegistryServiceConnection: 'acr-connection'
  imageRepository: 'myapp'
  tag: '$(Build.BuildId)'

stages:
  - stage: Build
    jobs:
      - job: BuildJob
        steps:
          - task: DotNetCoreCLI@2
            inputs:
              command: publish
              arguments: '-c Release -o $(Build.ArtifactStagingDirectory)'
          - task: PublishBuildArtifacts@1

  - stage: DeployQA
    dependsOn: Build
    condition: succeeded()
    environment: qa
    jobs:
      - deployment: Deploy
        strategy:
          runOnce:
            deploy:
              steps:
                - download: current
                - script: echo Deploying to QA

  - stage: DeployProd
    dependsOn: DeployQA
    condition: succeeded()
    environment: prod
    jobs:
      - deployment: Deploy
        strategy:
          runOnce:
            deploy:
              steps:
                - download: current
                - script: echo Deploying to Prod
```

### Secrets management

```yaml
# GitHub Actions — secrets
steps:
  - name: Deploy to Azure
    uses: azure/webapps-deploy@v3
    with:
      publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
      package: ./out

# NEVER:
#   - echo ${{ secrets.SOMETHING }}  (leaks in log)
#   - storing secrets in artifact files
```

### Rollback strategy

```yaml
# Option A: Revert image tag
steps:
  - run: kubectl set image deployment/myapp app=${{ env.REGISTRY }}/myapp:${{ env.PREVIOUS_VERSION }}

# Option B: Blue-green swap
steps:
  - run: kubectl patch service myapp -p '{"spec":{"selector":{"version":"blue"}}}'
```

## Preguntas guía

- ¿El mismo artefacto se despliega en todos los entornos?
- ¿Los secretos se inyectan en runtime o están en el repositorio?
- ¿Cada entorno tiene sus propias variables de entorno?
- ¿Hay un gate antes de producción (aprobación manual)?
- ¿El rollback se puede ejecutar en menos de 5 minutos?
- ¿Hay evidencia de cada deploy (quién, qué, cuándo)?

## Salidas esperadas

- Pipeline YAML (GitHub Actions o Azure DevOps).
- Definición de entornos (dev, qa, prod) con variables y secretos.
- Estrategia de versionado de artefactos (semver + commit SHA).
- Deployment gates (automáticos + manuales).
- Procedimiento de rollback documentado.

## Criterios de calidad

- El pipeline pasa lint + test + security scan antes de build.
- No hay secretos en ningún archivo del repositorio.
- Cada deploy produce un artefacto inmutable trazable a un commit.
- El rollback está definido y es ejecutable por un solo comando.
- Producción requiere aprobación manual explicita.

## Comportamiento esperado del agente

Cuando un pipeline mezcle lint/test/build/deploy en un solo job, debe separarlos en stages paralelizables.
Cuando no haya environment separation, debe definir dev → qa → prod con sus gates.
Cuando los secretos aparezcan en YAML o scripts, debe moverlos a GitHub Secrets / Azure Key Vault.
Cuando no haya rollback definido, debe proponer al menos una estrategia de revert image tag.

## Plantilla de respuesta

```
1. Pipeline provider (GitHub Actions / Azure DevOps).
2. Stage breakdown (lint → test → build → security → deploy).
3. Environment definitions (dev, qa, prod).
4. Secrets map (env vars per environment).
5. Artifact versioning scheme.
6. Rollback procedure.
```

## Ejemplos

### Ejemplo 1 — Rollback por revert de tag

```bash
# Current: myapp:abc123, Previous: myapp:def456
kubectl set image deployment/myapp app=myapp:def456
```

### Ejemplo 2 — Deployment gate con Azure DevOps

```yaml
# Pre-deployment conditions in Azure portal
- Gates:
  - Evaluate artifact: check test results pass
  - Evaluate artifact: check security scan pass
  - Manual approval: required
```

## Checklist

- [ ] Pipeline organizado en stages secuenciales (fail fast).
- [ ] Mismo artefacto promovido entre entornos (no rebuild).
- [ ] Secretos inyectados desde provider, no en código.
- [ ] Entorno de producción protegido con approval gate.
- [ ] Rollback documentado (revert tag / blue-green).
- [ ] Artifact versionado con commit SHA o build ID.
- [ ] Logs de cada deploy preservados para auditoría.
- [ ] Pruebas, lint y security scan ejecutados en CI.
