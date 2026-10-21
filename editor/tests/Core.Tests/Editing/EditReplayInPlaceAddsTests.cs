using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The added objects waiting for a deleted or replaced object's place: the newest takes a place first, each place takes one,
/// an object that came from a source is not a candidate, and what a place takes is attached to it.
/// </summary>
public class EditReplayInPlaceAddsTests
{
    private static readonly Vector3 Place = new(10, 0, 10);

    [Fact]
    public void ThePlaceTakesTheNewestAddedObjectFirst()
    {
        var pool = new EditReplay.InPlaceAdds([new NewObject(-1, 7, Place, Vector3.Zero, 0f), new NewObject(-2, 7, Place, Vector3.Zero, 0f)]);

        Assert.Equal(-1, Assert.Single(pool.Take(7, Place)).Id);
    }

    [Fact]
    public void EachPlaceTakesOneObjectOnly()
    {
        var pool = new EditReplay.InPlaceAdds([new NewObject(-1, 7, Place, Vector3.Zero, 0f), new NewObject(-2, 7, Place, Vector3.Zero, 0f)]);
        pool.Take(7, Place);

        Assert.Equal(-2, Assert.Single(pool.Take(7, Place)).Id);
        Assert.Empty(pool.Take(7, Place));
    }

    [Fact]
    public void AnObjectWithASourceIsNotAPlaceCandidate()
    {
        var pool = new EditReplay.InPlaceAdds([new NewObject(-1, 7, Place, Vector3.Zero, 0f, SourceId: 3)]);

        Assert.Empty(pool.Take(7, Place));
    }

    [Fact]
    public void WhatAPlaceTakesIsAttachedToIt()
    {
        var added = new NewObject(-1, 7, Place, Vector3.Zero, 0f);
        var pool = new EditReplay.InPlaceAdds([added]);

        pool.Take(7, Place);

        Assert.Contains(added, pool.Attached);
    }
}
