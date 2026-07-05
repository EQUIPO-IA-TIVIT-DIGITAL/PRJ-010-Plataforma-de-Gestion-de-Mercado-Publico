---
name: pull-request
description: 'Creates Pull Requests following conventional commits conventions. Trigger:
  When creating PRs (PR template, title conventions, changelog).'
metadata:
  phase:
  - construction
  enforcement: mandatory
  depends_on:
  - changelog
  - code-review
  consumed_by: []
  agent_roles:
  - delivery-agent
  validation_profile: documentation
---

## PR Creation Process
1. Analyze changes: `git diff main...HEAD`
2. Determine affected components: Backend, Frontend, Database
3. Fill template sections
4. Create PR with your Git provider's CLI or UI

## PR Template Structure
```markdown
## Descripción
## Tipo de cambio
## Componentes afectados
## Issue relacionado
## Checklist
## Screenshots (if UI changes)
## Notas adicionales
```

## Title Conventions (Conventional Commits)
Format: `type(scope): description`

### Types
| Type | Usage |
|------|-------|
| `feat` | New functionality |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Formatting |
| `refactor` | Refactoring without behavior change |
| `perf` | Performance improvement |
| `test` | Tests |
| `chore` | Maintenance, dependencies |

### Scopes
| Scope | Usage |
|-------|-------|
| `api` | Backend |
| `ui` | Frontend |
| `db` | Database |
| `auth` | Authentication |
| `authz` | Authorization |
| `infra` | Infrastructure |

### Examples
```
feat(api): add user profile endpoint
fix(ui): resolve button alignment in header
refactor(db): optimize user query performance
chore: update dependencies
```

## Before Creating PR
1. All tests pass locally
2. Linting passes
3. CHANGELOG.md updated (if applicable)
4. Branch is up to date with main
5. Commits are clean and descriptive
6. No console.log or debug code left
7. Code review checklist completed
