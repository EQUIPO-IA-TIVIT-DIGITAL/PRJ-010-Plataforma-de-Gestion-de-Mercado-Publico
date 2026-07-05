---
name: skill-sync
description: 'Syncs skill metadata to SKILLS-MANIFEST.md after creating or modifying
  skills. Trigger: After creating/modifying a skill, regenerating manifest tables.'
metadata:
  phase:
  - operations
  enforcement: recommended
  depends_on:
  - skill-creator
  consumed_by: []
  agent_roles:
  - orchestrator-agent
  validation_profile: skill-contract
---

## Purpose
Keeps SKILLS-MANIFEST.md in sync with individual skill metadata.

## Required Skill Metadata
```yaml
name: skill-name
description: >
  What + Trigger
phase: [construction]
layer: [backend]
enforcement: mandatory
depends_on: [other-skill]
consumed_by: [consuming-skill]
agent_roles: [design-agent]
validation_profile: conventions-lint
mcp_usage: null
```

## What to Sync
1. Read all `.opencode/skills/*/SKILL.md` files
2. Extract frontmatter fields
3. Update SKILLS-MANIFEST.md:
   - Skills catalog table
   - Agent-to-skill map
   - Auto-invoke keywords table
4. Verify no duplicate skill names

## Checklist After Modifying Skills
- [ ] Added/updated all frontmatter fields in new/modified skill
- [ ] SKILLS-MANIFEST.md updated with new entries
- [ ] Verified no duplicate names
- [ ] Agent .agent.md files updated if skill roles changed
- [ ] README.md updated if new skill category added

## Sync Rules
| Rule | Rationale |
|------|-----------|
| Never remove skills from manifest without confirmation | Skills may be referenced |
| `enforcement: mandatory` skills must appear in all relevant agent files | Governance requirement |
| Skill `name` must match folder name exactly | Consistency |
