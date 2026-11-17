<!-- source: d6008f75 -->
# Hilfe zum Valheim World Editor

Der Valheim World Editor öffnet eine Valheim-Welt, gespeichert oder laufend, in einem 3D-Fenster. Forme den Boden, baue,
platziere und verschiebe alles Mögliche, dann speichere es zurück oder sieh im Spiel zu, wie es sich ändert, während du
arbeitest.

**Neu hier?** [Den Editor installieren](start/install.md) und folge dann [deiner ersten Bearbeitung](start/first-edit.md):
zehn Minuten an der Kopie einer Welt, nichts zu verlieren. Suchst du etwas? Durchsuche die Hilfe (`Ctrl+F` im
Hilfefenster) oder beginne bei einer Aufgabe unten. Im Editor öffnet `F1` oder das kleine **?** oben an einem Panel die
Hilfe bei diesem Werkzeug.

> [!IMPORTANT]
> Behalte eine Kopie einer Welt, bevor du sie änderst: siehe [Eine Welt vor dem Bearbeiten sichern](guides/back-up-a-world.md).
> Der Editor prüft, dass sich jeder Speicherstand korrekt zurücklesen lässt, bevor er den alten aufgibt, aber er behält
> selbst keine Kopie.

## Erste Schritte

- [Den Editor installieren](start/install.md): Download, erster Start unter Windows, Linux oder macOS, und das Plugin für
  die Live-Bearbeitung.
- [Deine erste Bearbeitung](start/first-edit.md): die Kopie einer Welt öffnen, einen Hügel anheben, Bäume pflanzen,
  speichern und im Spiel ansehen.
- [Dein erster Bau](start/first-build.md): eine Hütte in der Werkstatt bauen, als Bauplan speichern und in eine Welt
  einfügen.

## Welten bearbeiten

- [Eine Welt vor dem Bearbeiten sichern](guides/back-up-a-world.md)
- [Eine Welt öffnen, die nicht in der Liste steht](guides/open-a-world.md)
- [Die Welt bearbeiten, die du gerade spielst](guides/edit-your-game-live.md)
- [Einen dedizierten Server bearbeiten, während gespielt wird](guides/edit-a-server-live.md)
- [Einen gemieteten oder über ein Panel gehosteten Server bearbeiten](guides/edit-a-rented-server.md)
- [Einen Schritt rückgängig machen, eine Änderung zurücknehmen oder alles verwerfen](guides/undo-changes.md)
- [Einen Bereich aus einer Sicherung wiederherstellen](guides/restore-from-backup.md)
- [Zonen neu erzeugen](guides/reset-zones.md)
- [Ein Objekt, einen Gegenstand in einer Truhe oder ein Schild finden](guides/find-things.md)
- [Eine mit Expand World, Riverheim oder Tunnelheim erstellte Welt bearbeiten](guides/use-mods-worlds.md)

## Das Land formen

- [Über die 8-m-Grenze des Spiels hinaus anheben oder graben](guides/raise-past-the-limit.md)
- [Eine Straße oder eine Rampe einen Hügel hinauf anlegen](guides/make-a-road.md)
- [Einen Fluss oder Teich graben](guides/make-a-river.md)
- [Bäume und Felsen räumen, einen Bauplatz ebnen](guides/clear-an-area.md)

## Bauen und füllen

- [Einen Wald, ein Feld oder eine Hecke pflanzen](guides/plant-a-forest.md)
- [Ein Gebäude verschieben oder kopieren](guides/move-a-building.md)
- [Ändern, was eine Truhe enthält oder ein Schild sagt](guides/fill-a-chest.md)
- [Einen Bauplan in eine Welt einfügen](guides/paste-a-blueprint.md)
- [Einen Dungeon erzeugen und in die Welt setzen](guides/make-a-dungeon.md)
- [Einen Seed finden, der nahe am Start hat, was du willst](guides/find-a-seed.md)
- [Claude formen oder bauen lassen](guides/connect-claude.md)

## Werkzeuge

- [Das Editorfenster](editor-basics.md): die Startseite, die obere Leiste, die Werkzeugleiste, die Kamera, der Bereich,
  das Speichern, der Verlauf, das Panel Ansicht und der Schnitt.
- [Die Weltkarte](map.md): einen Ort finden, ihn in 3D öffnen, die ganze Welt durchsuchen.
- [Formen und Malen](terrain.md): die Bodenpinsel, Stempel und Ohne Grenze.
- [Gelände](landform.md): eine ganze Geländeform mit einem Klick, aus einer eigenen Formel (Hügel, Krater, Plateaus,
  Gräben), dem Berggenerator (Gipfel, Grate und Vulkane über die Grenze des Spiels hinaus) oder einem Bild.
- [Pfad](path.md): Straßen, Rampen und Flüsse entlang einer Linie.
- [Maske](masks.md): jedes Werkzeug auf ein Biom, eine Höhe, eine Neigung oder eine Bemalung beschränken; ihre Karte
  enthält auch den Schalter Ohne Grenze.
- [Messen und Einblendungen](measure.md): Entfernungen, Neigungen, Neigungsfarben und Höhenlinien.
- [Region, Kopieren und Einfügen](area.md): ein Rechteck oder Polygon ebnen oder bemalen, seine Objekte auswählen, es
  kopieren und einfügen, nachwachsen lassen oder aus einer Sicherung wiederherstellen.
- [Platzieren](place.md): Bäume, Felsen, Feldfrüchte und Spawnpunkte.
- [Bauen](build.md): die Bauteile, die Spieler bauen, einzeln oder entlang von Linien, mit der Bauleiste.
- [Auswahl](select.md): Objekte auswählen, verschieben, drehen, kopieren, ersetzen und löschen und in sie hineinsehen.
- [Die Werkstatt](workshop.md): auf einer leeren Parzelle bauen und das Ergebnis als Bauplan behalten.
- [Dungeons](dungeons.md): einen Dungeon erzeugen oder die Räume des Spiels einzeln bearbeiten.
- [Eine neue Welt](new-world.md): die Welt eines Seeds sehen, bevor es sie gibt, und sie erstellen.
- [Live-Modus](live-mode.md): Live anwenden, Auto, Neu laden und die Spieler, während das Spiel läuft.
- [Skript](scripting.md): einen Bereich mit C#-Skripten formen, bemalen und füllen.
- [Claude](claude.md): was Claude im Editor kann, und seine Grenzen.

## Wie es funktioniert

- [Bereiche und Zonen](concepts/areas-and-zones.md): warum der Editor die Welt blockweise öffnet.
- [Speichern und Verlauf](concepts/saving-and-history.md): ausstehende Änderungen, Speichern, live Anwenden,
  Rückgängigmachen und Sicherungen.
- [Bodengrenzen](concepts/ground-limits.md): die 8-m-Grenze des Spiels und wie der Editor über sie hinausgeht.
- [Live bearbeiten](concepts/live-editing.md): wie der Editor mit einem laufenden Spiel spricht.
- [Objekte](concepts/objects.md): was ein Objekt ist, Einrasten, die Stabilitätsprüfung, Wachstumsregeln, Spawnpunkte.
- [Dungeons im Spiel](concepts/dungeons.md): wie das Spiel Dungeons speichert und wie der Generator einen baut.
- [Sicherheit](concepts/safety.md): was der Editor schreibt und warum nichts deinen Computer verlässt.

## Hilfe

- [Fehlerbehebung](help/troubleshooting.md): was zu tun ist, wenn etwas nicht funktioniert.
- [Fragen](help/faq.md): kurze Antworten auf das, was am häufigsten gefragt wird.
- [Tastenkürzel](help/shortcuts.md): jede Tasten- und Mausaktion.
- [Glossar](help/glossary.md): die Begriffe, die der Editor verwendet.

## Für Übersetzer und Mod-Autoren

- [Übersetzen](translating.md): die Sprachkataloge, wie man einen erstellt und wie man diese Dokumentation übersetzt.
- [Für Mod-Autoren](modding.md): was der Editor von Mods lernt, `worldeditor.json`, Erweiterungen und Add-ons.
