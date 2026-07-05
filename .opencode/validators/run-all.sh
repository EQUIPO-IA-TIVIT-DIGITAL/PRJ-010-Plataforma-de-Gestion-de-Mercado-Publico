#!/bin/bash
# run-all.sh — Execute all validators and report results.
# Exit code 0 only if all validators pass.

set -e

DIR="$(cd "$(dirname "$0")" && pwd)"
total=0
passed=0
failed=0

run_check() {
    local name="$1"
    local script="$2"
    total=$((total + 1))
    if python3 "$DIR/$script" > /dev/null 2>&1; then
        echo "✓ $name"
        passed=$((passed + 1))
    else
        echo "✗ $name"
        failed=$((failed + 1))
    fi
}

run_check "check-dependencies" "check-dependencies.py"
run_check "check-refs" "check-refs.py"
run_check "check-secrets" "check-secrets.py"
run_check "check-skill-contract" "check-skill-contract.py"

echo ""
echo "$passed passed, $failed failed (total $total)"
exit "$failed"
