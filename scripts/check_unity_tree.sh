#!/usr/bin/env bash
# Sanity checks on the committed Unity tree that need no Unity install.
# CI runs this on every PR; run it locally before pushing.
#
#   scripts/check_unity_tree.sh
#
# Exit status is the number of problems found, so 0 means clean.
set -u
cd "$(git rev-parse --show-toplevel)"
problems=0
fail() { echo "FAIL: $*"; problems=$((problems + 1)); }

# 1. Every asset has a .meta and every .meta has an asset. A missing pair
#    makes Unity regenerate GUIDs on the next import and silently breaks
#    every reference to that asset for everyone else.
while IFS= read -r -d '' f; do
    [ -e "$f.meta" ] || fail "missing meta: $f"
done < <(git ls-files -z Assets | grep -zv '\.meta$' | sed -z 's|/[^/]*$||' | sort -zu | grep -zv '^Assets$'; git ls-files -z Assets | grep -zv '\.meta$')
while IFS= read -r -d '' m; do
    [ -e "${m%.meta}" ] || fail "orphan meta: $m"
done < <(git ls-files -z Assets | grep -z '\.meta$')

# 2. Generated folders must never be committed.
for d in Library Temp Obj Logs UserSettings Build Builds; do
    if git ls-files --error-unmatch "$d" >/dev/null 2>&1; then fail "generated folder committed: $d"; fi
done

# 3. The editor version is pinned and everyone uses the same one.
want="6000.0.43f1"
have=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt | tr -d '\r')
[ "$have" = "$want" ] || fail "ProjectVersion.txt says $have, expected $want (update this script if the upgrade is deliberate)"

# 4. The package manifest and every asmdef parse as JSON.
for j in Packages/manifest.json $(git ls-files 'Assets/*.asmdef'); do
    python3 -c "import json,sys; json.load(open(sys.argv[1]))" "$j" 2>/dev/null || fail "invalid JSON: $j"
done

# 5. Nothing that looks like a credential.
if git grep -nIE '(api[_-]?key|secret|password|token)\s*[:=]\s*"[A-Za-z0-9_\-]{16,}"' -- Assets ProjectSettings Packages >/dev/null 2>&1; then
    fail "something under Assets/ProjectSettings/Packages looks like a hardcoded credential"
fi

if [ "$problems" -eq 0 ]; then echo "unity tree: clean"; fi
exit "$problems"
