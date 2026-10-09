#!/usr/bin/env bash
# Fetch the plain-C# dependencies the harness compiles from source.
# Unity.Mathematics is pure C# with no engine dependency, so building it
# alongside our assemblies lets the simulation compile and test with the
# stock .NET SDK -- no Unity license, no editor, runs in CI.
#
# The checkout is pinned to an immutable commit (invariants/INV-004): a tag
# can be moved, and this source is compiled and executed in CI. The pin is
# the commit the 1.2.5 tag pointed at when it was reviewed
# (git ls-remote https://github.com/Unity-Technologies/Unity.Mathematics.git refs/tags/1.2.5).
set -eu
cd "$(dirname "$0")"
um_url=https://github.com/Unity-Technologies/Unity.Mathematics.git
um_commit=53b0c4756c3c0dff3746285a8b83e858c26cdb06
mkdir -p .deps
# Re-fetch when the directory is missing or sits on a different commit
# (for example a stale tag clone from before the pin).
if [ "$(git -C .deps/um rev-parse HEAD 2>/dev/null || true)" != "$um_commit" ]; then
    rm -rf .deps/um
    git init -q .deps/um
    git -C .deps/um fetch -q --depth 1 "$um_url" "$um_commit"
    git -C .deps/um -c advice.detachedHead=false checkout -q FETCH_HEAD
    got=$(git -C .deps/um rev-parse HEAD)
    [ "$got" = "$um_commit" ] || { echo "Unity.Mathematics pin mismatch: $got"; exit 1; }
fi
echo "deps ready: .deps/um @ $um_commit"
