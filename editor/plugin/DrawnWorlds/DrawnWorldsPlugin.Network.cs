using HarmonyLib;

namespace DrawnWorlds;

/// <summary>
/// A drawn world played with others: every player's game makes its own ground, so each needs the map. A player's game
/// with this mod says so as it connects (before anything else it sends); the server of a drawn world sends it the map
/// just before the world's details (its seed...), from which the game starts its generator, so the map is there in
/// time. A player's game without the mod would make another world on the same seed: the server turns it away as the
/// game turns away another version.
/// </summary>
public sealed partial class DrawnWorldsPlugin
{
    /// <summary>What a game with the mod says as it connects.</summary>
    private const string Hello = "DrawnWorlds_Hello";

    /// <summary>The map the server sends.</summary>
    private const string MapRpc = "DrawnWorlds_Map";

    /// <summary>The connections whose game said it has the mod (on a server).</summary>
    private static readonly HashSet<ZRpc> WithMod = [];

    /// <summary>The map the server sent (on a player's game), or null: the world it joins is not drawn.</summary>
    internal static byte[] Received;

    /// <summary>A connection: the mod's calls, and (joining a server) the word that this game has the mod.</summary>
    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    private static class NewConnection
    {
        private static void Prefix(ZNet __instance, ZNetPeer peer)
        {
            if (__instance.IsServer())
            {
                peer.m_rpc.Register(Hello, rpc => WithMod.Add(rpc));
                return;
            }
            Received = null;
            peer.m_rpc.Register<ZPackage>(MapRpc, (_, pkg) =>
            {
                Received = pkg.ReadByteArray();
                Log.LogInfo($"The server's world is drawn: its map received ({Received.Length / 1024} KB).");
            });
            peer.m_rpc.Invoke(Hello);
        }
    }

    /// <summary>A player joining a drawn world without the mod is turned away, as the game turns away another version.</summary>
    [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
    private static class PeerInfo
    {
        private static bool Prefix(ZNet __instance, ZRpc rpc)
        {
            if (!__instance.IsServer() || MapOf(ZNet.World) == null || WithMod.Contains(rpc))
            {
                return true;
            }
            Log.LogWarning($"A player without DrawnWorlds was turned away ({rpc.GetSocket().GetEndPointString()}): a drawn world needs the mod on every player's game.");
            rpc.Invoke("Error", (int)ZNet.ConnectionStatus.ErrorVersion);
            return false;
        }
    }

    /// <summary>The server of a drawn world sends the map before the world's details.</summary>
    [HarmonyPatch(typeof(ZNet), "SendPeerInfo")]
    private static class SendMap
    {
        private static void Prefix(ZNet __instance, ZRpc rpc)
        {
            if (__instance.IsServer() && MapOf(ZNet.World) is { } kept)
            {
                var pkg = new ZPackage();
                pkg.Write(kept);
                rpc.Invoke(MapRpc, pkg);
            }
        }
    }

    /// <summary>A connection gone: forgotten.</summary>
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
    private static class Gone
    {
        private static void Prefix(ZNetPeer peer) => WithMod.Remove(peer.m_rpc);
    }
}
