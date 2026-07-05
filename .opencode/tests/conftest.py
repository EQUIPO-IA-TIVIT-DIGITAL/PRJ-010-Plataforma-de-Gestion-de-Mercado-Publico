import os
import yaml
import pytest


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


@pytest.fixture(scope='session')
def opencode_dir():
    return os.path.normpath(
        os.path.join(os.path.dirname(__file__), '..')
    )


@pytest.fixture(scope='session')
def skills_dir(opencode_dir):
    return os.path.join(opencode_dir, 'skills')


@pytest.fixture(scope='session')
def agents_dir(opencode_dir):
    return os.path.join(opencode_dir, 'agents')


@pytest.fixture(scope='session')
def framework_dir(opencode_dir):
    return os.path.join(opencode_dir, 'framework')


@pytest.fixture(scope='session')
def skill_folders(skills_dir):
    return sorted([
        d for d in os.listdir(skills_dir)
        if os.path.isdir(os.path.join(skills_dir, d))
        and os.path.exists(os.path.join(skills_dir, d, 'SKILL.md'))
    ])


@pytest.fixture(scope='session')
def skill_data(skills_dir):
    data = {}
    for d in sorted(os.listdir(skills_dir)):
        sp = os.path.join(skills_dir, d)
        if not os.path.isdir(sp):
            continue
        sf = os.path.join(sp, 'SKILL.md')
        if not os.path.exists(sf):
            continue
        fm = parse_frontmatter(sf)
        if fm:
            data[d] = fm
    return data
