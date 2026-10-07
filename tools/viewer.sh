#!/usr/bin/env bash
# Launches the developer match viewer (godot/) with Godot 4.7.2 .NET.
#
#   tools/viewer.sh [--seed=N] [--start=TICK] [--speed=X] [--autoplay]
#
# GODOT overrides the Godot executable; DOTNET_ROOT (default ~/.dotnet) must be an Arm64 .NET 10 SDK on Apple
# Silicon — see README, "Opening the Godot project".
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"
godot="${GODOT:-}"
if [[ -z "$godot" ]]; then
  for candidate in \
    "/Applications/Godot_mono.app/Contents/MacOS/Godot" \
    "$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot" \
    "$HOME/Downloads/Godot_mono.app/Contents/MacOS/Godot"; do
    if [[ -x "$candidate" ]]; then godot="$candidate"; break; fi
  done
fi
if [[ -z "$godot" ]]; then
  echo "Godot 4.7.2 .NET not found; set GODOT to its executable." >&2
  exit 1
fi

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

"$godot" --headless --path "$repo/godot" --build-solutions --quit >/dev/null
exec "$godot" --path "$repo/godot" ++ "$@"
