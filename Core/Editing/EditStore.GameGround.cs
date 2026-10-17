using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public sealed partial class EditStore
{
    /// <summary>What became of a zone's ground sent by the game (see <see cref="TakeGameGround"/>).</summary>
    public enum GameGround
    {
        /// <summary>What the editor has as applied: its own apply coming back.</summary>
        Same,
        /// <summary>A zone without changes of the editor's took the game's ground.</summary>
        Taken,
        /// <summary>A zone with changes kept them; the game's ground waits (GameChanged).</summary>
        Kept,
    }

    /// <summary>
    /// Live: a zone's ground as the game has it now (null: no ground changes there any more). Same: what
    /// the editor has as applied (its own apply coming back); Taken: a zone without changes of the
    /// editor's takes it; Kept: one with changes keeps them, the game's ground waiting (GameChanged).
    /// </summary>
    public GameGround TakeGameGround(int zx, int zz, TerrainZone? z)
    {
        var key = (zx, zz);
        ZoneEdit fresh = z != null && z.ModifiedHeight.Length == Cells && z.ModifiedPaint.Length == Cells ? FromTerrain(z) : new ZoneEdit(zx, zz);
        lock (_lock)
        {
            bool same = _baseline.TryGetValue(key, out ZoneEdit? was) ? was.SameGround(fresh) : fresh.IsEmpty;
            if (same)
            {
                _gameChanged.Remove(key);
                return GameGround.Same;
            }
            Version++;
            if (_zones.TryGetValue(key, out ZoneEdit? e) && e.Changed)
            {
                _gameChanged[key] = fresh;
                return GameGround.Kept;
            }
            Take(key, fresh, z != null);
            return GameGround.Taken;
        }
    }
}
