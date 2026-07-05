---
name: playwright
description: 'E2E testing with Playwright: Page Object Model, selector strategy, file
  structure, tag categories. Trigger: When creating or updating end-to-end tests,
  or configuring Playwright.'
metadata:
  phase:
  - quality
  layer:
  - frontend
  enforcement: mandatory
  depends_on:
  - api-first-testing
  consumed_by:
  - agent-qa
  agent_roles:
  - delivery-agent
  validation_profile: skill-contract
  mcp_usage: playwright
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Use Playwright MCP if available | ALWAYS | Browser interaction via MCP |
| Use Page Object Model | ALWAYS | Reusability and maintainability |
| Use `data-testid` as primary selector | ALWAYS | Stable under refactoring |
| Avoid CSS selectors that depend on order/position | NEVER | Brittle tests |
| Tag all tests (@smoke/@regression/@critical) | ALWAYS | Selective test runs |
| Test one scenario per test | ALWAYS | Isolated failures |

## Playwright MCP Workflow (MANDATORY when MCP available)
```
1. Start browser via MCP tool
2. Navigate to the page
3. Interact with elements
4. Capture selectors and behavior
5. Generate Page Object Model from real observation
6. Write test based on observed behavior
```

## File Structure
```
tests/
├── e2e/
│   ├── pages/
│   │   └── {Feature}Page.ts       # Page Object Models
│   ├── fixtures/
│   │   └── auth.fixture.ts        # Auth state
│   ├── specs/
│   │   └── {feature}/
│   │       ├── {feature}.create.spec.ts
│   │       ├── {feature}.list.spec.ts
│   │       └── {feature}.edit.spec.ts
│   └── utils/
│       └── test-helpers.ts
├── playwright.config.ts
└── .env.test
```

## Page Object Model Pattern
```typescript
import { Page, Locator } from '@playwright/test';

export class {Feature}Page {
  readonly page: Page;
  readonly createButton: Locator;
  readonly nameInput: Locator;
  readonly submitButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.createButton = page.getByTestId('btn-create');
    this.nameInput = page.getByTestId('input-name');
    this.submitButton = page.getByTestId('btn-submit');
  }

  async navigate() {
    await this.page.goto('/entities');
  }

  async create(data: { name: string }) {
    await this.createButton.click();
    await this.nameInput.fill(data.name);
    await this.submitButton.click();
  }
}
```

## Selector Priority
| Priority | Selector | Example |
|----------|----------|---------|
| 1st | `data-testid` | `getByTestId('btn-create')` |
| 2nd | ARIA role + name | `getByRole('button', { name: 'Create' })` |
| 3rd | Visible text | `getByText('Create Entity')` |
| 4th | Label | `getByLabel('Name')` |
| AVOID | CSS classes | `.btn-primary:nth-child(2)` |

## Test Tag Categories
| Tag | Purpose | Run frequency |
|-----|---------|---------------|
| `@smoke` | Critical happy path | On every commit |
| `@regression` | Full feature coverage | On every PR |
| `@critical` | Business-critical flows | On every deploy |
| `@slow` | Long-running tests | Nightly |
| `@manual` | Requires manual trigger | On demand |

## Test Structure
```typescript
import { test, expect } from '@playwright/test';
import { {Feature}Page } from '../pages/{Feature}Page';

test.describe('{Feature} @regression', () => {
  test('should create a new {entity} @smoke', async ({ page }) => {
    const featurePage = new {Feature}Page(page);
    await featurePage.navigate();

    await featurePage.create({ name: 'Test Entity' });

    await expect(page.getByText('Created successfully')).toBeVisible();
  });
});
```

## playwright.config.ts
```typescript
import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: './tests/e2e/specs',
  testMatch: '**/*.spec.ts',
  timeout: 30000,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 4 : undefined,
  reporter: [['html', { outputFolder: 'playwright-report' }]],
  use: {
    baseURL: process.env.BASE_URL || 'http://localhost:3000',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
});
```

## Auth Fixture (Session Reuse)
```typescript
// auth.fixture.ts — store auth state once, reuse across tests
import { test as base } from '@playwright/test';
export const test = base.extend({
  page: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: './tests/e2e/.auth/user.json',
    });
    const page = await context.newPage();
    await use(page);
  },
});
```
