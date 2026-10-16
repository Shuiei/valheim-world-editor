using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The 3D view's spawn point and nest markers: the things are looked through for them only when they change, not at
/// every frame nor when kinds are shown or hidden, and the markers follow what was added or deleted, the kinds worked
/// out again and another area shown.
/// </summary>
public class GlViewSpawnMarkersTests
{
    /// <summary>A new object of this kind at (x, 30, z) in the world.</summary>
    private static NewObject Spawner(string name, float x, float z) => new(0, StableHash.Of(name), new Vector3(x, 30, z), Vector3.Zero, 1f);

    [AvaloniaFact]
    public void TheThingsAreLookedThroughOnlyWhenTheyChange()
    {
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("Spawner_Boar"), new Vector3(30, 30, 30), Vector3.Zero, 0, false));
        var v = new GlView();
        v.Show(s.Scene, null);
        v.ReadModels();
        v.SetShown(ObjectKind.SpawnPoints, true);
        var at = new Vector3(30 - s.Scene.Cx, 32.4f, -(30 - s.Scene.Cz));
        v.SetCamera(at + new Vector3(0, 20, 20), at, 1.5f);
        Assert.Equal([(at, GlView.SpawnColour(false))], v.SpawnMarkers(s.Scene));
        int finds = v.SpawnFinds;
        // Frames with nothing changed: the markers come from what was found, the camera's move included.
        v.SetCamera(at + new Vector3(0, 10, 10), at, 1.5f);
        Assert.Single(v.SpawnMarkers(s.Scene));
        Assert.Single(v.SpawnMarkers(s.Scene));
        Assert.Equal(finds, v.SpawnFinds);
        // Kinds shown or hidden: the markers drawn change, the things are not looked through again.
        v.SetShown(ObjectKind.Trees, false);
        v.SetShown(ObjectKind.SpawnPoints, false);
        v.SetShown(ObjectKind.Nests, true);
        Assert.Empty(v.SpawnMarkers(s.Scene));
        v.SetShown(ObjectKind.SpawnPoints, true);
        Assert.Single(v.SpawnMarkers(s.Scene));
        Assert.Equal(finds, v.SpawnFinds);
        // Another one placed: found again, once.
        var added = s.Commit("Placed", null, [], [(Spawner("Spawner_Boar", 34, 30), false)]);
        Assert.Equal(2, v.SpawnMarkers(s.Scene).Count);
        Assert.Equal(2, v.SpawnMarkers(s.Scene).Count);
        Assert.Equal(finds + 1, v.SpawnFinds);
        // Deleted: its marker goes.
        s.Delete(added);
        Assert.Equal([(at, GlView.SpawnColour(false))], v.SpawnMarkers(s.Scene));
    }

    // What was found is dropped when the things' kinds are worked out again and when another area is shown: the
    // markers are those of the things as they are now, never of the ones looked through before.
    [AvaloniaFact]
    public void TheThingsAreLookedThroughAgainForNewKindsOrAnotherArea()
    {
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("Spawner_Boar"), new Vector3(30, 30, 30), Vector3.Zero, 0, false));
        var v = new GlView();
        v.Show(s.Scene, null);
        v.SetShown(ObjectKind.SpawnPoints, true);
        var at = new Vector3(30 - s.Scene.Cx, 32.4f, -(30 - s.Scene.Cz));
        v.SetCamera(at + new Vector3(0, 20, 20), at, 1.5f);
        // Before the kinds are worked out, nothing is a spawn point.
        Assert.Empty(v.SpawnMarkers(s.Scene));
        v.ReadModels();
        Assert.Equal([(at, GlView.SpawnColour(false))], v.SpawnMarkers(s.Scene));
        // Another area, its spawn point elsewhere.
        var other = TestScenes.Flat(2, new WorldScene.Thing(2, StableHash.Of("Spawner_Boar"), new Vector3(36, 30, 30), Vector3.Zero, 0, false));
        v.Show(other.Scene, null);
        Assert.DoesNotContain((at, GlView.SpawnColour(false)), v.SpawnMarkers(other.Scene));
        v.ReadModels();
        Assert.Equal([(new Vector3(36 - other.Scene.Cx, 32.4f, -(30 - other.Scene.Cz)), GlView.SpawnColour(false))], v.SpawnMarkers(other.Scene));
    }
}
