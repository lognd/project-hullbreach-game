#!/usr/bin/env bash
# Fetch the plain-C# dependencies the harness compiles from source.
# Unity.Mathematics is pure C# with no engine dependency, so building it
# alongside our assemblies lets the simulation compile and test with the
# stock .NET SDK -- no Unity license, no editor, runs in CI.
set -eu
cd "$(dirname "$0")"
mkdir -p .deps
if [ ! -d .deps/um ]; then
    git clone -q --depth 1 --branch 1.2.5 https://github.com/Unity-Technologies/Unity.Mathematics.git .deps/um
fi
echo "deps ready: .deps/um"
