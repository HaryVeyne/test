#!/usr/bin/env bash
set -euo pipefail

task_root="$(cd "$(dirname "$0")/.." && pwd)"
godot_bin="${GODOT_BIN:-godot}"
task_data="$(mktemp -d)"
trap 'rm -rf "$task_data"' EXIT
"$godot_bin" --headless --path "$task_root" --editor --import
dotnet build "$task_root/ProjectUnknown.csproj"
XDG_DATA_HOME="$task_data" "$godot_bin" --headless --path "$task_root" -- --verify
