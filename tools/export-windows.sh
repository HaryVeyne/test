#!/usr/bin/env bash
set -euo pipefail

# Set GODOT_BIN to a Godot 4.6.3 .NET executable when it is not on PATH.
task_root="$(cd "$(dirname "$0")/.." && pwd)"
godot_bin="${GODOT_BIN:-godot}"
mkdir -p "$task_root/Builds/Windows"
"$godot_bin" --headless --path "$task_root" --editor --import
dotnet build "$task_root/ProjectUnknown.csproj" -c Release
"$godot_bin" --headless --path "$task_root" --export-release "Windows Desktop" "$task_root/Builds/Windows/ProjectUnknown.exe"
cp "$task_root/Assets/Fonts/LICENSE-NotoSansCJK.txt" "$task_root/Builds/Windows/LICENSE-NotoSansCJK.txt"
printf 'Windows build: %s\n' "$task_root/Builds/Windows/ProjectUnknown.exe"
