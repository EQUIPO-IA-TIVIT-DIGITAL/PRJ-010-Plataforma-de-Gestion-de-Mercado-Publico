---
name: html-prototype
description: 'Generates interactive HTML mockups that look like the final app using
  HTML + CSS + minimal JS. Trigger: After Requirements Analysis when stakeholder approval
  of screens is needed before API design.'
metadata:
  phase:
  - inception
  enforcement: optional
  depends_on:
  - hu-template
  consumed_by:
  - api-first-spec
  agent_roles:
  - design-agent
  validation_profile: documentation
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Use a single shared CSS file | ALWAYS | Single source of truth |
| Copy CSS + JS into output folder | ALWAYS | Portable, offline bundle |
| Zero external CDN references | NEVER | Must work offline |
| Zero inline styles | NEVER | Use CSS classes only |
| One HTML file per screen | ALWAYS | Keeps review granular |
| Labels and field names in project language | ALWAYS | Match user expectations |
| English for CSS class names and data-testid | ALWAYS | Code identifiers stay in English |
| Include `data-testid` on interactive elements | ALWAYS | QA team uses them in E2E tests |

## Interactivity API (data-* attributes)

| Attribute | Element | Behavior |
|-----------|---------|----------|
| `data-navigate="./page.html"` | button, a | Navigates to URL |
| `data-toggle="#modalId"` | button | Toggles visibility on target |
| `data-dismiss="modal"` | button | Hides closest modal overlay |
| `data-tab="name"` | button, a | Activates tab within tab group |
| `data-tab-panel="name"` | div | Shown when matching tab is active |
| `data-tab-group` | div | Container for tabs + panels |
| `data-state-select` | select | Drives visibility of state panels |
| `data-show-state="DRAFT"` | any | Visible only when state matches |
| `data-validate-error` | input | Shows field error styling |

## Output Location
`docs/inception/prototypes/{screen-name}.html`
Naming convention: `kebab-case` matching the feature.

## Page Types
1. **List Page** — paginated table with search and filters
2. **Form Page (Create/Edit)** — form with field rows
3. **Detail Page (View)** — read-only with status-dependent action bar
4. **Dashboard Page** — KPIs and summary stats
5. **Calendar/Schedule Page** — date-based grid
6. **Wizard Page (Multi-step)** — multi-step modal or page
7. **Dual-Section Page** — two independent data sections
8. **Report Page (Tabbed)** — tab navigation between reports

## Critical Tab Rules
- Tab panels use `data-tab-panel` WITHOUT the `hidden` attribute
- First panel gets a visible class, others do not
- JS toggles visibility class — `hidden` attribute would override this
- Active tab button gets an `--active` modifier class

## Design Tokens to Use in CSS
- Spacing: base 4px (xs=4, sm=8, md=12, lg=16, xl=24)
- Border radius: sm=4px (badges), md=8px (cards/inputs)
- Text hierarchy: H1→H3 as headings, body/small/caption for content
