# Let Claude shape or build

Connect Claude Code or Claude Desktop to the editor, then ask Claude in plain words to shape the ground, plant or build.

**You need:** the editor, and [Claude Code](https://claude.com/claude-code) or Claude Desktop on the same computer.

1. On the editor's start page, open **Settings** and find the **Claude** section.
2. Turn on **Allow Claude to connect**. Optional: turn on **Claude may run scripts** to let Claude write and run C#
   scripts (a script runs with your rights on this computer; leave it off if unsure).
3. Press **Save** (it closes Settings), then open **Settings** again: the section shows "Listening on http://127.0.0.1:5731/mcp".
4. Add the editor to Claude, once:
   - **Claude Code:** press **Copy** next to the command, paste it into a terminal and run it.
   - **Claude Desktop:** press **Copy** next to the configuration lines, add them to Claude Desktop → Settings →
     Developer → **Edit Config** (`claude_desktop_config.json`), then restart Claude Desktop.
5. In the editor, open a world's area in 3D, or the Workshop.
6. Ask Claude, for example: "Flatten a 40 m square in the middle of the area, pave it, and put a ring of torches round
   it."
7. Watch Claude work in the 3D view. Each of its steps appears in **History** as "Claude: …".
8. Keep it with **Save to world**, **Apply live** or **Save blueprint**, or take it back (below).

<!-- picture: claude-code-mcp.jpg: a terminal running Claude Code after typing /mcp, the list showing "valheim-editor" as connected, next to the editor window with a 3 × 3 area open -->

## Check it worked

- In Claude Code, `/mcp` lists **valheim-editor** as connected.
- Claude's first answer describes what is open in the editor.
- The top bar's pending count grows with Claude's changes; nothing is written until you save or apply.

If Claude says it cannot reach the editor, see [Claude cannot connect](../help/troubleshooting.md#claude-cannot-connect).

## Undo it

- `Ctrl+Z` takes back the last step, Claude's or yours.
- **Take back Claude's changes**, at the top of History, removes every step Claude made and keeps yours.
- **Discard** drops everything not saved.
- To stop Claude reaching the editor, turn **Allow Claude to connect** off and **Save**. **New token** then **Save**
  locks out any copy of the old command.

## Related

- [Claude](../claude.md): every setting, what Claude can do, safety
- [Safety](../concepts/safety.md#scripts-and-claude): what scripts and Claude can reach
- [Undo a step, take back one change, or discard everything](undo-changes.md)
