# Find an object, a chest's item or a sign

Search the whole world for an object, an item in any chest, or a text on a sign or portal, and open it in 3D.

**You need:** a world open on its [map](../map.md).

1. In the map's side panel, open **Search the world**.
2. Type part of what you look for: a kind (`portal`, `chest`, `Beech`), an item (`Silver`, `Wood`) or words on a sign.
   Letter case does not matter.
3. Choose what to look for: **Objects (by kind)**, **Items in containers** or **Texts (signs, portals, wards…)**.
4. Press **Find** (or `Enter`). Every result is pinned in orange on the map, and listed with a count per kind.
5. Click a result in the list. The map centres on it and picks its zone.
6. Press **Edit in 3D**. The editor opens with that object selected. For an item or a text, its data opens too.
7. Optional: change the chest's contents or the sign's text, then **Save** (or **Apply live**).

![Search the world for Beech1: every beech pinned in orange, one picked with a gold ring](../images/map-search.jpg)
*Each pin is one object found; the one picked in the list is larger, with a gold ring.*

## Check it worked

- The line above the results says how many were found. The list shows the first 300; up to 2,000 are pinned.
- **Nothing found?** Check the spelling and what you chose to look for. The search sees the world as it is in the editor:
  objects you deleted but did not save are not found. The game's own bookkeeping objects (`_ZoneCtrl`…) are found
  only when your text starts with `_`.

## Undo it

The search changes nothing. A change you make to the object is one step: `Ctrl+Z` takes it back.

## Related

- [World map](../map.md#search-the-world)
- [Change what a chest holds or a sign says](fill-a-chest.md)
- [Select](../select.md)
