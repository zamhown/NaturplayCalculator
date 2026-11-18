#!/bin/bash
# Build the Uno AppKit application and preserve executable modes in the ZIP.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet_command="${NATURPLAY_DOTNET:-dotnet}"
architecture="$(uname -m)"
[[ "$architecture" != x86_64 ]] || architecture=x64
run=false
test=true
locked=true
while [[ $# -gt 0 ]]; do
    case "$1" in
        --arch) architecture="${2:?--arch requires arm64 or x64}"; shift ;;
        --run) run=true ;;
        --skip-tests) test=false ;;
        --update-lock) locked=false ;;
        *) echo "Usage: $0 [--arch arm64|x64] [--run] [--skip-tests] [--update-lock]" >&2; exit 2 ;;
    esac
    shift
done
[[ "$(uname -s)" == Darwin ]] || { echo 'The AppKit package must be built on macOS.' >&2; exit 1; }
[[ "$architecture" == arm64 || "$architecture" == x64 ]] || { echo 'Supported architectures: arm64, x64.' >&2; exit 2; }
command -v "$dotnet_command" >/dev/null || { echo 'Install the .NET SDK specified in global.json, or set NATURPLAY_DOTNET to its executable.' >&2; exit 1; }
cd "$root"
runtime="osx-$architecture"
destination="$root/artifacts/macos/$runtime"
app="$destination/Naturplay Calculator.app"
if pgrep -f "$app/Contents/MacOS/NaturplayCalculator.Desktop" >/dev/null; then
    echo 'Close the packaged calculator before rebuilding it.' >&2
    exit 1
fi
if "$test"; then
    "$dotnet_command" test ncalc/NaturplayCalculator.Tests -c Release "-p:RestoreLockedMode=$locked"
fi
"$dotnet_command" publish ncalc/NaturplayCalculator.Desktop -c Release -r "$runtime" \
    -p:PackageFormat=app "-p:RestoreLockedMode=$locked"
source_app="$root/ncalc/NaturplayCalculator.Desktop/bin/Release/net10.0-desktop/$runtime/publish/Naturplay Calculator.app"
test -x "$source_app/Contents/MacOS/NaturplayCalculator.Desktop"
mkdir -p "$destination"
rm -rf "$app"
ditto "$source_app" "$app"
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"
ditto -c -k --sequesterRsrc --keepParent "$app" "$destination/NaturplayCalculator-$runtime.zip"
echo "Built: $app"
if "$run"; then open "$app"; fi
