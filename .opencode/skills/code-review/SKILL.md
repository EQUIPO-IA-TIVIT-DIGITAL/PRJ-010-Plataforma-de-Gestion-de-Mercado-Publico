---
name: code-review
description: 'Code review checklist before creating PRs. Applies to any backend (.NET,
  Java, Python), frontend (React, Angular, Vue), or database layer. Trigger: Before
  committing code, creating PRs, or when asked to review.'
metadata:
  phase:
  - construction
  enforcement: mandatory
  depends_on: []
  consumed_by:
  - pull-request
  agent_roles:
  - control-agent
  - delivery-agent
  validation_profile: skill-contract
---

## Critical Rules

| Rule | Type | Rationale |
|------|------|-----------|
| Run this checklist before EVERY PR | ALWAYS | Catch issues early |
| Fix all blockers before commit | ALWAYS | Don't merge broken code |
| Document skipped checks with reason | ALWAYS | Transparency |

## Quick Checklist

### Blockers (MUST fix)
- Build passes / No type errors / No linter errors / Tests pass
- No secrets in code / No console.log or debug output / No commented-out code

### 🟡 Warnings (Should fix)
- No loosely typed variables (`any`, `Object`, `dynamic` without reason) / No magic numbers / No duplicate code
- Error handling / Loading states / Empty states

### Best Practices
- Meaningful names / Small functions / Consistent patterns with the rest of the codebase

## Layer-Specific Checks

### Database (Stored Procedures / Queries)
| Check | Look For |
|-------|----------|
| Error format | Standardized error code + message + field |
| Transaction | BEGIN TRY/CATCH or equivalent with ROLLBACK |
| Parameters | All inputs are parameters, no string concatenation in queries |
| Pagination | List queries have page/pageSize parameters |

### Backend (.NET / Java / Python / Node.js)
| Check | Look For |
|-------|----------|
| Validation | Input validation on commands/requests |
| Exception mapping | DB/external errors → typed domain exceptions |
| Async | `await` on all async calls (or equivalent) |
| Dispose | Proper cleanup of connections, streams |
| Logging | No sensitive data in logs |

### Frontend (React / Angular / Vue)
| Check | Look For |
|-------|----------|
| Types | No `any` / no implicit any, proper interfaces |
| Keys | Unique `key` prop on list items |
| Dependencies | Correct effect dependencies |
| Cleanup | Cancel subscriptions/effects on unmount |
| Accessibility | Labels, ARIA attributes |

## Common Issues to Catch

| Issue | Example | Fix |
|-------|---------|-----|
| Missing error handling | `await api.post()` without try/catch | Add error handling |
| Hardcoded values | `if (status === 1)` | Use constants/enums |
| Missing loading state | Button doesn't show loading | Add loading indicator |
| N+1 queries | Loop with DB call inside | Batch or join |
| Memory leak | Timer/subscription without cleanup | Add cleanup in effect |
