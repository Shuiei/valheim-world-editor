using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>The object tools' pictures: Area, copy and paste, Place and Select.</summary>
public sealed partial class Scenes
{
    /// <summary>
    /// area.jpg: the Region tool's Select objects chosen on a box round a dense clump of trees, rocks, bushes and
    /// mushrooms (Pickables shown in View), not done yet: the kind chips count what is inside.
    /// </summary>
    private void Area(Editor e)
    {
        Bare(e, unsaved: false, view: true);
        e.Send("click Pickables");
        e.Send("click View");
        string[] kinds = ["Beech1", "Birch1", "Oak1", "Rock_3", "Bush01", "Pickable_Mushroom"];
        for (int dz = -12; dz <= 12; dz += 6)
        {
            Trees(e, P(-16, dz), P(16, dz), density: 5, kinds: kinds);
        }
        AreaAction(e, P(-14, -13), P(14, 13), "Select objects", apply: false);
        Camera(e, 10, 0, 200, 55, 64);
        e.Send("wait 500");
        e.Shot(Out("area.jpg"));
    }

    /// <summary>
    /// area-polygon.jpg: a polygon drawn round part of a wood with the Region tool, and area-polygon-done.jpg: the wood
    /// after Select objects handed what the polygon held to the Select tool, and Delete took it out.
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
        RegionAction(e, "Select objects");
        Camera(e, 12, 0, 200, 55, 70);
        e.Shot(Out("area-polygon.jpg"));
        e.Send("key Return");
        e.Send("key Delete");
        e.Shot(Out("area-polygon-done.jpg"));
    }

    /// <summary>
    /// area-regrow.jpg: a bare box on the bare zone, then the same box after Regrow nature, same camera: the game's own
    /// trees, bushes and rocks for the biome, and the status bar saying what grew.
    /// </summary>
    private void AreaRegrow(Editor e)
    {
        Bare(e, unsaved: false);
        AreaAction(e, P(-18, -18), P(18, 18), "Regrow nature", apply: false);
        Camera(e, 29, 0, 200, 42, 80);
        e.Send("wait 500");
        string before = Out("before-area-regrow.jpg"), after = Out("after-area-regrow.jpg");
        e.Shot(before);
        e.Send("key Return");
        WaitForMessage(e, "Regrew");
        e.Shot(after, keepMessage: true);
        // The message takes two lines: the halves end above it. The cut leaves the Mask card out; it is kept once.
        Halves(Out("area-regrow.jpg"), RegrowPair, before, after, new HalvesCut(1000, AreaPanelRight, 830, [MaskCard]));
    }

    /// <summary>area-regrow.jpg's halves.</summary>
    private static readonly Pair RegrowPair = new("Bare", "After Regrow nature");

    /// <summary>
    /// paste.jpg: a copied wood of a few dozen trees and rocks being pasted (one copy), turned 30°, over open grass,
    /// before the click that puts it down.
    /// </summary>
    private void Paste(Editor e)
    {
        Bare(e, unsaved: false, size: 3);
        // The woods round the bare zone taken away: the paste lands on open grass.
        RemoveObjects(e, P(-95, -95), P(95, 95));
        for (int dz = -16; dz <= 16; dz += 8)
        {
            Trees(e, P(-18, dz), P(18, dz), density: 3, kinds: ["Beech1", "Birch1", "Oak1", "Rock_3", "Rock_7"]);
        }
        e.Send("key B");
        e.Send("click Box");
        Drag(e, P(-22, -20), P(22, 20));
        RegionAction(e, "Copy and paste");
        e.Send("key C ctrl");
        e.Send("key V ctrl");
        e.Send("set Copies|1");
        // 30°: two steps of 15° (the building bar's Turn by).
        e.Send("set Turn by|15°");
        for (int i = 0; i < 2; i++)
        {
            e.Send("key OemComma");
        }
        e.Send("set Turn by|22.5°");
        Camera(e, -44, 0, 200, 42, 110);
        e.Send("wait 300");
        Move(e, P(-62, 1));
        e.Send("wait 300");
        Move(e, P(-62, 0));
        e.Send("wait 500");
        e.Shot(Out("paste.jpg"));
    }

    /// <summary>
    /// paste-stack.jpg: a hut with a floor and a flat roof, copied and pasted three times upwards with Repeat (stack),
    /// seen from the ground: beside the original, the three floors one click placed.
    /// </summary>
    private void PasteStack(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e, P(-16, -12), P(16, 12));
        Hut(e, P(-3, -3), P(3, 3), layers: 1);
        // The roof: floor pieces on the walls' tops, 2 m up (four lifts of half a metre).
        BuildFloor(e, "wood_floor", P(-3, -3), P(3, 3), halfMetresUp: 4);
        e.Send("key B");
        e.Send("click Box");
        Drag(e, P(-5, -5), P(5, 5));
        RegionAction(e, "Copy and paste");
        e.Send("key C ctrl");
        e.Send("key V ctrl");
        e.Send("set Copies|3");
        e.Send("choose upwards");
        Camera(e, -5, 0, 200, 12, 30);
        e.Send("wait 300");
        // Beside the original hut, so the stack stands on its own.
        Move(e, P(-10, 1));
        e.Send("wait 300");
        Move(e, P(-10, 0));
        e.Send("wait 300");
        e.Send($"mouse down {P(-10, 0).X} {P(-10, 0).Z}");
        e.Send($"mouse up {P(-10, 0).X} {P(-10, 0).Z}");
        e.Send("wait 500");
        e.Shot(Out("paste-stack.jpg"));
    }

    /// <summary>
    /// plant-brush.jpg: the Meadows woods preset in the Place brush over open grass, seen from low down: the
    /// see-through trees and bushes inside the circle are what a click places.
    /// </summary>
    private void PlantBrush(Editor e)
    {
        BrushOverGrass(e);
        // The circle right of the panel.
        Camera(e, 8, 0, 200, 28, 46);
        Move(e, P(0, 1));
        e.Send("wait 300");
        Move(e, P(0, 0));
        e.Send("wait 500");
        e.Shot(Out("plant-brush.jpg"));
    }

    /// <summary>
    /// place-brush-before-after.jpg: the same brush, before the click (the preview in its circle) and after it (the
    /// trees standing on the same spot), side by side.
    /// </summary>
    private void PlaceBrushBeforeAfter(Editor e)
    {
        BrushOverGrass(e);
        // Both halves with the Place tool: the View tool shows no preview, and its click places nothing.
        // The halves are cut right of the Mask card, which sits further right beside the wider Place panel; the panel's
        // right edge is kept over the before half. The circle in the middle of the before half.
        var at = P(-8, 0);
        Camera(e, 29, 0, 200, 25, 100);
        Move(e, P(-8, 1));
        e.Send("wait 300");
        Move(e, at);
        e.Send("wait 500");
        var (before, after) = ClickBetweenShots(e, at, "place-brush-before-after.jpg", keepMessage: false);
        Halves(Out("place-brush-before-after.jpg"), OneClickPair, before, after,
            new HalvesCut(0, Keep: [new SKRectI(GroundPairLeft, GroundPairTop, 452, 842)], From: 770));
    }

    /// <summary>
    /// The Place brush with the Meadows woods preset, Brush size 10 m, its circle in the middle of the view over bare
    /// grass, the camera about 10 m up and 20 m back.
    /// </summary>
    private void BrushOverGrass(Editor e)
    {
        Bare(e, unsaved: false);
        e.Send("key T");
        e.Send("click Brush");
        e.Send("check One at a time, exactly at the cursor|off");
        e.Send("choose Meadows woods");
        e.Send("set Brush size|10");
        Camera(e, 0, 0, 200, 28, 46);
        e.Send("wait 300");
        Move(e, P(0, 1));
        e.Send("wait 300");
        Move(e, P(0, 0));
        e.Send("wait 500");
    }

    /// <summary>
    /// place-line.jpg: birches every 4 m along a smooth curve through five points beside a paved path (place.md), shown
    /// before Enter with the Line panel beside it.
    /// </summary>
    private void PlaceLine(Editor e)
    {
        Bare(e);
        Level(e);
        // A paved path for the row of trees to follow.
        e.Send("key D8");
        e.Send("set Size|2");
        e.Send("set Strength|1");
        Path(e, [P(-24, -6), P(-10, 0), P(4, -2), P(14, 6), P(24, 8)], steps: 8, hold: 150);
        e.Send("key T");
        Kinds(e, "Birch1");
        e.Send("click Line");
        e.Send("click Points");
        e.Send("set Every|4");
        e.Send("check Smooth curve through the points|on");
        foreach (var p in new[] { P(-24, -11), P(-10, -5), P(4, -7), P(14, 1), P(24, 3) })
        {
            Click(e, p);
        }
        Move(e, P(24, 20));
        Camera(e, 10, 0, 200, 42, 56);
        e.Shot(Out("place-line.jpg"));
    }

    /// <summary>plant-line.jpg: a fence being laid with Build along a line through points, end to end (build.md).</summary>
    private void PlantLine(Editor e)
    {
        Bare(e);
        Level(e);
        Piece(e, "wood_fence");
        Lay(e, "Line");
        e.Send("set Layers|1");
        foreach (var p in new[] { P(-22, -12), P(-6, -2), P(8, -6), P(20, 10) })
        {
            Click(e, p);
        }
        Move(e, P(20, 10));
        Camera(e, 12, 0, 200, 40, 52);
        e.Shot(Out("plant-line.jpg"));
        e.Send("click Clear shape (Esc)");
        Lay(e, "One piece");
    }

    /// <summary>
    /// line-shapes.jpg: Build's other ways to lay a piece end to end: a stake wall Circle and stone walls in a Rectangle
    /// two layers high, seen from the side; the dock's foot shows Rectangle and Layers 2 (build.md).
    /// </summary>
    private void LineShapes(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e, P(-30, -20), P(30, 20));
        BuildWalls(e, "stake_wall", P(10, 0), P(18, 0), shape: "Circle");
        Piece(e, "stone_wall_2x1");
        Lay(e, "Rectangle");
        e.Send("set Layers|2");
        Drag(e, P(-18, -6), P(-4, 6));
        e.Send("click Place (Enter)");
        // The placed rectangle stays drawn for another placing: cleared, the pieces show as placed.
        e.Send("click Clear shape (Esc)");
        Camera(e, 2, 0, 200, 22, 44);
        e.Send("wait 300");
        Move(e, P(0, 30));
        e.Shot(Out("line-shapes.jpg"));
        Lay(e, "One piece");
    }

    /// <summary>
    /// plant-grid.jpg: carrots and turnips being placed on a grid turned 15° over cultivated ground, seen from close
    /// above so each cell's crop shows.
    /// </summary>
    private void PlantGrid(Editor e)
    {
        Bare(e);
        Level(e);
        e.Send("key D7");
        e.Send("set Size|4");
        e.Send("set Strength|1");
        for (int dz = -7; dz < 8; dz += 3)
        {
            Path(e, [P(-10, dz), P(10, dz)], steps: 6, hold: 150);
        }
        e.Send("key T");
        Pieces(e);
        Kinds(e, "sapling_carrot", "sapling_turnip");
        e.Send("click Grid");
        e.Send("set Spacing|1");
        e.Send("set Rotation|15");
        Camera(e, 2, 0, 200, 58, 17);
        e.Send("wait 300");
        Drag(e, P(-6, -4), P(6, 4));
        Move(e, P(0, 30));
        e.Shot(Out("plant-grid.jpg"));
    }

    /// <summary>plant-zone.jpg: bushes and young trees for a polygon drawn point by point, shown before Enter.</summary>
    private void PlantZone(Editor e)
    {
        Bare(e);
        e.Send("key T");
        Nature(e);
        Kinds(e, "Beech_small1", "Bush01", "RaspberryBush");
        e.Send("click Polygon");
        foreach (var p in new[] { P(-20, -14), P(-4, -20), P(16, -12), P(22, 6), P(6, 18), P(-14, 14) })
        {
            Click(e, p);
        }
        Camera(e, -12, 0, 20, 50, 62);
        e.Shot(Out("plant-zone.jpg"));
    }

    /// <summary>
    /// place-chooser.jpg: the Place tool's Choose kinds card searching "rock", BigRock ticked and placed once: the Rocks
    /// category counts the ticked kind, BigRock heads the Recent row.
    /// </summary>
    private void PlaceChooser(Editor e)
    {
        Bare(e);
        e.Send("key T");
        Pieces(e);
        Kinds(e, "BigRock");
        Single(e, P(3, 2));
        Move(e, P(0, 80));
        e.Send("click Choose kinds…");
        e.Send("type Search kinds|rock");
        Camera(e, -10, 2, 200, 40, 30);
        e.Send("wait 300");
        e.Shot(Out("place-chooser.jpg"));
    }

    /// <summary>
    /// spawn-points.jpg: close on spawn points placed by a hut (two boars and a draugr), each a box with its marker, the
    /// draugr one selected and open in the Inspector.
    /// </summary>
    private void SpawnPoints(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e);
        Hut(e, P(-10, -4), P(-4, 2));
        e.Send("key T");
        Pieces(e);
        Kinds(e, "Spawner_Boar");
        Single(e, P(0, -3));
        Single(e, P(3, 2));
        Kinds(e, "Spawner_Draugr");
        Single(e, P(-2, 4));
        e.Send("key E");
        Camera(e, 1, 0, 200, 30, 16);
        e.Send("wait 300");
        Click(e, P(-2, 4));
        e.Send("key I");
        e.Send("wait 300");
        e.Shot(Out("spawn-points.jpg"));
    }

    /// <summary>
    /// guide-forest-zone.jpg: Place's Polygon mode with the Meadows woods preset, Clumping 50% and Patch size 30 m: a polygon
    /// about 60 × 40 m previewed in groves and clearings, the panel's Polygon fill rows in view.
    /// </summary>
    private void GuideForestZone(Editor e)
    {
        Bare(e, unsaved: false);
        e.Send("key T");
        e.Send("choose Meadows woods");
        e.Send("click Polygon");
        e.Send("set Density|1.5");
        e.Send("set Clumping|50");
        e.Send("set Patch size|30");
        Camera(e, 6, 0, 200, 72, 100);
        e.Send("wait 300");
        foreach (var p in new[] { P(-29, -20), P(29, -20), P(29, 20), P(-29, 20) })
        {
            Click(e, p);
        }
        Move(e, P(0, 80));
        // The panel scrolled down to its Polygon fill rows.
        e.Scroll(270, 600, 6);
        e.Send("wait 500");
        e.Shot(Out("guide-forest-zone.jpg"));
    }

    /// <summary>select-building.jpg: one wall clicked, then Whole building: the hut's every piece selected.</summary>
    private void SelectBuilding(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e);
        Hut(e, P(-4, -3), P(4, 3));
        e.Send("key E");
        Settle(e);
        Click(e, P(4, 0));
        e.Send("click Whole building");
        Camera(e, 6, 0, 200, 35, 30);
        e.Shot(Out("select-building.jpg"), keepMessage: true);
    }

    /// <summary>A hut with a chest and a fire pit inside, seen from above at an angle: the Select tool on.</summary>
    private void HutInView(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e);
        Hut(e, P(-6, -4), P(6, 4));
        // On the floor: put down after it (Build puts them on the piece pointed at).
        BuildOne(e, "piece_chest_wood", P(-4, 2));
        BuildOne(e, "fire_pit", P(-2, -2));
        e.Send("key E");
        Camera(e, 6, 0, 200, 40, 30);
        // A frame drawn with this camera: the mouse's points are read with the last frame's.
        e.Send("wait 300");
    }

    /// <summary>
    /// select-pick.jpg: Pick Built pieces only, a box over a hut among rocks, bushes and young trees: the hut alone.
    /// </summary>
    private void SelectPick(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e);
        Trees(e, P(-14, -9), P(14, 9), density: 3, kinds: ["Rock_3", "Bush01", "Beech_small1"]);
        Hut(e, P(-6, -4), P(6, 4));
        e.Send("key E");
        e.Send("choose Built pieces only");
        Camera(e, 6, 0, 200, 55, 34);
        e.Send("wait 300");
        Drag(e, P(12, -10), P(-12, 10), mods: "alt");
        e.Send("wait 300");
        e.Shot(Out("select-pick.jpg"), keepMessage: true);
    }

    /// <summary>
    /// select-zone-drawing.jpg and select-zone.jpg: with "A drag draws: An outline on the ground", a selection ring drawn
    /// with Alt around rocks and bushes, then the selection it made.
    /// </summary>
    private void SelectZone(Editor e)
    {
        Bare(e, unsaved: false);
        Trees(e, P(-20, -8), P(20, 8), density: 6, kinds: ["Rock_3", "Bush01", "Beech_small1", "RaspberryBush"]);
        e.Send("key E");
        e.Send("choose An outline on the ground (all above it)");
        (double X, double Z)[] ring = [P(-12, -10), P(6, -12), P(14, -2), P(10, 10), P(-6, 12), P(-14, 2), P(-12, -9)];
        Path(e, ring, steps: 2, hold: 30, mods: "alt", release: false);
        Camera(e, 8, 0, 200, 48, 40);
        e.Shot(Out("select-zone-drawing.jpg"));
        e.Send($"mouse up {ring[^1].X} {ring[^1].Z} left alt");
        e.Send("wait 300");
        e.Shot(Out("select-zone.jpg"), keepMessage: true);
    }
}
