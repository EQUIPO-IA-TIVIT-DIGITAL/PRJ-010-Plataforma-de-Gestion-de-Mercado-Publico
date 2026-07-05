# Validators

Validadores deterministas para el framework agéntico. Cada script es
autónomo y puede ejecutarse individualmente.

## Validators

| Script | Propósito | Exit code |
|--------|-----------|-----------|
| `check-dependencies.py` | Verifica que todas las dependencias entre skills (`depends_on`, `consumed_by`) sean válidas, existan como directorios y no formen ciclos | 0 = pass |
| `check-refs.py` | Verifica que todos los enlaces markdown en `.opencode/` apunten a archivos existentes y no usen rutas antiguas (`.github/`, `.vscode/`) | 0 = pass |
| `check-secrets.py` | Escanea `.md` y `.json` en busca de patrones de credenciales (sk-, ghp_, token, password, secret, api_key). Nivel: alerta (no bloquea) | Siempre 0 |
| `check-skill-contract.py` | Valida que cada `SKILL.md` cumpla el contrato: name match, description > 20 chars, phase, enforcement, validation_profile | 0 = pass |

## How to Run

```bash
# Run a single validator
python3 .opencode/validators/check-dependencies.py

# Run all validators
bash .opencode/validators/run-all.sh
```

## Pre-commit Integration

Add to `.pre-commit-config.yaml`:

```yaml
- repo: local
  hooks:
    - id: check-dependencies
      name: Check skill dependencies
      entry: python3 .opencode/validators/check-dependencies.py
      language: system
      pass_filenames: false
    - id: check-refs
      name: Check markdown references
      entry: python3 .opencode/validators/check-refs.py
      language: system
      pass_filenames: false
    - id: check-secrets
      name: Check for secrets in workspace
      entry: python3 .opencode/validators/check-secrets.py
      language: system
      pass_filenames: false
    - id: check-skill-contract
      name: Check skill contract
      entry: python3 .opencode/validators/check-skill-contract.py
      language: system
      pass_filenames: false
```

Or use a single hook that runs all:

```yaml
- repo: local
  hooks:
    - id: run-all-validators
      name: Run all framework validators
      entry: bash .opencode/validators/run-all.sh
      language: system
      pass_filenames: false
```
