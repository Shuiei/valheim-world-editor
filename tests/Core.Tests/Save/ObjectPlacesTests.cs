using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Objects found again by kind and place (ObjectPlaces): to the centimetre, each once, two at one place in turn, and
/// every object of a save by its id there.
/// </summary>
public class ObjectPlacesTests
{
    [Fact]
    public void AnObjectIsFoundByItsKindAndPlaceOnce()
    {
        var places = new ObjectPlaces();
        var at = new Vector3(10.12f, 30f, -4.5f);
        places.Add(1, 7, at);
        places.Add(2, 7, at);
        places.Add(3, 8, at);
        // Within the centimetre it is the same place; another kind or another centimetre is not.
        Assert.Equal(1, places.Take(7, at + new Vector3(0.002f, 0f, 0f)));
        Assert.Equal(2, places.Take(7, at));
        Assert.Null(places.Take(7, at));
        Assert.Null(places.Take(8, at + new Vector3(0.02f, 0f, 0f)));
        Assert.Equal(3, places.Take(8, at));
    }

    [Fact]
    public void EveryObjectOfASaveIsFoundByItsId()
    {
        using var w = new TempWorld();
        var save = w.Load();
        var places = ObjectPlaces.Of(save);
        var first = new Dictionary<(int, Vector3), int>();
        for (int id = 0; id < save.ObjectRefs.Count; id++)
        {
            first.TryAdd((save.ObjectRefs[id].Prefab, save.ObjectRefs[id].Position), id);
        }
        foreach (var ((prefab, position), id) in first)
        {
            Assert.Equal(id, places.Take(prefab, position));
        }
    }
}
