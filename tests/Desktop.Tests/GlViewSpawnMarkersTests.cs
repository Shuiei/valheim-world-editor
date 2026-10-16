using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The 3D view's spawn point and nest markers: the things are looked through for them only when they change, not at
/// every frame, and the markers follow what was added or deleted.
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
        // Another one placed: found again, once.
        var added = s.Commit("Placed", null, [], [(Spawner("Spawner_Boar", 34, 30), false)]);
        Assert.Equal(2, v.SpawnMarkers(s.Scene).Count);
        Assert.Equal(2, v.SpawnMarkers(s.Scene).Count);
        Assert.Equal(finds + 1, v.SpawnFinds);
        // Deleted: its marker goes.
        s.Delete(added);
        Assert.Equal([(at, GlView.SpawnColour(false))], v.SpawnMarkers(s.Scene));
    }
}
