---
name: agent-qa
description: 'Meta-skill: activates all testing skills in sequence for QA and quality
  assurance work. Trigger: When creating test plans, writing tests, or reviewing quality.'
metadata:
  phase: quality
  layer:
  - backend
  - frontend
  enforcement: recommended
  depends_on:
  - playwright
  - api-first-testing
  consumed_by: []
  agent_roles:
  - delivery-agent
  - control-agent
---

## Purpose
Meta-skill for QA workflows. Activates testing-related skills and guides test creation.

## QA Workflow

| Step | Skill | Artifacts |
|------|-------|-----------|
| 1 | `api-first-spec` | Feature spec (for test derivation) |
| 2 | `api-first-testing` | E2E test plan from spec |
| 3 | `playwright` | Playwright tests |
| 4 | `code-review` | Test code review |
| 5 | `performance` | Performance test considerations |

## Test Types

| Type | Tool | When |
|------|------|------|
| Unit | Jest / Vitest / JUnit / pytest | Individual functions |
| Integration | Supertest / RestAssured / httpx | API contract testing |
| E2E | Playwright | Full user flows |
| Performance | k6 / JMeter / Locust | Load testing |
| Accessibility | Axe + Playwright | A11y compliance |

## Test Planning Checklist
- [ ] Happy path test for each endpoint
- [ ] Validation error tests (400)
- [ ] Auth/authz tests (401/403)
- [ ] Not found tests (404)
- [ ] Pagination tests
- [ ] Export tests (if applicable)
- [ ] Edge cases from business rules

## Quality Gates
| Gate | Threshold |
|------|-----------|
| Unit test coverage | ≥ 80% |
| Critical flows E2E | 100% |
| Zero known security issues | Mandatory |
| Zero accessibility (WCAG AA) critical | Mandatory |

## Test Data Management

| Need | Approach |
|------|----------|
| Isolated test data | Use `beforeEach` to seed per-test data, never share state between tests |
| Realistic data volumes | For performance tests, use production-like data volumes in staging |
| Authentication in tests | Use API tokens or test user fixtures — never real credentials |
| Cleanup | Always clean up test data after run (DB transactions rollback, API resource teardown) |

## CI Integration Notes

- **Playwright tests**: Run in `--shard` mode for parallel execution. Configure in `playwright.config.ts`.
- **API tests**: Run before E2E. They catch contract breaks faster.
- **Coverage reports**: Fail CI if coverage drops below threshold. Enforce in pipeline config.
- **Flaky test handling**: Tag flaky tests with `@flaky`, set retries to 2 in Playwright config.

## Common Pitfalls

- **Testing implementation, not behavior**: Test what the user sees and does, not internal functions (those are unit tests).
- **Hardcoded waits**: Use `waitFor` selectors, not `setTimeout`. Hardcoded waits are fragile and slow.
- **Test pollution**: One test modifying shared state breaks others. Isolate via `beforeEach` + fresh data.
- **Skipping a11y**: Accessibility is not optional. Run `@axe-core/playwright` on every page.
- **No error scenarios**: Only testing the happy path misses 90% of real bugs. Always test validation errors, auth failures, and empty states.
