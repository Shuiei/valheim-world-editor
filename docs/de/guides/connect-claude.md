<!-- source: e9f26daf -->
# Claude formen oder bauen lassen

Verbinde Claude Code oder Claude Desktop mit dem Editor und bitte Claude dann in einfachen Worten, den Boden zu formen,
zu pflanzen oder zu bauen.

**Du brauchst:** den Editor und [Claude Code](https://claude.com/claude-code) oder Claude Desktop auf demselben Computer.

1. Öffne auf der Startseite des Editors **Einstellungen** und such den Abschnitt **Claude**.
2. Schalte **Claude verbinden lassen** ein. Optional: schalte **Claude darf Skripte ausführen** ein, damit Claude
   C#-Skripte schreiben und ausführen kann (ein Skript läuft mit deinen Rechten auf diesem Computer; lass es aus, wenn
   du unsicher bist).
3. Drücke **Speichern** (das schließt die Einstellungen) und öffne dann erneut **Einstellungen**: der Abschnitt zeigt „Listening on http://127.0.0.1:5731/mcp“ (lauscht auf dieser Adresse).
4. Füge den Editor einmalig zu Claude hinzu:
   - **Claude Code:** drücke **Kopieren** neben dem Befehl, füge ihn in ein Terminal ein und führe ihn aus.
   - **Claude Desktop:** drücke **Kopieren** neben den Konfigurationszeilen, füge sie unter Claude Desktop → Settings →
     Developer → **Edit Config** (`claude_desktop_config.json`) hinzu und starte Claude Desktop dann neu.
5. Öffne im Editor den Bereich einer Welt in 3D oder die Werkstatt.
6. Bitte Claude zum Beispiel: „Ebne ein Quadrat von 40 m in der Mitte des Bereichs, pflastere es und stelle einen Ring
   aus Fackeln darum auf.“
7. Sieh Claude in der 3D-Ansicht bei der Arbeit zu. Jeder seiner Schritte erscheint im **Verlauf** als „Claude: …“.
8. Behalte es mit **In Welt speichern**, **Live anwenden** oder **Bauplan speichern**, oder nimm es zurück (unten).

<!-- picture: claude-code-mcp.jpg: a terminal running Claude Code after typing /mcp, the list showing "valheim-editor" as connected, next to the editor window with a 3 × 3 area open -->

## Prüfen, ob es geklappt hat

- In Claude Code listet `/mcp` **valheim-editor** als verbunden auf.
- Claudes erste Antwort beschreibt, was im Editor geöffnet ist.
- Die Zahl der ausstehenden Änderungen in der oberen Leiste wächst mit Claudes Änderungen; nichts wird geschrieben, bis
  du speicherst oder anwendest.

Sagt Claude, es erreiche den Editor nicht, siehe [Claude kann sich nicht verbinden](../help/troubleshooting.md#claude-kann-sich-nicht-verbinden).

## Rückgängig machen

- `Ctrl+Z` nimmt den letzten Schritt zurück, Claudes oder deinen.
- **Claudes Änderungen zurücknehmen**, oben im Verlauf, entfernt jeden Schritt, den Claude im offenen Bereich gemacht
  hat, und behält deine.
- **Verwerfen** verwirft alles, was nicht gespeichert ist.
- Um Claude den Zugang zum Editor zu sperren, schalte **Claude verbinden lassen** aus und drücke **Speichern**. **Neues
  Token** und dann **Speichern** sperrt jede Kopie des alten Befehls aus.

## Verwandte Seiten

- [Claude](../claude.md): jede Einstellung, was Claude tun kann, Sicherheit
- [Sicherheit](../concepts/safety.md#skripte-und-claude): was Skripte und Claude erreichen können
- [Einen Schritt rückgängig machen, eine Änderung zurücknehmen oder alles verwerfen](undo-changes.md)
