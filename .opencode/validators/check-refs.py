#!/usr/bin/env python3
"""
check-refs.py

Checks all markdown files in .opencode/ for:
  - Markdown links pointing to non-existent files
  - Links referencing old paths (.github/, .vscode/)
  - Agent references in AGENT-MODEL.md pointing to existing .agent.md files

Exit code: 0 = all checks pass, 1 = failures found.
"""

import sys
import os
import re


def _opencode_dir():
    return os.path.normpath(
        os.path.join(os.path.dirname(__file__), '..')
    )


def collect_markdown_files(root_dir):
    files = []
    for root, dirs, fnames in os.walk(root_dir):
        dirs[:] = [d for d in dirs if not d.startswith('.')]
        for fname in fnames:
            if fname.endswith('.md'):
                files.append(os.path.join(root, fname))
    return files


def find_links(content):
    return re.findall(r'\]\(([^)]+)\)', content)


URI_SCHEME_RE = re.compile(r'^[a-zA-Z][a-zA-Z0-9+.-]*:')


def is_external(link):
    if URI_SCHEME_RE.match(link):
        return True
    if link.startswith('#'):
        return True
    return False


def main():
    opencode_dir = _opencode_dir()
    errors = []

    # 1. Scan all .md files for broken links
    for filepath in collect_markdown_files(opencode_dir):
        with open(filepath, 'r') as f:
            content = f.read()

        links = find_links(content)
        for link in links:
            if is_external(link):
                continue

            # Strip anchor
            clean = link.split('#')[0]
            if not clean:
                continue

            # Check for old paths
            if '.github/' in clean or '.vscode/' in clean:
                errors.append(
                    f"  {filepath}: old path reference '{link}'"
                )
                continue

            resolved = os.path.normpath(
                os.path.join(os.path.dirname(filepath), clean)
            )
            if not os.path.exists(resolved):
                errors.append(
                    f"  {filepath}: link target not found "
                    f"'{link}' -> '{resolved}'"
                )

    # 2. Check AGENT-MODEL.md references
    agent_model = os.path.join(opencode_dir, 'framework', 'AGENT-MODEL.md')
    if os.path.exists(agent_model):
        with open(agent_model, 'r') as f:
            content = f.read()
        agent_refs = re.findall(r'\]\(([^)]+\.agent\.md)\)', content)
        for ref in agent_refs:
            resolved = os.path.normpath(
                os.path.join(os.path.dirname(agent_model), ref)
            )
            if not os.path.exists(resolved):
                errors.append(
                    f"  {agent_model}: agent reference not found "
                    f"'{ref}' -> '{resolved}'"
                )

    if errors:
        print("REFERENCE CHECKS FAILED:")
        for e in errors:
            print(e)
        sys.exit(1)

    print("All reference checks passed.")
    sys.exit(0)


if __name__ == '__main__':
    main()
