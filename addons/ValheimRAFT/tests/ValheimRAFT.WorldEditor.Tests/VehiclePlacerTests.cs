using System.Numerics;
using Xunit;

namespace ValheimRAFT.WorldEditor.Tests;

public sealed class VehiclePlacerTests
{
    private readonly List<SwivelShip.Built> _ship = SwivelShip.Read();

    private static void Same(Quaternion expected, Quaternion actual) =>
        Assert.True(Math.Abs(Quaternion.Dot(expected, actual)) > 1 - 1e-6f, $"{actual} is not {expected}");

    // The probe saw the turned swivel's platform at (0, 0.5, 0, 0.866) and the moving one's 1.5 m up and 0.5 m forward.
    [Fact]
    public void ATurnedSwivelsPartsHangOnItsPlatformTurnedAsTheModTurnedIt()
    {
        var frame = new VehiclePlacer().FrameOf(_ship.Holding(-1285914268).Saved)!;

        Assert.Equal(Vector3.Zero, frame.Offset);
        Same(new Quaternion(0, 0.5f, 0, 0.8660254f), frame.Turn);
    }

    [Fact]
    public void AMovedSwivelsPartsHangOnItsPlatformMovedAsTheModMovedIt()
    {
        var frame = new VehiclePlacer().FrameOf(_ship.Holding(-1285914267).Saved)!;

        Assert.Equal(new Vector3(0, 1.5f, 0.5f), frame.Offset);
        Same(Quaternion.Identity, frame.Turn);
    }

    [Fact]
    public void ASwivelAtItsStartOrFollowingTheWindIsAtRest()
    {
        var swivel = new FakeObject(Keys.Swivel);
        Assert.Null(new VehiclePlacer().FrameOf(swivel));
        swivel.Ints[Keys.SwivelState] = 0;
        Assert.Null(new VehiclePlacer().FrameOf(swivel));
        swivel.Ints[Keys.SwivelState] = 2;
        swivel.Ints[Keys.SwivelMode] = 3;
        Assert.Null(new VehiclePlacer().FrameOf(swivel));
        Assert.Null(new VehiclePlacer().FrameOf(new FakeObject("wood_floor")));
    }

    [Fact]
    public void ATurnedSwivelThatKeepsNoSettingsTurnsAsItsPrefabSays()
    {
        // The prefab's own: turning, about x, 45 degrees.
        var swivel = new FakeObject(Keys.Swivel);
        swivel.Ints[Keys.SwivelState] = 3;

        Same(Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 4), new VehiclePlacer().FrameOf(swivel)!.Turn);
    }

    [Fact]
    public void AParkedLandVehicleStandsAtItsShipsPlaceWithItsOwnTurn()
    {
        var land = _ship.Single(b => b.Saved.Prefab == Keys.LandVehicle);
        var ship = _ship.Holding(-1285914270);
        var parent = new FakeObject(ship.Saved.Prefab, ship.At, ship.Turn);

        var place = new VehiclePlacer().PlaceOf(land.Saved, parent)!;

        Assert.Equal(Vector3.Zero, place.Offset);
        Same(land.Turn, ship.Turn * place.Turn);
        Assert.Null(new VehiclePlacer().PlaceOf(ship.Saved, parent));
    }

    [Fact]
    public void UnityEulerAnglesTurnAboutZThenXThenY()
    {
        var q = VehiclePlacer.Euler(new Vector3(0, 30, 5));
        // The custom mast the fixture's sail hangs on, turned 30 degrees and leaning 5 on its ship.
        var y = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 6);
        var z = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 5 * MathF.PI / 180);
        Same(y * z, q);
    }
}
