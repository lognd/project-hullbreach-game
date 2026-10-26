#!/usr/bin/env bash
# Shared bootstrap for doctor.sh and play.sh: make sure uv exists, then run
# the Python tool. All real logic lives in tools/hbtools (one implementation
# for every OS); this file only finds or installs uv.
set -u

tool="$1"
shift
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

find_uv() {
    if command -v uv >/dev/null 2>&1; then
        command -v uv
        return 0
    fi
    for candidate in "$HOME/.local/bin/uv" "$HOME/.cargo/bin/uv"; do
        if [ -x "$candidate" ]; then
            echo "$candidate"
            return 0
        fi
    done
    return 1
}

uv_bin="$(find_uv)"
if [ -z "$uv_bin" ]; then
    echo "The Hullbreach tools need a small helper program called uv."
    echo "uv downloads the right Python for you; it does not change your system Python."
    echo "It is installed with the official installer from https://astral.sh/uv"
    echo "(the command is: curl -LsSf https://astral.sh/uv/install.sh | sh)."
    echo
    answer=""
    if [ -r /dev/tty ]; then
        printf "Install uv now? Type y and press Enter for yes [y/N]: "
        read -r answer </dev/tty
    fi
    case "$answer" in
        y|Y|yes|YES|Yes) ;;
        *)
            echo "OK, nothing was installed. Install uv yourself with the command"
            echo "above, close and reopen this terminal, then run this again."
            exit 1
            ;;
    esac
    if ! command -v curl >/dev/null 2>&1; then
        echo "curl is missing. On Ubuntu/Debian type: sudo apt install curl"
        echo "then run this again."
        exit 1
    fi
    curl -LsSf https://astral.sh/uv/install.sh | sh || {
        echo "The uv installer failed (see the messages above). Check your internet"
        echo "connection and run this again."
        exit 1
    }
    uv_bin="$(find_uv)"
    if [ -z "$uv_bin" ]; then
        echo "uv was installed but this terminal cannot see it yet."
        echo "Close this terminal, open a new one, and run this again."
        exit 1
    fi
fi

if [ ! -d "$here/.venv" ]; then
    echo "Preparing the Hullbreach tools (the first time takes a minute)..."
fi
exec "$uv_bin" run --quiet --project "$here" "$tool" "$@"
