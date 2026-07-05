import os
import re

import yaml

from conftest import parse_frontmatter, get_field

VALID_PHASES = {
    'governance', 'discovery', 'conception', 'architecture', 'platform',
    'scaffold', 'inception', 'construction', 'quality', 'operations',
    'closure',
}


def _manifest_path(opencode_dir):
    return os.path.join(opencode_dir, 'framework', 'SKILLS-MANIFEST.md')


def _agent_model_path(opencode_dir):
    return os.path.join(opencode_dir, 'framework', 'AGENT-MODEL.md')


# ── Test 1: every SKILL.md has valid YAML frontmatter ──

def test_all_skills_have_valid_yaml(skills_dir):
    bad = []
    for d in sorted(os.listdir(skills_dir)):
        sp = os.path.join(skills_dir, d)
        if not os.path.isdir(sp):
            continue
        sf = os.path.join(sp, 'SKILL.md')
        if not os.path.exists(sf):
            continue
        if parse_frontmatter(sf) is None:
            bad.append(sf)
    assert not bad, f"Skills with invalid YAML frontmatter:\n" + "\n".join(bad)


# ── Test 2: no circular dependencies in depends_on chains ──

def test_no_circular_dependencies(skill_data):
    deps = {}
    for name, fm in skill_data.items():
        deps[name] = get_field(fm, 'depends_on', []) or []

    cycles = []
    visited = set()
    rec_stack = set()
    path = []

    def dfs(node):
        if node in rec_stack:
            cycle = path[path.index(node):] + [node]
            cycles.append(' -> '.join(cycle))
            return
        if node in visited or node not in deps:
            return
        visited.add(node)
        rec_stack.add(node)
        path.append(node)
        for dep in deps[node]:
            dfs(dep)
        path.pop()
        rec_stack.discard(node)

    for skill in deps:
        if skill not in visited:
            dfs(skill)

    assert not cycles, f"Circular dependencies detected:\n" + "\n".join(cycles)


# ── Test 3: every skill folder has entry in SKILLS-MANIFEST.md ──

def test_all_skills_in_manifest(opencode_dir, skill_data):
    manifest_path = _manifest_path(opencode_dir)
    with open(manifest_path, 'r') as f:
        content = f.read()

    missing = []
    for name in skill_data:
        if f'`{name}`' not in content:
            missing.append(name)
    assert not missing, (
        f"Skills not found in SKILLS-MANIFEST.md:\n" + "\n".join(missing)
    )


# ── Test 4: every skill mentioned in depends_on / consumed_by exists ──

def test_all_referenced_skills_exist(skill_data):
    all_names = set(skill_data.keys())
    missing = []
    for name, fm in skill_data.items():
        for ref in (get_field(fm, 'depends_on', []) or []):
            if ref not in all_names:
                missing.append(f"{name}.depends_on: '{ref}'")
        for ref in (get_field(fm, 'consumed_by', []) or []):
            if ref not in all_names:
                missing.append(f"{name}.consumed_by: '{ref}'")
    assert not missing, (
        f"Referenced skills not found:\n" + "\n".join(missing)
    )


# ── Test 5: each .agent.md file has name in frontmatter ──

def test_agent_files_have_name(agents_dir):
    bad = []
    for fname in sorted(os.listdir(agents_dir)):
        if not fname.endswith('.agent.md'):
            continue
        fp = os.path.join(agents_dir, fname)
        fm = parse_frontmatter(fp)
        if fm is None or not fm.get('name'):
            bad.append(fname)
    assert not bad, f"Agent files missing 'name':\n" + "\n".join(bad)


# ── Test 6: no .github/ or .vscode/ references in any file ──

def test_no_old_path_references(opencode_dir):
    bad = []
    for root, dirs, files in os.walk(opencode_dir):
        dirs[:] = [d for d in dirs if not d.startswith('.')]
        for fname in files:
            if not fname.endswith('.md'):
                continue
            fp = os.path.join(root, fname)
            with open(fp, 'r') as f:
                content = f.read()
            links = re.findall(r'\]\(([^)]+)\)', content)
            for link in links:
                if '.github/' in link or '.vscode/' in link:
                    bad.append((fp, link))
    assert not bad, (
        "Old path references found:\n" +
        "\n".join(f"  {fp}: {link}" for fp, link in bad)
    )


# ── Test 7: all phase values are from the valid set ──

def test_valid_phase_values(skill_data):
    bad = []
    for name, fm in skill_data.items():
        phase = get_field(fm, 'phase')
        if phase is None:
            bad.append(f"{name}: missing phase")
            continue
        phases = phase if isinstance(phase, list) else [phase]
        for p in phases:
            if p not in VALID_PHASES:
                bad.append(f"{name}: invalid phase '{p}'")
    assert not bad, f"Invalid phase values:\n" + "\n".join(bad)


# ── Test 8: all skills use the same frontmatter structure (metadata wrapper) ──

def test_consistent_frontmatter(skills_dir, skill_data):
    bad = []
    expected_root = {'name', 'description', 'metadata'}
    for d in sorted(os.listdir(skills_dir)):
        sp = os.path.join(skills_dir, d)
        if not os.path.isdir(sp):
            continue
        sf = os.path.join(sp, 'SKILL.md')
        if not os.path.exists(sf):
            continue
        fm = parse_frontmatter(sf)
        if fm is None:
            continue
        unexpected = set(fm.keys()) - expected_root
        if unexpected:
            bad.append(f"{d}: unexpected root keys {unexpected}")
        if 'metadata' in fm and not isinstance(fm['metadata'], dict):
            bad.append(f"{d}: metadata is not a dict")
    assert not bad, f"Frontmatter inconsistencies:\n" + "\n".join(bad)
