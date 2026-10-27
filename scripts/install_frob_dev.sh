#!/usr/bin/env bash
# Install the frob dev pre-release (x86_64 linux frob-cli) into $1 (default ~/.local/bin).
# Why: the pinned PyPI 0.532.0 cannot bind C# NUnit tests (TEST001/COV001); the
# dev build can. The asset name carries the commit, so it is discovered, then
# verified against its published .sha256 before it is unpacked.
set -euo pipefail
dest="${1:-$HOME/.local/bin}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cd "$work"
gh release download dev -R lognd/frob -p 'frob-cli-x86_64-unknown-linux-gnu-*'
sha256sum -c ./*.sha256
tar -xJf ./*.tar.xz
mkdir -p "$dest"
install -m 0755 frob-cli-x86_64-unknown-linux-gnu/frob "$dest/frob"
"$dest/frob" --version
