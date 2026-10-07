# The plugin's changelog for its Thunderstore page, made from the editor's CHANGELOG.md (the editor and
# the plugin share one version and one changelog). Each version is split in two: what changed in the
# plugin (the "### WorldEditorBridge" part of the version, or "unchanged") and what changed in the
# editor, with a link to that editor on GitHub (Thunderstore does not host programs). Only versioned
# sections are kept; the older history is in the full changelog on GitHub.
# Usage: python3 changelog.py <CHANGELOG.md> <out.md> <version> [released tags...]
import re, sys

src, out, version, tags = sys.argv[1], sys.argv[2], sys.argv[3], set(sys.argv[4:])
repo = 'https://github.com/Shuiei/valheim-world-editor'
text = open(src, encoding='utf-8').read()
sections = re.split(r'(?m)^(?=## )', text)
lines = [
    '# Changelog', '',
    'WorldEditorBridge is the live-editing plugin of **Valheim World Editor**: the two share one version.',
    '**Download the editor that goes with this plugin from GitHub:**',
    f'[Valheim World Editor v{version}]({repo}/releases/tag/v{version}) (every version: the',
    f'[releases page]({repo}/releases)). Thunderstore only hosts the plugin.', '',
    'Each version below says what changed in this plugin, then what changed in the editor.', '',
]
for sec in sections:
    m = re.match(r'## v(\d+\.\d+\.\d+)\b(.*)', sec)
    if not m:
        continue
    v, title = m.group(1), m.group(2)
    # The version's parts ("### Added", "### WorldEditorBridge"...).
    parts = re.split(r'(?m)^(?=### )', sec.split('\n', 1)[1] if '\n' in sec else '')
    plugin = [p for p in parts if p.startswith('### WorldEditorBridge')]
    editor = [p for p in parts if p.startswith('### ') and not p.startswith('### WorldEditorBridge')]
    lines += [f'## {v}{title}', '', '### WorldEditorBridge (this plugin)', '']
    if plugin:
        lines.append(plugin[0].split('\n', 1)[1].strip('\n'))
    else:
        lines.append('Unchanged: only the version number follows the editor\'s.')
    lines += ['', '### Valheim World Editor', '']
    if v == version or f'v{v}' in tags:
        lines += [f'Download: [Valheim World Editor v{v}]({repo}/releases/tag/v{v})', '']
    lines.append('\n\n'.join(p.replace('### ', '#### ', 1).strip('\n') for p in editor) if editor else 'No change.')
    lines.append('')
lines += [f'Older history: [the full changelog]({repo}/blob/main/CHANGELOG.md).']
open(out, 'w', encoding='utf-8').write('\n'.join(lines).rstrip('\n') + '\n')
