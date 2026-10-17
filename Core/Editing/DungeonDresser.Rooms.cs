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
        switch (f.S.Room)
        {
            case Room.Entrance: Entrance(f); break;
            case Room.Hall: Hall(f); break;
            case Room.Hub: Hub(f); break;
            case Room.Gallery: Gallery(f); break;
            case Room.Burial: Burial(f); break;
            case Room.Ossuary: Ossuary(f); break;
            case Room.Chapel: Chapel(f); break;
            case Room.Shrine: Shrine(f); break;
            case Room.Tomb: Tomb(f); break;
            case Room.Throne: Throne(f); break;
            case Room.Barracks: Barracks(f); break;
            case Room.Armory: Armory(f); break;
            case Room.Feast: Feast(f); break;
            case Room.Kitchen: Kitchen(f); break;
            case Room.Store: Store(f); break;
            case Room.Forge: Forge(f); break;
            case Room.Library: Library(f); break;
            case Room.Cells: Cells(f); break;
            case Room.Torture: Torture(f); break;
            case Room.Guard: Guard(f); break;
            case Room.Treasury: Treasury(f); break;
            case Room.Boss: Boss(f); break;
            case Room.Antechamber: Antechamber(f); break;
            case Room.Den: Den(f); break;
            case Room.Totem: Totem(f); break;
            case Room.Mine: Mine(f); break;
            case Room.Workshop: Workshop(f); break;
            case Room.Quarters: Quarters(f); break;
            case Room.Secret: Secret(f); break;
            case Room.StairTop or Room.StairBottom: Stairwell(f); break;
            default:
                break;
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
