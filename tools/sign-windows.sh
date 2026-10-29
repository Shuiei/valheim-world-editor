#!/usr/bin/env bash
# Signs what this repository ships for Windows to run or load (Authenticode: an unsigned program or DLL is what
# antivirus rules flag), with the maintainer's Certum code-signing certificate, through SimplySign's cloud: the
# private key never leaves Certum, ssign (https://github.com/Le-Syl21/ssign) logs in with the account's e-mail and a
# one-time code and gets the signatures over HTTPS, with Certum's RFC 3161 timestamp (they outlive the certificate).
# In the Windows package: the program and spirv-cross.dll, the one native library beside it that comes unsigned (the
# others come signed by Microsoft and AvaloniaUI). In every other zip given (the plugin's, the add-ons', the SDK's):
# every DLL, all built here.
# Each zip is rewritten in place with its files signed; nothing else in it changes.
# Usage: tools/sign-windows.sh <ValheimWorldEditor-v<version>-win-x64.zip | another zip>...
#   needs ssign, unzip and zip, and in the environment CERTUM_EMAIL with either CERTUM_OTP (the TOTP
#   seed: CI) or CERTUM_TOKEN (the current six-digit code from the SimplySign app: by hand).
#   With osslsigncode installed, the signatures are checked after.
set -euo pipefail
[ $# -gt 0 ] || { echo "Usage: tools/sign-windows.sh <zip>..." >&2; exit 2; }
: "${CERTUM_EMAIL:?the e-mail of the Certum account}"
if [ -z "${CERTUM_OTP:-}" ] && [ -z "${CERTUM_TOKEN:-}" ]; then
  echo "Set CERTUM_OTP (the TOTP seed) or CERTUM_TOKEN (the current six-digit code)." >&2; exit 2
fi
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
zips=() files=()
for arg in "$@"; do
  zip=$(realpath "$arg")
  dir="$work/${#zips[@]}"
  unzip -q "$zip" -d "$dir"
  zips+=("$zip")
  case "$(basename "$zip")" in
    ValheimWorldEditor-*-win-x64.zip)
      found=("$dir/ValheimWorldEditor/ValheimWorldEditor.exe" "$dir/ValheimWorldEditor/spirv-cross.dll")
      for f in "${found[@]}"; do
        [ -f "$f" ] || { echo "No ValheimWorldEditor/$(basename "$f") in $zip" >&2; exit 1; }
      done ;;
    *)
      mapfile -t found < <(find "$dir" -type f -name '*.dll' | sort)
      [ ${#found[@]} -gt 0 ] || { echo "No DLL in $zip" >&2; exit 1; } ;;
  esac
  files+=("${found[@]}")
done
# One login signs them all.
ssign -n "Valheim World Editor" -u https://github.com/Shuiei/valheim-world-editor "${files[@]}"
if command -v osslsigncode > /dev/null; then
  for f in "${files[@]}"; do osslsigncode verify -in "$f"; done
else
  echo "osslsigncode is not installed: the signatures were not checked."
fi
# Each package is replaced only once its new zip is complete.
for i in "${!zips[@]}"; do
  zip=${zips[$i]}
  (cd "$work/$i" && zip -qrX "$zip.signed" .)
  mv -f "$zip.signed" "$zip"
  echo "signed  $(basename "$zip")"
done
