namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// Hands an object the bridge made, moved or changed to the game's mods as the game hands them an object that comes
    /// from another game: its own data goes through ZDO.Deserialize, which mods patch to learn of objects as they come.
    /// ValheimRAFT's ZdoWatcher keeps the ids of ships and what ropes are tied to so: without it, a host's own game could
    /// not find a pasted ship's pieces by their ids until the world loaded again, and dropped ropes tied to them. The
    /// values are the object's own, set again: nothing changes. Main thread only.
    /// </summary>
    private static void Announce(ZDO zdo)
    {
        ZPackage pkg = new();
        zdo.Serialize(pkg);
        pkg.SetPos(0);
        zdo.Deserialize(pkg);
    }
}
