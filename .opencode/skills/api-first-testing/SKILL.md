---name: api-first-testing
description: 'Generate E2E and API tests from OpenAPI spec using Playwright and API
  testing tools. Generates test cases, Page Objects, and assertions from endpoints.
  Trigger: When creating E2E tests from OpenAPI spec, generating test cases.'
metadata:
  phase:
  - inception
  - quality
  - inception
  - construction
  layer:
  - e2e
  enforcement: mandatory
  depends_on:
  - api-first-spec
  consumed_by:
  - agent-qa
  agent_roles:
  - control-agent
  validation_profile: skill-contract---

## Workflow
OpenAPI Spec → Parse → Generate Scenarios → Page Objects → API Tests → E2E Tests

## Test Scenarios by Endpoint Type

| Endpoint Type | Test Case | Type |
|---------------|-----------|------|
| GET /entities (list) | List all / with filter / with search / with pagination | Happy path |
| POST /entities | Valid data | Happy path |
| POST /entities | Missing required fields | Validation (400) |
| POST /entities | Duplicate unique field | Business error (409) |
| GET /entities/{id} | Valid ID | Happy path |
| GET /entities/{id} | Non-existent ID | Not found (404) |
| DELETE /entities/{id} | Valid ID in DRAFT | Happy path |
| DELETE /entities/{id} | Invalid state | Business error (400) |
| POST /entities/{id}/{verb} | Valid state transition | Happy path |
| POST /entities/{id}/{verb} | Wrong source state | Business error (400) |
| POST /sub/{subId}/remove | Valid with justification | Happy path |
| POST /sub/{subId}/remove | Missing justification | Validation (400) |
| PUT /sub/reorder | Valid reorder | Happy path |

## data-testid Convention
| Component | data-testid |
|-----------|-------------|
| List table | `{entity}-table` |
| Create button | `create-{entity}-btn` |
| Edit button | `edit-{entity}-btn` |
| Delete button | `delete-{entity}-btn` |
| Form inputs | `{field}-input` |
| Submit button | `submit-btn` |
| Operation button | `{verb}-{entity}-btn` |
| Justification input | `justification-input` |
| Confirm dialog | `confirm-dialog` |
| Confirm button | `confirm-btn` |

## File Structure
```
tests/
├── api/{feature}.api.spec.ts
├── e2e/{feature}.e2e.spec.ts
├── pages/{Feature}Page.ts
└── fixtures/{feature}.fixtures.ts
```

## Checklist
- [ ] Operation test: valid state transition (should pass)
- [ ] Operation test: wrong source state (should fail)
- [ ] Operation test: missing preconditions (should fail)
- [ ] Remove test: with justification (should pass)
- [ ] Remove test: without justification (should fail)
- [ ] Test setup creates entities in required state (beforeAll)
- [ ] `data-testid` added to all interactive components
- [ ] Page Objects reuse existing base page
