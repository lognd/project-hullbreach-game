#!/usr/bin/env bash
# Project Hullbreach doctor: checks this computer and explains every fix.
# Usage: ./doctor.sh [--demo online|guest|platform] [--deep] [--collect]
exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/tools/hbtools/run.sh" hb-doctor "$@"
