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

# 7. The keyboard Horizontal axis must not be inverted: the right arrow has
#    to steer the ship right, and the invert flag is invisible in code review.
if awk '/negativeButton: left/{f=1; next} f && /^\s*invert:/{print $0; exit}' ProjectSettings/InputManager.asset | grep -q 'invert: 1'; then
    fail "keyboard Horizontal axis (negativeButton: left) has invert: 1 in ProjectSettings/InputManager.asset"
fi

# 8. Security policy rules (invariants/INV-001..004). Each guards a recurrence
#    class found in the network-trust audit; the tickets named below make the
#    tree pass. Heuristic greps, not taint analysis.
net=Assets/Scripts/Hullbreach.Net
# POL-net-no-remote-sized-alloc (INV-001): no array sized by a wire-read count.
if grep -nE 'new [A-Za-z0-9_]+\[(count|n|len|length)\]' "$net/NetMessages.cs" >/dev/null 2>&1; then
    fail "POL-net-no-remote-sized-alloc: wire-read count sizes an allocation in $net/NetMessages.cs (INV-001)"
fi
# POL-net-reader-bounds-checked (INV-001): ByteReader must expose a remaining-length check.
if ! grep -qE 'Remaining|TryRead' "$net/Wire.cs"; then
    fail "POL-net-reader-bounds-checked: ByteReader in $net/Wire.cs has no bounds/remaining check (INV-001)"
fi
# POL-net-reliable-window-bounded (INV-002): the reorder buffer needs an explicit window constant.
if ! grep -qE 'MaxReliable(Window|Pending)' "$net/ClientReplica.cs"; then
    fail "POL-net-reliable-window-bounded: no MaxReliableWindow/MaxReliablePending bound in $net/ClientReplica.cs (INV-002)"
fi
# POL-server-validates-client-design (INV-003): Join must validate the client-supplied design.
if ! grep -qE 'ValidateDesign|DesignValidator' "$net/ServerSimulation.cs"; then
    fail "POL-server-validates-client-design: ServerSimulation does not validate client-supplied designs/inputs (INV-003)"
fi
# POL-fetch-pinned-commit (INV-004): a cloned dependency must be pinned to a 40-hex commit.
while IFS= read -r -d '' f; do
    [ "$f" = scripts/check_unity_tree.sh ] && continue  # this rule's own text names `git clone`
    if grep -q 'git clone' "$f" && ! grep -qE '\b[0-9a-f]{40}\b' "$f"; then
        fail "POL-fetch-pinned-commit: $f clones without a pinned commit hash (INV-004)"
    fi
done < <(git ls-files -z 'tools/*.sh' 'scripts/*.sh')

if [ "$problems" -eq 0 ]; then echo "unity tree: clean"; fi
exit "$problems"
