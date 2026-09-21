#!/usr/bin/env bash
# Build the engine-free assemblies and run the edit-mode tests with dotnet.
#   tools/plaincs/run_tests.sh [extra dotnet test args]
# Resolves dotnet from PATH, falling back to the Windows SDK under WSL.
#
# Excludes [Category("Slow")] tests (currently just
# SolverBenchmarks.Benchmark_2000Blocks, a multi-second 2000-block CG/
# buckling run) by default so the ~30s default run stays fast; run the
# slow ones explicitly with:
#   tools/plaincs/run_tests.sh --filter "TestCategory=Slow"
# or, to run everything including Slow:
#   tools/plaincs/run_tests.sh --filter "TestCategory!=NeverMatches"
set -eu
cd "$(dirname "$0")"
./fetch_deps.sh >/dev/null
dn=""
for c in dotnet "/mnt/c/Program Files/dotnet/dotnet.exe"; do
    if command -v "$c" >/dev/null 2>&1; then dn="$c"; break; fi
done
[ -n "$dn" ] || { echo "dotnet SDK not found"; exit 2; }
if printf '%s\n' "$@" | grep -q -- '--filter'; then
    "$dn" test Hullbreach.Plain.Tests/Hullbreach.Plain.Tests.csproj --nologo -v q "$@"
else
    "$dn" test Hullbreach.Plain.Tests/Hullbreach.Plain.Tests.csproj --nologo -v q --filter "TestCategory!=Slow" "$@"
fi
