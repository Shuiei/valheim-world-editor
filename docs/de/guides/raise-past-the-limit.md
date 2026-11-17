<!-- source: c7695b6c -->
# Über die 8-m-Grenze des Spiels hinaus anheben oder graben

Hebe eine Klippe oder eine Felsnadel an oder grabe einen Canyon, weit über die 8 m hinaus, die das Spiel erlaubt, so
dass das Spiel jedes Spielers es ohne Mod lädt.

**Du brauchst:** eine in 3D geöffnete Welt ([eine Kopie oder eine Sicherung davon](back-up-a-world.md)) bei
geschlossenem Spiel, oder den [Live-Modus](edit-your-game-live.md).

1. Sichere die Welt. Das Speichern dieser Art von Boden leert den Verlauf des Bereichs.
2. Klicke in der Werkzeugleiste auf **Anheben** (`1`) oder **Absenken** (`2`).
3. Setze auf der Karte **Maske** neben den Pinseleinstellungen den Haken bei **Ohne Grenze: über die ±8 m des Spiels
   hinaus**. Die Statusleiste meldet, dass die Bodenwerkzeuge jetzt über die ±8 m des Spiels hinausgehen.
4. Stelle **Größe** und **Stärke** ein, halte dann die linke Maustaste auf dem Boden gedrückt und bewege die Maus.
   Der Boden stoppt nicht mehr bei 8 m und wird nicht mehr rot.
5. Optional: Forme das Ergebnis mit **Glätten** (`4`) oder **Naturnah** (`0`) nach, die über die Grenze hinaus
   weiterwirken, solange „Ohne Grenze“ an ist.
6. Sieh dir die Form von mehreren Seiten an. `Ctrl+Z` nimmt einen Strich zurück; **Urzustand** (`5`) nimmt einen
   Teil davon zurück.
7. Klicke auf **In Welt speichern** (oder **Live anwenden**). Die Speichermeldung sagt, wie viele Bodenscheiben
   entstanden sind und wie viele Bäume und Felsen entfernt wurden, zum Beispiel „Boden ohne Grenze: 312
   Bodenscheibe(n), 40 Bäume/Felsen entfernt“.

Für einen ganzen Berg mit einem Klick verwende stattdessen [Gelände](../landform.md#berge) mit seiner Art
**Berge**: Es geht immer über die Grenze hinaus.

## Prüfen, ob es geklappt hat

- Fahre mit dem Cursor über den neuen Boden: Die „ursprüngliche“ Höhe in der Statusleiste ist jetzt der neue Boden.
- Öffne die Welt im Spiel und geh dorthin. Die Form erscheint, wenn du dich näherst, so wie der eingeebnete Boden
  eines Dorfs.

## Rückgängig machen

- **Vor dem Speichern:** `Ctrl+Z`, der Pinsel **Urzustand** oder **Verwerfen** für alles nicht Gespeicherte.
- **Nach dem Speichern:** Der Verlauf beginnt neu ab der gespeicherten Welt. Der Pinsel **Urzustand** setzt die
  Stelle weiterhin auf den Boden zurück, den die Welt erzeugt hat (ebenso **Urzustand** im Werkzeug Pfad und
  **Zurück zum Urzustand** in Region): Speichere erneut, und die nicht mehr nötigen Bodenscheiben verschwinden. Oder
  setze die Stelle mit [Einen Bereich aus einer Sicherung wiederherstellen](restore-from-backup.md) zurück.

## Verwandte Seiten

- [Ohne Grenze](../concepts/ground-limits.md#ohne-grenze): wie der Boden über 8 m hinaus gespeichert wird
- [Formen und Malen](../terrain.md#ohne-grenze)
- [Gelände](../landform.md)
