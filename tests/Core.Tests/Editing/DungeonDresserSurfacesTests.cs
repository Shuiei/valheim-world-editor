using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What hangs on walls and lies about (DungeonDresser.Surfaces): a runner rug is one rug for a short span and a row of
/// pieces with its end pieces for a long one, a hanging the props do not know adds nothing, and no count or density
/// adds nothing.
/// </summary>
public class DungeonDresserSurfacesTests
{
    [Fact]
    public void ARunnerTooShortForTwoPiecesIsOneRug()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var (dresser, o) = DungeonDresserInstance.Of(room);

        PrivateCall.Instance(dresser, "Runner", room.Frame, -2f, 2f, 0f);

        var rug = Assert.Single(o.Items);
        Assert.Contains(rug.Prefab, new[] { "rug_wolf", "rug_fur", "rug_deer" });
    }

    [Fact]
    public void ALongRunnerIsEndPiecesWithMiddlePiecesBetween()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var (dresser, o) = DungeonDresserInstance.Of(room);

        PrivateCall.Instance(dresser, "Runner", room.Frame, -10f, 10f, 0f);

        string[] expected = ["Morkhalla_Rug_end1", "Morkhalla_Rug_middle", "Morkhalla_Rug_middle", "Morkhalla_Rug_end2"];
        Assert.Equal(expected, o.Items.Select(i => i.Prefab));
    }

    [Fact]
    public void AHangingTheProbsDoNotKnowAddsNothing()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var (dresser, o) = DungeonDresserInstance.Of(room);

        PrivateCall.Instance(dresser, "Hang", room.Frame, "no_such_hanging", (0f, 0f, 0f));

        Assert.Empty(o.Items);
    }

    [Fact]
    public void NoHangingsAreMadeForACountOfZero()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var (dresser, o) = DungeonDresserInstance.Of(room);

        PrivateCall.Instance(dresser, "Hangings", room.Frame, 0, Array.Empty<string>());

        Assert.Empty(o.Items);
    }

    [Fact]
    public void NoClutterIsMadeAtADensityOfZero()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var (dresser, o) = DungeonDresserInstance.Of(room);

        PrivateCall.Instance(dresser, "Clutter", room.Frame, 0f, Array.Empty<string>());

        Assert.Empty(o.Items);
    }
}
