using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Finding the spawn points and nests still in the scene, with their colour and where their marker goes.
public sealed partial class GlView
{
    /// <summary>Every spawn point and nest of the scene still there: its kind, where its marker goes and its colour.</summary>
    private (ObjectKind Kind, Vector3 At, Vector4 Colour)[] FindSpawns(WorldScene s)
    {
        var found = new List<(ObjectKind, Vector3, Vector4)>();
        var colours = new Dictionary<int, Vector4>();
        lock (s.Things)
        {
            lock (_objLock)
            {
                for (int i = 0; i < s.Things.Count && i < _kinds.Length; i++)
                {
                    var k = _kinds[i];
                    var t = s.Things[i];
                    if (k is not (ObjectKind.SpawnPoints or ObjectKind.Nests) || t.Gone)
                    {
                        continue;
                    }
                    if (!colours.TryGetValue(t.Prefab, out var c))
                    {
                        c = colours[t.Prefab] = k == ObjectKind.Nests ? NestColour : SpawnColour(Terrain.PrefabCatalog.SpawnOf(t.Prefab)?.Respawns == true);
                    }
                    found.Add((k, ToView(s, t.Position + new Vector3(0, 2.4f, 0)), c));
                }
            }
        }
        return [.. found];
    }
}
