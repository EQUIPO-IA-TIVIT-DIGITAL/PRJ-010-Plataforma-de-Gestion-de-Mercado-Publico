#!/usr/bin/env python3
"""
check-skill-contract.py

For each SKILL.md validates:
  - name matches folder name
  - description is present and > 20 characters
  - metadata.phase uses only valid values
  - metadata.enforcement is one of: mandatory, recommended, optional
  - metadata.validation_profile is one of the 7 valid profiles

Exit code: 0 = all checks pass, 1 = failures found.
"""

import sys
import os
import yaml

VALID_PHASES = {
    'governance', 'discovery', 'conception', 'architecture', 'platform',
    'scaffold', 'inception', 'construction', 'quality', 'operations',
    'closure',
}

VALID_ENFORCEMENTS = {'mandatory', 'recommended', 'optional'}

VALID_PROFILES = {
    'documentation', 'skill-contract', 'architecture-consistency',
    'security-review', 'tenant-isolation', 'release-gate', 'governance-review',
}


def _skills_dir():
    return os.path.normpath(
        os.path.join(os.path.dirname(__file__), '..', 'skills')
    )


def parse_frontmatter(filepath):
    with open(filepath, 'r') as f:
        content = f.read()
    parts = content.split('---', 2)
    if len(parts) < 3:
        return None
    try:
        return yaml.safe_load(parts[1])
    except yaml.YAMLError:
        return None


def get_field(fm, field, default=None):
    if isinstance(fm.get('metadata'), dict) and field in fm['metadata']:
        return fm['metadata'][field]
    if field in fm:
        return fm[field]
    return default


def validate_profiles(profile_value):
    if isinstance(profile_value, str):
        return [p.strip() for p in profile_value.split(',')]
    if isinstance(profile_value, list):
        return profile_value
    return [str(profile_value)]


def main():
    skills_dir = _skills_dir()
    errors = []

    for entry in sorted(os.listdir(skills_dir)):
        skill_path = os.path.join(skills_dir, entry)
        if not os.path.isdir(skill_path):
            continue
        skill_file = os.path.join(skill_path, 'SKILL.md')
        if not os.path.exists(skill_file):
            continue

        fm = parse_frontmatter(skill_file)
        if fm is None:
            errors.append(f"  {skill_file}: no valid YAML frontmatter")
            continue

        # name matches folder
        name = fm.get('name', '')
        if name != entry:
            errors.append(
                f"  {skill_file}: name '{name}' != folder '{entry}'"
            )

        # description > 20 chars
        desc = fm.get('description', '')
        if not desc or len(desc) <= 20:
            errors.append(
                f"  {skill_file}: description too short "
                f"({len(desc)} chars, need > 20)"
            )

        # phase
        phase = get_field(fm, 'phase')
        if phase is None:
            errors.append(f"  {skill_file}: missing phase")
        else:
            phases = phase if isinstance(phase, list) else [phase]
            for p in phases:
                if p not in VALID_PHASES:
                    errors.append(
                        f"  {skill_file}: invalid phase '{p}'"
                    )

        # enforcement
        enforcement = get_field(fm, 'enforcement')
        if enforcement is None:
            errors.append(f"  {skill_file}: missing enforcement")
        elif enforcement not in VALID_ENFORCEMENTS:
            errors.append(
                f"  {skill_file}: invalid enforcement '{enforcement}'"
            )

        # validation_profile
        profile = get_field(fm, 'validation_profile')
        if profile is not None:
            for p in validate_profiles(profile):
                if p not in VALID_PROFILES:
                    errors.append(
                        f"  {skill_file}: invalid "
                        f"validation_profile '{p}'"
                    )

    if errors:
        print("SKILL CONTRACT CHECKS FAILED:")
        for e in errors:
            print(e)
        sys.exit(1)

    print("All skill contract checks passed.")
    sys.exit(0)


if __name__ == '__main__':
    main()
