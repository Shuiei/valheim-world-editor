namespace TerrainEditor.DocsScreenshots;

/// <summary>The object tools' pictures: Area, copy and paste, Place and Select.</summary>
public sealed partial class Scenes
{
    /// <summary>area.jpg: the Area tool's Remove objects chosen on a box over trees and rocks, not done yet.</summary>
    private void Area(Editor e)
    {
        Bare(e, unsaved: false);
        Trees(e, P(-22, -10), P(22, 12), density: 5, kinds: ["Beech1", "Birch1", "Rock_3", "Bush01"]);
        AreaAction(e, P(-10, -12), P(14, 10), "Remove objects", apply: false);
        Camera(e, 14, 0, 200, 50, 62);
        e.Shot(Out("area.jpg"));
    }

    /// <summary>
    /// area-polygon.jpg: a polygon drawn round part of a wood with the Area tool, and area-polygon-done.jpg: the wood
    /// after Remove objects took out what the polygon held.
    /// </summary>
    private void AreaPolygon(Editor e)
    {
        Bare(e, unsaved: false);
        Trees(e, P(-24, -14), P(24, 14), density: 6, kinds: ["Beech1", "Birch1", "Oak1"]);
        e.Send("key B");
        e.Send("click Polygon");
        foreach (var p in new[] { P(-14, -14), P(6, -18), P(16, -2), P(4, 12), P(-12, 8) })
        {
            Click(e, p);
        }
        e.Send("key Return");
        e.Send("choose Remove objects");
        Camera(e, 12, 0, 200, 55, 70);
        e.Shot(Out("area-polygon.jpg"));
        e.Send("key Return");
        e.Send("click Clear (Esc)");
        e.Shot(Out("area-polygon-done.jpg"));
    }

    /// <summary>area-regrow.jpg: Regrow nature on the bare zone: the game's own trees, bushes and rocks for the biome.</summary>
    private void AreaRegrow(Editor e)
    {
        Bare(e, unsaved: false);
        AreaAction(e, P(-31, -31), P(31, 31), "Regrow nature", apply: true);
        e.Send("wait 1500");
        Camera(e, 10, 0, 200, 58, 120);
        e.Send("wait 500");
        e.Shot(Out("area-regrow.jpg"), keepMessage: true);
    }

    /// <summary>paste.jpg: a copied wood being pasted (one copy), turned, before the click that puts it down.</summary>
    private void Paste(Editor e)
    {
        Bare(e, unsaved: false);
        Trees(e, P(-24, -18), P(-12, -8), density: 8);
        e.Send("key B");
        e.Send("click Box");
        Drag(e, P(-27, -21), P(-9, -5));
        e.Send("key C ctrl");
        e.Send("key V ctrl");
        e.Send("set Copies|1");
        for (int i = 0; i < 2; i++)
        {
            e.Send("key OemComma shift");
        }
        Move(e, P(10, 10));
        Camera(e, 12, 0, 200, 58, 80);
        e.Shot(Out("paste.jpg"));
    }

    /// <summary>paste-stack.jpg: a hut copied and pasted three times upwards with Repeat (stack): a tower.</summary>
    private void PasteStack(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e, P(-14, -12), P(14, 12));
        Hut(e, P(-4, -3), P(4, 3), layers: 1);
        e.Send("key B");
        e.Send("click Box");
        Drag(e, P(-6, -5), P(6, 5));
        e.Send("choose Copy and paste");
        e.Send("key C ctrl");
        e.Send("key V ctrl");
        e.Send("set Copies|3");
        e.Send("choose upwards");
        // Beside the original hut, so the stack stands on its own.
        Move(e, P(-14, 0));
        Camera(e, -2, 0, 200, 22, 40);
        e.Send("wait 300");
        e.Shot(Out("paste-stack.jpg"));
    }

    /// <summary>plant-brush.jpg: a wood planted with the Place brush and its preview under the pointer.</summary>
    private void PlantBrush(Editor e)
    {
        Bare(e, unsaved: false);
        e.Send("key T");
        e.Send("click Brush");
        e.Send("check One at a time, exactly at the cursor|off");
        e.Send("choose Meadows woods");
        Nature(e);
        e.Send("set Brush size|10");
        Drag(e, P(-24, 18), P(-4, 22), steps: 6, hold: 150);
        // Far enough that the preview (a beech is about 20 m tall) does not fill the picture.
        Camera(e, 8, 2, 200, 55, 100);
        Move(e, P(6, -8));
        e.Send("wait 500");
        Move(e, P(6, -7));
        e.Shot(Out("plant-brush.jpg"));
    }

    /// <summary>plant-line.jpg: a fence being placed along a line through points, end to end.</summary>
    private void PlantLine(Editor e)
    {
        Bare(e);
        Level(e);
        e.Send("key T");
        Pieces(e);
        Kinds(e, "wood_fence");
        e.Send("click Line");
        e.Send("click Points");
        foreach (var p in new[] { P(-22, -12), P(-6, -2), P(8, -6), P(20, 10) })
        {
            Click(e, p);
        }
        Move(e, P(20, 10));
        Camera(e, 12, 0, 200, 40, 52);
        e.Shot(Out("plant-line.jpg"));
    }

    /// <summary>
    /// line-shapes.jpg: Line's other shapes, placed: a stake wall Circle and stone walls in a Rectangle two layers high.
    /// </summary>
    private void LineShapes(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e, P(-30, -20), P(30, 20));
        e.Send("key T");
        Pieces(e);
        Kinds(e, "stake_wall");
        e.Send("click Line");
        e.Send("click Circle");
        Drag(e, P(14, 0), P(25, 0));
        e.Send("key Return");
        Kinds(e, "stone_wall_2x1");
        e.Send("click Rectangle");
        e.Send("set Layers|2");
        Drag(e, P(-26, -8), P(-4, 8));
        e.Send("key Return");
        e.Send("key Escape");
        e.Send("key H");
        Camera(e, 0, 0, 200, 40, 64);
        e.Shot(Out("line-shapes.jpg"));
    }

    /// <summary>plant-grid.jpg: carrots and turnips being placed on a turned grid over cultivated ground.</summary>
    private void PlantGrid(Editor e)
    {
        Bare(e);
        Level(e);
        e.Send("key D7");
        e.Send("set Size|4");
        e.Send("set Strength|1");
        for (int dz = -12; dz < 13; dz += 4)
        {
            Path(e, [P(-16, dz), P(16, dz)], steps: 6, hold: 150);
        }
        e.Send("key T");
        Pieces(e);
        Kinds(e, "sapling_carrot", "sapling_turnip");
        e.Send("click Grid");
        e.Send("set Spacing|1.5");
        e.Send("set Rotation|15");
        Drag(e, P(-12, -9), P(12, 9));
        Camera(e, 12, 0, 200, 45, 46);
        e.Shot(Out("plant-grid.jpg"));
    }

    /// <summary>plant-zone.jpg: bushes and young trees for a zone drawn point by point, shown before Enter.</summary>
    private void PlantZone(Editor e)
    {
        Bare(e);
        e.Send("key T");
        Nature(e);
        Kinds(e, "Beech_small1", "Bush01", "RaspberryBush");
        e.Send("click Zone");
        foreach (var p in new[] { P(-20, -14), P(-4, -20), P(16, -12), P(22, 6), P(6, 18), P(-14, 14) })
        {
            Click(e, p);
        }
        Camera(e, -12, 0, 20, 50, 62);
        e.Shot(Out("plant-zone.jpg"));
    }

    /// <summary>place-chooser.jpg: the Place tool's kinds chooser, searching for rocks.</summary>
    private void PlaceChooser(Editor e)
    {
        Bare(e);
        e.Send("key T");
        e.Send("click Choose kinds…");
        e.Send("type Search kinds|rock");
        Camera(e, 0, 0, 200, 40, 55);
        e.Shot(Out("place-chooser.jpg"));
    }

    /// <summary>spawn-points.jpg: spawn points placed by a hut (boars and a draugr), each with its marker.</summary>
    private void SpawnPoints(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e);
        Hut(e, P(-10, -4), P(-2, 4));
        e.Send("key T");
        Pieces(e);
        Kinds(e, "Spawner_Boar");
        Single(e, P(6, -6));
        Single(e, P(10, 4));
        Kinds(e, "Spawner_Draugr");
        Single(e, P(4, 8));
        e.Send("key E");
        Click(e, P(6, -6));
        e.Send("key I");
        Camera(e, 2, 0, 200, 40, 40);
        e.Send("wait 300");
        e.Shot(Out("spawn-points.jpg"));
    }

    /// <summary>select-arrows.jpg: a selected rock and its arrows, the Select panel beside it.</summary>
    private void SelectArrows(Editor e)
    {
        Bare(e);
        Level(e);
        e.Send("key T");
        Nature(e);
        Kinds(e, "Rock_3");
        // Off the zone's middle: an object stands there, and One at a time keeps 0.3 m from any.
        Single(e, P(3, 2));
        e.Send("key E");
        Click(e, P(3, 2));
        Camera(e, 6, 2, 200, 30, 16);
        e.Shot(Out("select-arrows.jpg"));
    }

    /// <summary>select-building.jpg: one wall clicked, then Whole building: the hut's every piece selected.</summary>
    private void SelectBuilding(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e);
        Hut(e, P(-4, -3), P(4, 3));
        e.Send("key E");
        Click(e, P(4, 0));
        e.Send("click Whole building");
        Camera(e, 6, 0, 200, 35, 30);
        e.Shot(Out("select-building.jpg"), keepMessage: true);
    }

    /// <summary>
    /// select-zone-drawing.jpg and select-zone.jpg: a selection ring drawn with Alt around rocks and bushes, then the
    /// selection it made.
    /// </summary>
    private void SelectZone(Editor e)
    {
        Bare(e, unsaved: false);
        Trees(e, P(-20, -8), P(20, 8), density: 6, kinds: ["Rock_3", "Bush01", "Beech_small1", "RaspberryBush"]);
        e.Send("key E");
        (double X, double Z)[] ring = [P(-12, -10), P(6, -12), P(14, -2), P(10, 10), P(-6, 12), P(-14, 2), P(-12, -9)];
        Path(e, ring, steps: 2, hold: 30, mods: "alt", release: false);
        Camera(e, 8, 0, 200, 48, 40);
        e.Shot(Out("select-zone-drawing.jpg"));
        e.Send($"mouse up {ring[^1].X} {ring[^1].Z} left alt");
        e.Send("wait 300");
        e.Shot(Out("select-zone.jpg"), keepMessage: true);
    }

    /// <summary>inspector.jpg: a selected chest in the Inspector, its contents and data.</summary>
    private void Inspector(Editor e)
    {
        Bare(e);
        Level(e);
        e.Send("key T");
        Pieces(e);
        Kinds(e, "piece_chest_wood");
        Single(e, P(3, 2));
        e.Send("key E");
        Click(e, P(3, 2));
        e.Send("key I");
        Camera(e, 0, 2, 200, 35, 10);
        e.Shot(Out("inspector.jpg"));
    }
}
