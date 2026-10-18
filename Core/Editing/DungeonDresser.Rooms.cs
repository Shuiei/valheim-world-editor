using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- The rooms.
    /// <summary>Dresses a space for what it is, and puts the key chest and its guard in if it holds the key.</summary>
    private void Furnish(Frame f)
    {
        _lights = 0;
        _decayed = false;
        if (f.S.Corridor)
        {
            Corridor(f);
            Nooks(f);
            Finish(f);
            return;
        }
        if (Sparse(f))
        {
            Bare(f);
            Nooks(f);
            Finish(f);
            return;
        }
        if (RoomDressers.TryGetValue(f.S.Room, out var dress))
        {
            dress(f);
        }
        if (f.S.KeyChest)
        {
            KeyChest(f);
            Elite(f, 0, f.HalfD * 0.3f, 180);
        }
        Nooks(f);
        Finish(f);
    }

    /// <summary>
    /// Each room kind's own dressing, by the kind. A kind not listed has none: the room is left as it is.
    /// </summary>
    private Dictionary<Room, Action<Frame>> RoomDressers => new()
    {
        [Room.Entrance] = Entrance,
        [Room.Hall] = Hall,
        [Room.Hub] = Hub,
        [Room.Gallery] = Gallery,
        [Room.Burial] = Burial,
        [Room.Ossuary] = Ossuary,
        [Room.Chapel] = Chapel,
        [Room.Shrine] = Shrine,
        [Room.Tomb] = Tomb,
        [Room.Throne] = Throne,
        [Room.Barracks] = Barracks,
        [Room.Armory] = Armory,
        [Room.Feast] = Feast,
        [Room.Kitchen] = Kitchen,
        [Room.Store] = Store,
        [Room.Forge] = Forge,
        [Room.Library] = Library,
        [Room.Cells] = Cells,
        [Room.Torture] = Torture,
        [Room.Guard] = Guard,
        [Room.Treasury] = Treasury,
        [Room.Boss] = Boss,
        [Room.Antechamber] = Antechamber,
        [Room.Den] = Den,
        [Room.Totem] = Totem,
        [Room.Mine] = Mine,
        [Room.Workshop] = Workshop,
        [Room.Quarters] = Quarters,
        [Room.Secret] = Secret,
        [Room.StairTop] = Stairwell,
        [Room.StairBottom] = Stairwell,
    };

    /// <summary>
    /// The last touches, once everything that keeps a spot stands: the lights the room asked for, then its decay.
    /// Each from a stream of its own, neither keeping a spot, so that the settings' Light and Decay change nothing
    /// else (decay's rubble may lie over an unkept torch's foot; the torch is thin).
    /// </summary>
    private void Finish(Frame f)
    {
        if (f.S.Corridor)
        {
            CorridorLights(f, Local(f, 1));
            var decay = Local(f, 2);
            if (_decay > 0.3f && decay.NextDouble() < _decay * 0.3 && Spot(f, 0.8f, rnd: decay) is var (ru, rv))
            {
                string[] heaps = ["Morkhalla_Rubble2", "Morkhalla_Rubble3", "Morkhalla_Rubble1"];
                Put(f, heaps[decay.Next(heaps.Length)], ru, rv, (float)decay.NextDouble() * 360, 0.9f, take: false);
            }
            Growths(f);
            return;
        }
        if (_lights > 0)
        {
            PlaceLights(f, _lights == 2);
        }
        // Growths before the decay: neither keeps a spot, and the growths must not move with the Decay setting.
        Growths(f);
        if (_decayed)
        {
            PlaceDecay(f);
        }
    }
}
