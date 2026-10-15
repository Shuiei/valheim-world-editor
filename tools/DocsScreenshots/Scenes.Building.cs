namespace TerrainEditor.DocsScreenshots;

/// <summary>The building pictures: blueprints, the Workshop and dungeons.</summary>
public sealed partial class Scenes
{
    /// <summary>A hut on levelled ground, copied with the Area tool and saved as a blueprint under this name.</summary>
    private static void SaveHut(Editor e, string name, int layers)
    {
        Level(e, P(-12, -10), P(12, 10));
        Hut(e, P(-4, -3), P(4, 3), layers);
        e.Send("key B");
        e.Send("click Box");
        Drag(e, P(-7, -6), P(7, 6));
        e.Send("choose Copy and paste");
        e.Send("key C ctrl");
        e.Send("click Save blueprint…");
        e.Send("wait 500");
        e.Send($"type |{name}");
        e.Send("click Save");
        e.Send("wait 500");
    }

    /// <summary>blueprints.jpg: a small hut saved as a blueprint, in the Blueprints panel.</summary>
    private void Blueprints(Editor e)
    {
        Bare(e, unsaved: false);
        SaveHut(e, "Small hut", 2);
        e.Send("click Blueprints…");
        Camera(e, 10, 0, 200, 40, 40);
        e.Shot(Out("blueprints.jpg"), keepMessage: true);
    }

    /// <summary>
    /// workshop.jpg (a house kept as a blueprint, open in the Workshop, its pieces tinted by the support check),
    /// workshop-library.jpg (the Build panel's Library) and workshop-cut.jpg (Cut at 3 m: inside the house).
    /// </summary>
    private void Workshop(Editor e)
    {
        Bare(e, unsaved: false);
        // Walls four high: the support check tints the upper ones yellow and orange, as the game's build mode would.
        SaveHut(e, "Small house", 4);
        e.Send("click Blueprints");
        e.Send("click Edit");
        e.Send("wait 500");
        e.Send("click Leave anyway");
        e.Send("wait 2500");
        e.Send("key T");
        // The Workshop's plot is centred on 0, 0 (not the world's zone).
        e.Send("camera 5 0 210 24 22");
        e.Shot(Out("workshop.jpg"));
        e.Send("click Library");
        e.Send("wait 500");
        e.Shot(Out("workshop-library.jpg"));
        e.Send("click Pieces");
        e.Send("set Cut: off|3");
        e.Send("camera 5 0 210 50 20");
        e.Send("wait 500");
        e.Shot(Out("workshop-cut.jpg"));
    }

    /// <summary>
    /// workshop-dungeon.jpg: a dungeon made of building pieces by the generator, opened on the Workshop's plot and cut
    /// open to show its rooms.
    /// </summary>
    private void WorkshopDungeon(Editor e)
    {
        WaitForLook(e);
        e.Send("click The Workshop");
        e.Send("click Open the Workshop");
        e.Send("wait 2500");
        e.Send("click Library");
        e.Send("click Generate dungeon…");
        e.Send("choose Black Forest");
        e.Send("choose Medium");
        e.Send("wait 1500");
        e.Send("click Open on the plot");
        e.Send("wait 5000");
        e.Send("set Cut: off|3");
        e.Send("camera 0 0 210 60 90");
        e.Send("wait 1500");
        e.Shot(Out("workshop-dungeon.jpg"));
    }

    /// <summary>
    /// dungeon-generate.jpg (the Dungeon tool's generator and its plan), dungeon-entrance.jpg (the entrance the game
    /// would put, placed on the ground) and dungeon-rooms.jpg (a dungeon of the game's own rooms, 5000 m up, cut 2 m
    /// above the floor, with the panel's room editing: the rooms list, doors, Turn, Delete room, Close open ends).
    /// </summary>
    private void Dungeon(Editor e)
    {
        Bare(e, unsaved: false);
        e.Send("click Dungeon");
        e.Send("choose Black Forest");
        e.Send("choose Medium");
        e.Send("wait 1500");
        // Down to the plan, under the choices.
        e.Scroll(250, 600, 15);
        Camera(e, 0, 0, 200, 40, 60);
        e.Shot(Out("dungeon-generate.jpg"));
        e.Send("click Place in the world");
        e.Send("wait 4000");
        Camera(e, 8, 0, 200, 30, 34);
        e.Send("wait 1000");
        e.Shot(Out("dungeon-entrance.jpg"));
        // The game's own rooms: a dungeon the game lays out, which the panel edits room by room.
        e.Send("key Z ctrl");
        e.Scroll(250, 400, -30);
        e.Send("choose The game's own rooms");
        e.Send("wait 1500");
        e.Send("click Place in the world");
        e.Send("wait 4000");
        e.Send("click Go to the dungeon");
        e.Send("wait 3000");
        e.Scroll(250, 600, 30);
        e.Send("set Cut 4 m above the floor|2");
        e.Send($"camera {Cx - 12} {Cz} 200 62 80 5002");
        e.Send("wait 1500");
        e.Shot(Out("dungeon-rooms.jpg"));
    }
}
