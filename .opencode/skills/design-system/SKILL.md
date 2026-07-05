---
name: design-system
description: 'Visual design system: colors, typography, spacing, component wrappers,
  and theming. Trigger: When styling components, choosing colors, or applying visual
  patterns.'
metadata:
  phase:
  - inception
  - construction
  layer:
  - frontend
  enforcement: mandatory
  depends_on: []
  consumed_by:
  - react
  - agent-frontend
  - agent-fullstack
  agent_roles:
  - design-agent
  validation_profile: skill-contract
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Use CSS Modules | ALWAYS | Scoped styles, no conflicts |
| Use design tokens (not raw values) | ALWAYS | Consistency |
| Use design system icons | ALWAYS | Visual consistency |
| Hardcode colors outside tokens | NEVER | Maintainability |
| Use component wrappers, not UI library directly | ALWAYS | Consistent project-level styling |
| Wrap new UI library components following wrapper pattern | ALWAYS | Design consistency |

## Color Tokens

### Primary Colors
```css
--color-primary: #007788;
--color-primary-hover: #5b8def;
--color-link: #1890ff;
```

### Semantic Colors
```css
--color-success-bg: #f6ffed; --color-success: #52c41a;
--color-warning-bg: #fff7e6; --color-warning: #fa8c16;
--color-error-bg: #fff2f0;   --color-error: #ff4d4f;
--color-info-bg: #e6f7ff;    --color-info: #1890ff;
```

### Text Colors (Grays)
```css
--color-text-title: #262626;
--color-text-primary: #4a5568;
--color-text-secondary: #595959;
--color-text-muted: #8c8c8c;
--color-text-disabled: #bfbfbf;
```

### UI Colors (Grays)
```css
--color-bg-page: #f5f5f5;
--color-bg-card: #ffffff;
--color-border: #d9d9d9;
--color-border-light: #e8e8e8;
```

### Status Badge Colors
| Status | Background | Text |
|--------|------------|------|
| Draft | `#e5e7eb` | `#374151` |
| Pending | `#fef3c7` | `#92400e` |
| In Progress | `#dbeafe` | `#1e40af` |
| Approved | `#d1fae5` | `#065f46` |
| Rejected | `#fee2e2` | `#991b1b` |

## Typography
| Role | Size | Weight |
|------|------|--------|
| H1 | 24px | 600 |
| H2 | 20px | 600 |
| H3 | 16px | 600 |
| Body | 14px | 400 |
| Small | 13px | 400 |
| Caption | 12px | 400 |

## Spacing (Base 4px)
| Token | Value |
|-------|-------|
| `xs` | 4px |
| `sm` | 8px |
| `md` | 12px |
| `lg` | 16px |
| `xl` | 24px |

## Border Radius
| Context | Value |
|---------|-------|
| Badges / Tags | 4px |
| Cards / Inputs | 8px |
| Avatars / Chips | 50% |

## Shadows
| Context | Value |
|---------|-------|
| Card | `0 1px 2px rgba(0,0,0,0.03)` |
| Hover | `0 2px 8px rgba(0,0,0,0.08)` |
| Elevated | `0 4px 12px rgba(0,0,0,0.15)` |

## Layout Constants
```css
--header-height: 56px;
--sidebar-width: 220px;
--sidebar-collapsed-width: 80px;
```

## Component Wrapper Pattern (React)
```tsx
// Wrap UI library components for consistent project styling
const ProjectButton = forwardRef<HTMLButtonElement, ButtonProps>((props, ref) => (
  <AntdButton ref={ref} size="large" {...props} />
));
ProjectButton.displayName = 'ProjectButton';
```

## Theme Configuration (Ant Design 6)
```tsx
<ConfigProvider theme={{ token: { colorPrimary: '#007788' } }}>
  <App />
</ConfigProvider>
```

## Core Component Catalog
Wrappers to create for consistent styling:
- Input, TextArea, Password, Search
- Select, AutoComplete, DatePicker, TimePicker
- Form, FormItem, InputNumber
- Button, Switch, Radio, Checkbox
- Table (generic `<T extends object>`)
- Modal, Drawer
- Card, Tag, Alert, Tabs
- Spin, Avatar, Divider, Space
- Typography (namespace)
- Descriptions, Popconfirm

## CSS Module Pattern
```css
/* {Component}.module.css */
.container { padding: var(--spacing-lg); }
.title { font-size: 20px; font-weight: 600; color: var(--color-text-title); }
```
