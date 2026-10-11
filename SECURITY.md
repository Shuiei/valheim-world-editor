# Security

## Reporting a vulnerability

Please don't open a public issue for a security problem. Report it privately instead:
**[Report a vulnerability](https://github.com/Shuiei/valheim-world-editor/security/advisories/new)**
(the repository's Security tab, then **Report a vulnerability**).

Say what is affected (the editor, the WorldEditorBridge plugin, or both), the version, and how to
reproduce it. Once it is fixed, the fix is released and the
advisory published, with credit to you if you want it.

## What is in scope

- **The editor**: reading and writing world saves, blueprints and settings, the SSH connection to a
  server, live mode, and the Claude connection (MCP server).
- **The plugin (WorldEditorBridge)**: its local connection and token, and the changes it applies to
  a running game or server.

The editor and the plugin are only meant to listen on the computer they run on, behind a secret
token (see [Safety](README.md#safety)). Anything that lets another computer reach them, gets past
the token, or makes a world, blueprint or settings file run code or write outside its folder is a
vulnerability.

## Checking a download

Each file on the [releases page](https://github.com/Shuiei/valheim-world-editor/releases) has a
`<file>.sigstore.json` beside it: its [Sigstore](https://www.sigstore.dev/) signature, made by this
repository's release workflow (`.github/workflows/sign-release.yml`). With
[cosign](https://docs.sigstore.dev/cosign/system_config/installation/), download both and run:

```sh
cosign verify-blob ValheimWorldEditor-v1.19.0-linux-x64.tar.gz \
  --bundle ValheimWorldEditor-v1.19.0-linux-x64.tar.gz.sigstore.json \
  --certificate-identity-regexp '^https://github.com/Shuiei/valheim-world-editor/\.github/workflows/sign-release\.yml@' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

`Verified OK` means the file is the one the release was signed with, byte for byte.

## Supported versions

Only the [latest release](https://github.com/Shuiei/valheim-world-editor/releases/latest) gets
security fixes. Use the same version of the editor and the plugin.
