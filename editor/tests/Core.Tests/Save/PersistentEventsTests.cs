using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The world's active persistent events, at the end of its .db2 file: read, written back as the game reads them,
/// started and stopped by a save, and left byte for byte by a save that changes none.
/// </summary>
public class PersistentEventsTests
{
    /// <summary>The random events' state the game writes before the list: a timer, no event running, its time and place.</summary>
    private static byte[] Head()
    {
        using MemoryStream ms = new();
        using (BinaryWriter w = new(ms))
        {
            w.Write(12.5f);
            w.Write("");
            w.Write(0f);
            w.Write(0f); w.Write(0f); w.Write(0f);
        }
        return ms.ToArray();
    }

    [Fact]
    public void AListWrittenIsReadBackTheSame()
    {
        List<ActiveEvent> events =
        [
            new(0, 3, new Vector3(103.85f, 56.11f, -1655.39f), 215.86f, 0, -1),
            new(1, 4, new Vector3(-1788.74f, 34.95f, 3353.53f), 150f, 2040, 5963.525390625),
        ];

        var read = PersistentEvents.Read(PersistentEvents.Write(Head(), events));

        Assert.NotNull(read);
        Assert.Equal(Head(), read.Value.Head);
        Assert.Equal(events, read.Value.Events);
    }

    [Fact]
    public void ATailWithoutTheListHasNoEvents()
    {
        Assert.Null(PersistentEvents.Read(Head()));
        Assert.Null(PersistentEvents.Read([]));
        // A length that is not the rest of the file.
        Assert.Null(PersistentEvents.Read([.. Head(), 9, 0, 0, 0, 1, 2]));
    }

    [Fact]
    public void ADamagedListHasNoEvents()
    {
        byte[] tail = PersistentEvents.Write(Head(), [new(0, 1, Vector3.One, 100, 0, -1)]);
        tail[^3] ^= 0xff;
        tail[^6] ^= 0xff;

        Assert.Null(PersistentEvents.Read(tail));
    }

    [Fact]
    public void TheGamesOwnWorldHoldsAnEmptyList()
    {
        using var w = new TempWorld();

        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActiveEvent>>(w.Load().Zones!.Events));
    }

    [Fact]
    public void ASaveStartsAndStopsEvents()
    {
        using var w = new TempWorld();
        var world = w.Load();

        var first = WorldWriter.Save(world, [], events:
        [
            new EventEdit(-1, false, 0, new Vector3(500, 40, 500), 123f, 0),
            new EventEdit(-2, false, 1, new Vector3(-700, 45, 300), 222f, 5000),
        ]);

        Assert.True(first.Saved, first.Message);
        var saved = first.Read!;
        var events = saved.Zones!.Events!;
        // The game's next ids, in the order asked; one with a duration starts at the save's time, one without never ends.
        Assert.Equal([(0, 0, 123f, -1d), (1, 1, 222f, 5000d)], events.Select(e => (e.Id, e.Source, e.Radius, e.Duration)));
        Assert.Equal(new Vector3(500, 40, 500), events[0].Position);
        Assert.Equal((0d, saved.Zones.NetTime), (events[0].StartTime, events[1].StartTime));

        // Stopping one, and one that is not there (skipped), with another started: its id goes on from the highest left.
        var second = WorldWriter.Save(saved, [], events:
        [
            new EventEdit(0, true, 0, events[0].Position, 123f, 0),
            new EventEdit(77, true, 0, default, 1f, 0),
            new EventEdit(-1, false, 2, new Vector3(1, 2, 3), 50f, 0),
        ]);

        Assert.True(second.Saved, second.Message);
        Assert.Equal([(1, 1), (2, 2)], second.Read!.Zones!.Events!.Select(e => (e.Id, e.Source)));
        Assert.Equal(world.ObjectCount, second.Read.ObjectCount);
    }

    [Fact]
    public void ASaveWithoutEventEditsKeepsTheFilesEndByteForByte()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var started = WorldWriter.Save(world, [], events: [new EventEdit(-1, false, 0, new Vector3(5, 6, 7), 80f, 0)]).Read!;
        byte[] before = File.ReadAllBytes(Path.Combine(w.Dir, $"_main.{started.SaveNumber}.db2"));
        var t = started.TerrainZones[0];

        var reset = WorldWriter.Save(started, [], resets: [new ZoneReset(t.ZoneX, t.ZoneZ, KeepBuildings: true, Ground: false)]);

        Assert.True(reset.Saved, reset.Message);
        byte[] after = File.ReadAllBytes(Path.Combine(w.Dir, $"_main.{reset.Number}.db2"));
        int tail = PersistentEvents.Write(Head(), started.Zones!.Events!).Length - Head().Length;
        Assert.Equal(before[^tail..], after[^tail..]);
        Assert.Single(reset.Read!.Zones!.Events!);
    }

    [Fact]
    public void AWorldWithoutAnEventListRefusesEventEdits()
    {
        using var w = new TempWorld();
        var world = w.Load();
        // The file's end cut down to the random events' state, as an older game wrote it.
        string db = Path.Combine(w.Dir, $"_main.{world.SaveNumber}.db2");
        byte[] bytes = File.ReadAllBytes(db);
        int block = PersistentEvents.Write([], []).Length;
        File.WriteAllBytes(db, bytes[..^block]);
        var old = w.Load();
        Assert.Null(old.Zones!.Events);

        var result = WorldWriter.Save(old, [], events: [new EventEdit(-1, false, 0, default, 100f, 0)]);

        Assert.False(result.Saved);
        Assert.Equal(old.SaveNumber, w.Load().SaveNumber);
    }

    [Fact]
    public void TheStoreKeepsEachStartAndStopOnce()
    {
        using var w = new TempWorld();
        var edits = new EditStore(w.Load());
        int a = edits.NewEventId(), b = edits.NewEventId();
        Assert.True(a < 0 && b < a);
        var start = new EventEdit(a, false, 0, default, 100f, 0);
        var stop = new EventEdit(3, true, 0, default, 100f, 0);

        edits.SetEvent(start, true);
        edits.SetEvent(start, true);
        edits.SetEvent(stop, true);
        Assert.Equal(2, edits.EventCount);
        // An id taken by a start put back (a redo) is not given again.
        edits.SetEvent(new EventEdit(-40, false, 0, default, 100f, 0), true);
        Assert.True(edits.NewEventId() < -40);

        edits.SetEvent(start, false);
        Assert.Equal([stop.Id, -40], edits.Events.Select(e => e.Id));
    }
}
