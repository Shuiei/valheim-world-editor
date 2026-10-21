Valheim World Editor add-ons
============================

An add-on makes the editor work with a mod that brings no editor support of its own: it draws what
the mod makes and gives you the mod's tools in the 3D editor. Each add-on is its own download.

To install one: unpack its .zip here, so that its folder (with its worldeditor.json and its .dll)
is inside this addons folder. It is used while the mod it is for is installed in BepInEx/plugins,
like any other mod (the mod's own .dll stays there; the add-on only teaches the editor about it).
Settings, under Add-ons, lists what is installed and what became of each.

To remove one: delete its folder.

An add-on is code that runs inside the editor with your rights: install only those you trust.
Writing one: https://github.com/Shuiei/valheim-world-editor/blob/main/docs/modding.md#add-ons
