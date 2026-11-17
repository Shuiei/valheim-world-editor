<!-- source: 4a8a11bc -->
# Eine Welt vor dem Bearbeiten sichern

Lege eine Kopie des Ordners einer Welt an, zu der du immer zurückkehren kannst, egal was danach mit der Welt passiert.

**Du brauchst:** den Namen der Welt und ein geschlossenes Valheim (oder einen gestoppten Server, der die Welt
ausführt).

1. Schließe Valheim oder stoppe den Server. Ein laufendes Spiel schreibt die Welt ständig, daher kann eine Kopie, die
   währenddessen entsteht, halb alt und halb neu sein.
2. Finde den Ordner der Welt: Klicke auf der Startseite des Editors auf **Eine gespeicherte Welt** und fahre mit der
   Maus über die Karte der Welt. Der Tooltip endet mit dem Ordner, zum Beispiel
   `~/.config/unity3d/IronGate/Valheim/worlds_local/Midgard`.
3. Öffne den Ordner, der ihn enthält (`worlds_local`), in deinem Dateimanager. Die üblichen Orte sind:
   - Windows: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\worlds_local`
   - Linux: `~/.config/unity3d/IronGate/Valheim/worlds_local` (Valheim über Proton:
     `<Steam library>/steamapps/compatdata/892970/pfx/drive_c/users/steamuser/AppData/LocalLow/IronGate/Valheim/worlds_local`)
   - macOS: `~/Library/Application Support/IronGate/Valheim/worlds_local`
   - Ein dedizierter Server: `<savedir>/worlds_local` auf dem Server.
4. Kopiere den Ordner der Welt (`Midgard`) und füge die Kopie im selben Ordner `worlds_local` ein.
5. Benenne die Kopie in `<World>_backup_<date>` um, zum Beispiel `Midgard_backup_2026-10-22`. Behalte den Namen der
   Welt genau so, wie er ist, dann `_backup_`.

> [!TIP]
> So benannt liegt die Kopie neben der Welt, und der Editor führt sie im Werkzeug Region unter
> **Zurück zu einer Sicherung** als Sicherung auf, sodass du später einen Bereich daraus zurückholen kannst: siehe
> [Einen Bereich aus einer Sicherung wiederherstellen](restore-from-backup.md).

## Prüfen, ob es geklappt hat

- Die Kopie enthält dieselben Dateien wie die Welt: `_main.<n>.chunks`, `.db2`, `.fwl2`, `.ok` und die
  `.chunk`-Dateien.
- Auf der Startseite führt **Eine gespeicherte Welt** auch die Kopie auf, unter ihrem eigenen Namen. Öffne zum
  Bearbeiten die Welt selbst, nicht die Kopie.
- In der 3D-Ansicht einer Welt führt **Zurück zu einer Sicherung** im Werkzeug [Region](../area.md) die Kopie als
  **Editor · \<Datum\>** auf.

## Rückgängig machen

Die Kopie ändert nichts an der Welt: Lösche den Ordner der Kopie, wenn du ihn nicht mehr brauchst.

Um die ganze Welt so zurückzusetzen, wie sie in der Kopie war:

1. Schließe Valheim oder stoppe den Server.
2. Verschiebe den Ordner der Welt (`Midgard`) aus `worlds_local` heraus, zum Beispiel auf deinen Schreibtisch
   (lösche ihn später, wenn du sicher bist).
3. Benenne die Kopie (`Midgard_backup_2026-10-22`) in den Namen der Welt (`Midgard`) um.
4. Starte das Spiel: Es lädt die Welt so, wie sie beim Kopieren war.

Das Spiel legt auch eigene Sicherungen neben der Welt an (`<World>_backup_auto-<date>`); der Editor führt sie als
**Spiel · \<Datum\>** auf.

## Verwandte Seiten

- [Deine erste Bearbeitung](../start/first-edit.md)
- [Einen Bereich aus einer Sicherung wiederherstellen](restore-from-backup.md)
- [Einen Schritt rückgängig machen, eine Änderung zurücknehmen oder alles verwerfen](undo-changes.md)
- [Sicherungen](../concepts/saving-and-history.md#sicherungen)
