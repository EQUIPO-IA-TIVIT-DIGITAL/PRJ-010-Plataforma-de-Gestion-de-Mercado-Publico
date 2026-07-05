#!/usr/bin/env python3
"""
check-dependencies.py

Reads all SKILL.md files from .opencode/skills/, parses YAML frontmatter, and:
  - Verifies every skill in depends_on exists as a skill directory
  - Verifies every skill in consumed_by exists
  - Detects circular dependencies
  - Ensures depends_on skills have enforcement: mandatory or recommended

Exit code: 0 = all checks pass, 1 = failures found.
"""

import sys
import os
import yaml

VALID_ENFORCEMENTS = {'mandatory', 'recommended'}


def _skill_dir_path():
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


def build_skill_map(skills_dir):
    skill_map = {}
    parse_errors = []

    for entry in sorted(os.listdir(skills_dir)):
        skill_path = os.path.join(skills_dir, entry)
        if not os.path.isdir(skill_path):
            continue
        skill_file = os.path.join(skill_path, 'SKILL.md')
        if not os.path.exists(skill_file):
            continue

        fm = parse_frontmatter(skill_file)
        if fm is None:
            parse_errors.append(f"  {skill_file}: no valid YAML frontmatter")
            continue

        name = fm.get('name', entry)
        depends_on = get_field(fm, 'depends_on', []) or []
        consumed_by = get_field(fm, 'consumed_by', []) or []
        enforcement = get_field(fm, 'enforcement', '')

        skill_map[name] = {
            'dir': entry,
            'depends_on': depends_on,
            'consumed_by': consumed_by,
            'enforcement': enforcement,
        }

    return skill_map, parse_errors


def check_existence(skill_map):
    errors = []
    all_names = set(skill_map.keys())
    for name, info in skill_map.items():
        for dep in info['depends_on']:
            if dep not in all_names:
                errors.append(f"  {name}: depends_on '{dep}' not found")
        for cons in info['consumed_by']:
            if cons not in all_names:
                errors.append(f"  {name}: consumed_by '{cons}' not found")
    return errors


def check_circular(skill_map):
    errors = []
    visited = set()
    rec_stack = set()
    path = []

    def dfs(node):
        if node in rec_stack:
            cycle = path[path.index(node):] + [node]
            errors.append(f"  Circular dependency: {' -> '.join(cycle)}")
            return
        if node in visited:
            return
        if node not in skill_map:
            return

        visited.add(node)
        rec_stack.add(node)
        path.append(node)

        for dep in skill_map[node].get('depends_on', []):
            dfs(dep)

        path.pop()
        rec_stack.discard(node)

    for name in skill_map:
        if name not in visited:
            dfs(name)

    return errors


def check_enforcement(skill_map):
    errors = []
    for name, info in skill_map.items():
        for dep in info['depends_on']:
            if dep in skill_map:
                dep_enf = skill_map[dep]['enforcement']
                if dep_enf not in VALID_ENFORCEMENTS:
                    errors.append(
                        f"  {name}: depends_on '{dep}' has enforcement "
                        f"'{dep_enf}' (must be mandatory or recommended)"
                    )
    return errors


def main():
    skills_dir = _skill_dir_path()
    skill_map, parse_errors = build_skill_map(skills_dir)

    all_errors = list(parse_errors)
    all_errors.extend(check_existence(skill_map))
    all_errors.extend(check_circular(skill_map))
    all_errors.extend(check_enforcement(skill_map))

    if all_errors:
        print("DEPENDENCY CHECKS FAILED:")
        for e in all_errors:
            print(e)
        sys.exit(1)

    print("All dependency checks passed.")
    sys.exit(0)


if __name__ == '__main__':
    main()
