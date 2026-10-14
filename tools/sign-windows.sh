#!/usr/bin/env bash
# Signs the program of the Windows package (Authenticode) with the maintainer's Certum code-signing
# certificate, through SimplySign's cloud: the private key never leaves Certum, ssign
# (https://github.com/Le-Syl21/ssign) logs in with the account's e-mail and a one-time code and gets
# the signature over HTTPS, with Certum's RFC 3161 timestamp (the signature outlives the certificate).
# The zip is rewritten in place with the signed ValheimWorldEditor.exe; nothing else in it changes.
# Usage: tools/sign-windows.sh <ValheimWorldEditor-v<version>-win-x64.zip>
#   needs ssign, unzip and zip, and in the environment CERTUM_EMAIL with either CERTUM_OTP (the TOTP
#   seed: CI) or CERTUM_TOKEN (the current six-digit code from the SimplySign app: by hand).
#   With osslsigncode installed, the signature is checked after.
set -euo pipefail
zip=$(realpath "${1:?the win-x64 zip}")
: "${CERTUM_EMAIL:?the e-mail of the Certum account}"
if [ -z "${CERTUM_OTP:-}" ] && [ -z "${CERTUM_TOKEN:-}" ]; then
  echo "Set CERTUM_OTP (the TOTP seed) or CERTUM_TOKEN (the current six-digit code)." >&2; exit 2
fi
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
unzip -q "$zip" -d "$work"
exe=$work/ValheimWorldEditor/ValheimWorldEditor.exe
[ -f "$exe" ] || { echo "No ValheimWorldEditor/ValheimWorldEditor.exe in $zip" >&2; exit 1; }
ssign -n "Valheim World Editor" -u https://github.com/Shuiei/valheim-world-editor "$exe"
if command -v osslsigncode > /dev/null; then
  osslsigncode verify -in "$exe"
else
  echo "osslsigncode is not installed: the signature was not checked."
fi
rm "$zip"
(cd "$work" && zip -qr "$zip" ValheimWorldEditor)
echo "signed  $(basename "$zip")"
