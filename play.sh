#!/usr/bin/env bash
# Project Hullbreach launcher: build, host, join, serve. ./play.sh --help
exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/tools/hbtools/run.sh" hb-play "$@"
