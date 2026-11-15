#!/usr/bin/env bash
# Bot balance report: bot-vs-bot matches through the real ServerSimulation, table on stdout.
#   tools/plaincs/bot_report.sh [--seeds N] [--seconds S] [--storm] [--personalities] [--only NAME]
#   tools/plaincs/bot_report.sh --one hard easy 3 --trace     # one match, with a position trace
# A simulated second costs about 0.15 s of real time (the structural solver runs every tick), so run
# scenarios in parallel with --only to keep a wide sweep under a few minutes.
set -eu
cd "$(dirname "$0")"
./fetch_deps.sh >/dev/null
exec dotnet run -c Release --project Hullbreach.Bots.Cli -- "$@"
