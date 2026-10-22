# Generate a dungeon and put it in the world

Make a whole dungeon from a few choices and place it behind an entrance, as the game does with its own.

**You need:** a world open in the editor, with the area where the entrance goes open, in a zone with no dungeon
entrance yet (a backup first: see [Back up a world](back-up-a-world.md)).

## Steps

1. Click **Dungeon** on the tool rail (CREATE section).
2. Under **Generate a dungeon**, choose the **Biome**: it sets the monsters, loot and the entrance on the ground.
3. Choose the **Size** and **Levels**, and the boss under **Boss**.
4. Look at the plan; click **Level 1**, **Level 2**... under it to see each level.
5. Optional: click **Randomize** until you like the plan, or type a **Name** for the sign in the entrance hall.
6. Turn the view to face the spot where the entrance should go, with that spot in the middle of the view.
7. Click **Place in the world**. The entrance appears on the ground, its door facing you.
8. Click **Go to the dungeon** to look inside from above, the roofs cut away.
9. Click **Go to the entrance** to come back down.
10. Click **Save to world** (or **Apply live**).

![The entrance of a generated dungeon on the ground](../images/dungeon-entrance.jpg)
*The entrance after **Place in the world**: a burial chamber's mound, its door facing the camera.*

To change the dungeon by hand before placing it (move furniture, fill chests), click **Open in the Workshop** instead
of step 7, then save it as a blueprint and paste it: see [Paste a blueprint into a world](paste-a-blueprint.md).

## Check it worked

The status bar says *Generated …: … objects, 5000 m above here.* **Go to the dungeon** shows its rooms. After saving,
open the world in game, walk to the entrance and go through the door: you arrive in the entrance hall.

## Undo it

- Before saving: `Ctrl+Z` takes back the whole placement in one step.
- After saving: see [Undo a step, take back one change, or discard everything](undo-changes.md), or
  [Restore one area from a backup](restore-from-backup.md).

## Tips

- *Something is already up there*: each zone (64 × 64 m) holds one dungeon. Put the entrance in another zone.
- The same choices and seed always make the same dungeon of building pieces: note the **Seed** to make it again. A
  dungeon of the game's own rooms is laid out again from its entrance when placed, so it also needs the same spot and
  facing.
- **Made of: The game's own rooms** builds it from the game's rooms instead, to edit room by room.

## Related

- [Dungeon](../dungeons.md)
- [How the game keeps dungeons](../concepts/dungeons.md#how-the-game-keeps-dungeons)
- [Workshop](../workshop.md)
