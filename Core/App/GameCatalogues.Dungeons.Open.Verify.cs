namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        private sealed partial class Open
        {
            /// <summary>
            /// Checks the RandomSpawns read raw against the type tree, the first of each script read whole too.
            /// Throws InvalidDataException when they differ.
            /// </summary>
            /// <param name="raw">The RandomSpawns read raw.</param>
            private void VerifyRawReads(List<long> raw)
            {
                // The first of each script read whole too, to check.
                foreach (long pid in raw.DistinctBy(p => Plan(p)!.Value.Tree))
                {
                    if (B.Field(pid) is not { } whole || SpawnFields.Any(n => !Equals(_spawns[pid].GetValueOrDefault(n), n == "m_OffObject" ? whole["m_OffObject.m_PathID"].AsLong : Get(whole, n, null))))
                    {
                        throw Unexpected();
                    }
                }
            }

            /// <summary>
            /// Checks the first ZNetView's enabled flag read raw against the type tree. Throws InvalidDataException when
            /// they differ.
            /// </summary>
            /// <param name="behaviours">The behaviours that matter.</param>
            private void VerifyEnabled(List<(string Kind, long Pid)> behaviours)
            {
                if (behaviours.FirstOrDefault(x => x.Kind == "netview").Pid is long netview and not 0 && B.Field(netview) is { } nv && Truthy(Get(nv, "m_Enabled", 1L)) != Enabled(netview))
                {
                    throw Unexpected();
                }
            }
        }
    }
}
