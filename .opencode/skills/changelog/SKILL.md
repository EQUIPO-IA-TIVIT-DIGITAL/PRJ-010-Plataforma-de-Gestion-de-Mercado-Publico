---
name: changelog
description: 'Manages changelog entries following keepachangelog.com format. Trigger:
  When creating PRs, adding changelog entries, or working with CHANGELOG.md.'
metadata:
  phase:
  - operations
  - closure
  enforcement: recommended
  depends_on: []
  consumed_by:
  - pull-request
  agent_roles:
  - delivery-agent
  validation_profile: documentation
---

## Changelog Location

Each project should have a `CHANGELOG.md` in the root directory.

## Format Rules (keepachangelog.com)

### Section Order (ALWAYS this order)

```
## [Unreleased]
### Added / ### Changed / ### Deprecated / ### Removed / ### Fixed / ### Security
```

### Emoji Prefixes (REQUIRED)

| Section    | Emoji | Usage |
|------------|-------|-------|
| Added      | `### Added` | New features, endpoints, components |
| Changed    | `### Changed` | Modifications to existing functionality |
| Deprecated | `### Deprecated` | Features marked for removal |
| Removed    | `### Removed` | Deleted features |
| Fixed      | `### 🐞 Fixed` | Bug fixes |
| Security   | `### Security` | Security patches, CVE fixes |

### Entry Format
- Blank line after section header before first entry
- Blank line between sections
- Be specific: what changed, not why (that's in the PR)
- One entry per PR / one entry per story
- No period at the end
- Do NOT start with redundant verbs

### Semantic Versioning Rules

| Change Type | Version Bump | Example |
|-------------|--------------|---------|
| Bug fixes, patches | PATCH (x.y.**Z**) | 1.0.1 → 1.0.2 |
| New features (backwards compatible) | MINOR (x.**Y**.0) | 1.0.2 → 1.1.0 |
| Breaking changes, removals | MAJOR (**X**.0.0) | 1.1.0 → 2.0.0 |

**CRITICAL:** `### Removed` entries MUST only appear in MAJOR version releases.

**NEVER modify already released versions.** Once a version is released, its changelog section is frozen.

### Bad Entries (examples)
- `Fixed bug.` → Too vague, has period
- `Added new feature for users` → Missing PR link, redundant verb
- `Add search bar [(#123)]` → Redundant verb
