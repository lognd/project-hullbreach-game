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
#    Git Bash on Windows ships `python`, not `python3`, so resolve whichever
#    exists. Do not swallow the interpreter's own errors: hiding them turned
#    "python3 not found" into "invalid JSON: Packages/manifest.json" and sent
#    people hunting a syntax error that was not there.
py=""
for candidate in python3 python py; do
    if command -v "$candidate" >/dev/null 2>&1; then py="$candidate"; break; fi
done
if [ -z "$py" ]; then
    fail "no python interpreter found (tried python3, python, py); cannot validate JSON"
else
    for j in Packages/manifest.json $(git ls-files 'Assets/*.asmdef'); do
        "$py" -c "import json,sys; json.load(open(sys.argv[1]))" "$j" || fail "invalid JSON: $j"
    done
fi

# 5. Nothing that looks like a credential.
if git grep -nIE '(api[_-]?key|secret|password|token)\s*[:=]\s*"[A-Za-z0-9_\-]{16,}"' -- Assets ProjectSettings Packages >/dev/null 2>&1; then
    fail "something under Assets/ProjectSettings/Packages looks like a hardcoded credential"
fi

# 6. No IMGUI creeping back into runtime code (docs/design/ui-port.md D8):
#    OnGUI/GUILayout./GUI. at real code positions, not inside a comment.
while IFS= read -r -d '' f; do
    code=$(sed -E 's#//.*$##' "$f")
    if grep -qE 'OnGUI\(|GUILayout\.|GUI\.' <<<"$code"; then
        fail "IMGUI use in runtime code: $f"
    fi
done < <(git ls-files -z 'Assets/Scripts/*.cs')

if [ "$problems" -eq 0 ]; then echo "unity tree: clean"; fi
exit "$problems"
