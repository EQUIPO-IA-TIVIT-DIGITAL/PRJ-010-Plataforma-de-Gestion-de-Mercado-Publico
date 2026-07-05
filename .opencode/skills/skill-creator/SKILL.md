---
name: skill-creator
description: 'Creates new AI agent skills following framework conventions. Trigger:
  When asked to create a new skill, add agent instructions, or document patterns for
  AI.'
metadata:
  phase:
  - construction
  - operations
  - inception
  - construction
  - operations
  enforcement: recommended
  depends_on: []
  consumed_by:
  - skill-sync
  agent_roles:
  - orchestrator-agent
  - design-agent
  validation_profile: skill-contract
---

## When to Create a Skill
Create when: pattern used repeatedly / project-specific conventions / complex workflows / decision trees.
Don't create when: documentation already exists / trivial pattern / one-off task.

## Skill Structure
```
.opencode/skills/{skill-name}/
├── SKILL.md              # Required
├── assets/               # Optional - templates, schemas, examples
└── references/           # Optional - links to local docs
```

## SKILL.md Frontmatter Fields
| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Skill identifier (lowercase, hyphens) |
| `description` | Yes | What + Trigger in one block |
| `phase` | Yes | inception / construction / operations / closure |
| `layer` | Yes | frontend / backend / database / e2e / null |
| `enforcement` | Yes | mandatory / recommended / optional |
| `depends_on` | Yes | List of skill names this requires |
| `consumed_by` | Yes | List of skills that use this one |
| `agent_roles` | Yes | Which agents use this skill |
| `validation_profile` | Yes | Profile name or null |
| `mcp_usage` | Yes | MCP tool names or null |

## Naming Conventions
| Type | Pattern | Examples |
|------|---------|----------|
| Generic skill | `{technology}` | `react`, `typescript` |
| Workflow skill | `{action}-{target}` | `skill-creator`, `api-first-spec` |
| Domain skill | `{domain}-{aspect}` | `database-sp`, `database-audit` |
| Agent orchestrator | `agent-{role}` | `agent-backend`, `agent-fullstack` |

## Decision: assets/ vs references/
- Code/SQL/JSON templates → `assets/`
- Links to existing/internal docs → `references/`

**Key Rule:** Keep SKILL.md under 200 lines. Heavy content goes to assets/.

## Content Guidelines
**DO:** Start with Critical Rules / Use tables / Keep examples minimal / Reference assets/
**DON'T:** Duplicate existing docs / Lengthy explanations / Complete templates inline / Exceed 200 lines

## After Creating a Skill
- Register in SKILLS-MANIFEST.md
- Run skill-sync to update manifest tables
- Link in relevant agent .agent.md files if needed
