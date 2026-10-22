# Change what a chest holds or a sign says

Put items into a chest, take them out, or write new text on a sign, with the Inspector.

**You need:** a world open in the editor, with the area round the chest or sign open. To find it first, see
[Find an object, a chest's item or a sign](find-things.md).

## A chest

1. Press `E` to open [Select](../select.md).
2. Double-click the chest: it is selected alone and opens in the Inspector, its slots shown as in game.
3. Click an empty slot.
4. In **Find an item…**, type a few letters of the item (`iron`, `mead`) and choose it in the list.
5. Set **How many** and, for tools and armour, **Quality**.
6. Repeat for other slots. To take an item out, click its slot, then **Empty**; to move it, **Move**, then another
   slot.
7. Click **Apply changes**.
8. Click **Save to world** (or **Apply live**).

How many is cut to the largest stack the game allows for that item. Changed contents stay the chest's own: a chest
nobody has opened does not fill itself with the game's loot over them.

<!-- picture: guide-chest-slot.jpg: a wood chest selected and open in the Inspector, one slot clicked with Find an item… filled with "IronScrap", How many 20; three other slots holding items with their icons; Apply changes visible -->

## A sign

1. Press `E`, then click the sign.
2. Press `I` (**Inspect data (I)**): double-clicking a sign would select the whole building.
3. Under **DATA**, find **Text (sign)** and type the new text.
4. Click **Apply changes**, then **Save to world** (or **Apply live**).

For a sign that has no text yet, use **Add**: choose **text**, type `text` as the name, then the words.

## Check it worked

Select the chest or sign again and press `I`: the Inspector shows the new contents or text. After saving, open the
chest or read the sign in game.

## Undo it

- Before applying: **Revert** forgets the changes in the card.
- After applying: `Ctrl+Z` puts the old chest or sign back.
- After saving: see [Undo a step, take back one change, or discard everything](undo-changes.md).

## Related

- [Inspector](../select.md) (on the Select page)
- [What an object is](../concepts/objects.md#what-an-object-is)
- [Find an object, a chest's item or a sign](find-things.md)
