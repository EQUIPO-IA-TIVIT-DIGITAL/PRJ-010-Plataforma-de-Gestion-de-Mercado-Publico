#!/usr/bin/env python3
"""
check-secrets.py

Scans all .md and .json files in the workspace (excluding .git/) for
common credential patterns:
  - sk-... (OpenAI-style API keys)
  - ghp_... (GitHub personal access tokens)
  - Generic patterns: token, password, secret, api_key followed by = or :

Guardrail level: alerta (warns but does not block).
Exit code: always 0.
"""

import sys
import os
import re


def _workspace_root():
    return os.path.normpath(
        os.path.join(os.path.dirname(__file__), '..', '..')
    )


SECRET_PATTERNS = [
    (r'sk-\w{20,}', 'OpenAI-style API key (sk-...)'),
    (r'ghp_\w{20,}', 'GitHub personal access token (ghp_...)'),
    (
        r'(?:token|password|secret|api_key)\s*[=:]\s*[\'"]?\w[\w@#$%^&+=]{3,}[\'"]?',
        'Generic credential pattern (token/password/secret/api_key)',
    ),
]


def main():
    workspace = _workspace_root()
    warnings = []

    for root, dirs, files in os.walk(workspace):
        dirs[:] = [d for d in dirs if d != '.git']
        for fname in files:
            if not (fname.endswith('.md') or fname.endswith('.json')):
                continue
            filepath = os.path.join(root, fname)
            try:
                with open(filepath, 'r') as f:
                    content = f.read()
            except (UnicodeDecodeError, IsADirectoryError):
                continue

            for pattern, desc in SECRET_PATTERNS:
                for match in re.finditer(pattern, content, re.IGNORECASE):
                    line_no = content[:match.start()].count('\n') + 1
                    warnings.append(
                        f"  {filepath}:{line_no} possible {desc}"
                    )

    if warnings:
        print("SECRET SCAN WARNINGS:")
        for w in warnings:
            print(w)
        print("\n(Guardrail level: alerta - no blocking)")
    else:
        print("No secrets detected.")

    sys.exit(0)


if __name__ == '__main__':
    main()
