# The plugin's changelog for its Thunderstore page, made from the editor's CHANGELOG.md: the editor and
# the plugin share one version and one changelog. It says where to get the matching editor (GitHub:
# Thunderstore does not host programs), and links each version that has a GitHub release.
# Usage: python3 changelog.py <CHANGELOG.md> <out.md> <version> [released tags...]
import re, sys

src, out, version, tags = sys.argv[1], sys.argv[2], sys.argv[3], set(sys.argv[4:])
repo = 'https://github.com/Shuiei/valheim-world-editor'
text = open(src, encoding='utf-8').read()
body = text[text.index('\n## '):].lstrip('\n')
lines = [
    '# Changelog', '',
    'WorldEditorBridge is the live-editing plugin of **Valheim World Editor**: the two share one version',
    'and this changelog. **Download the editor that goes with this plugin from GitHub:**',
    f'[Valheim World Editor v{version}]({repo}/releases/tag/v{version}) (every version: the',
    f'[releases page]({repo}/releases)). Thunderstore only hosts the plugin.', '',
]
for line in body.split('\n'):
    m = re.match(r'^## v(\d+\.\d+\.\d+)\b(.*)$', line)
    if not m:
        lines.append(line)
        continue
    v = m.group(1)
    lines.append(f'## {v}{m.group(2)}')
    if v == version or f'v{v}' in tags:
        lines += ['', f'Editor: [Valheim World Editor v{v}]({repo}/releases/tag/v{v})']
open(out, 'w', encoding='utf-8').write('\n'.join(lines).rstrip('\n') + '\n')
