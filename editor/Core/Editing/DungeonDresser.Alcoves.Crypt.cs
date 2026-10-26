namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A crypt's alcove: a sarcophagus across it with a skull beside, a statue between two candles, or bones.</summary>
    private bool CryptAlcove(Frame f, (float U, float V, float Yaw) spot, float bu, float bv)
    {
        float yaw = spot.Yaw;
        switch (_rnd.Next(3))
        {
            case 0:
                if (!Put(f, "crypt_skeleton_chest", bu, bv, yaw + 90, 0.8f, force: true))
                {
                    return false;
                }
                var (su, sv) = Beside(spot, 1.4f);
                Put(f, One("skull_pile", "Skull1", "Candle_resin"), su, sv, R(0, 360), 0.3f);
                return true;
            case 1:
                if (!Put(f, Statue("StatueEvil", "StatueThor", "StatueFreya"), bu, bv, yaw, 0.7f, force: true))
                {
                    return false;
                }
                FlankingCandles(f, spot);
                return true;
            default:
                foreach (int side in new[] { -1, 1 })
                {
                    var (ou, ov) = Beside((bu, bv, yaw), side * 0.9f);
                    Put(f, One("bone_stack", "skull_pile", "LargeBone"), ou, ov, R(0, 360), 0.4f);
                }
                return true;
        }
    }
}
