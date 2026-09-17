#!/usr/bin/env bash
# Build the engine-free assemblies and run the edit-mode tests with dotnet.
#   tools/plaincs/run_tests.sh [extra dotnet test args]
# Resolves dotnet from PATH, falling back to the Windows SDK under WSL.
set -eu
cd "$(dirname "$0")"
./fetch_deps.sh >/dev/null
dn=""
for c in dotnet "/mnt/c/Program Files/dotnet/dotnet.exe"; do
    if command -v "$c" >/dev/null 2>&1; then dn="$c"; break; fi
done
[ -n "$dn" ] || { echo "dotnet SDK not found"; exit 2; }
"$dn" test Hullbreach.Plain.Tests/Hullbreach.Plain.Tests.csproj --nologo -v q "$@"
