using System.Numerics;

namespace TerrainEditor.Desktop;

// Marks over the objects: the new objects' green dots, the spawn points' and nests' markers, and the players
// online (live).
public sealed partial class GlView
{
    /// <summary>
    /// Live: the players online, in world metres (blue posts with a beam up; their names are labels
    /// the window places over the view).
    /// </summary>
    internal IReadOnlyList<(string Name, Vector3 World)> Players
    {
        get; set
        {
            field = value;
            Wake();
        }
    } = [];

    /// <summary>The players' posts on the graphics card.</summary>
    private uint _playersVao, _playersVbo;

    /// <summary>A player's post: a body 1.9 m tall (rings and sides) and a 40 m beam, in view space.</summary>
    internal static float[] PlayerPosts(WorldScene s, IEnumerable<(string Name, Vector3 World)> players)
    {
        var data = new List<float>();
        void Seg(Vector3 p, Vector3 q) => data.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
        foreach (var (_, w) in players)
        {
            var c = ToView(s, w);
            for (int i = 0; i < 12; i++)
            {
                float a0 = i * MathF.Tau / 12, a1 = (i + 1) * MathF.Tau / 12;
                var p0 = new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0)) * 0.45f;
                var p1 = new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1)) * 0.45f;
                Seg(c + p0, c + p1);
                Seg(c + p0 + new Vector3(0, 1.9f, 0), c + p1 + new Vector3(0, 1.9f, 0));
                if (i % 3 == 0)
                {
                    Seg(c + p0, c + p0 + new Vector3(0, 1.9f, 0));
                }
            }
            Seg(c, c + new Vector3(0, 40, 0));
        }
        return [.. data];
    }

    /// <summary>The live players' posts, light blue.</summary>
    private void DrawPlayers(WorldScene s, Matrix4x4 vp)
    {
        if (Players.Count == 0)
        {
            return;
        }
        DrawLines(ref _playersVao, ref _playersVbo, PlayerPosts(s, Players), vp, new Vector4(0.31f, 0.76f, 1, 1));
    }

    /// <summary>
    /// Whether a new object (id &lt; 0) is in the world already (saved, or live: applied); such objects lose their
    /// marker. Call <c>RefreshNewMarkers</c> when that changes.
    /// </summary>
    public Func<int, bool> InGame { get; set; } = _ => false;

    /// <summary>The green marks on new objects can be hidden (View: Unsaved marks).</summary>
    public bool ShowNewMarkers { get; set; } = true;

    /// <summary>
    /// The new objects not saved (or applied) yet, as the web editor marked them: a green dot of the same
    /// size on screen just above each, drawn over everything, for the kinds shown. Selected objects have none: the
    /// selection's box marks them, and their dots come back once they are no longer selected.
    /// </summary>
    internal List<Vector3> NewMarkers(WorldScene s)
    {
        var list = new List<Vector3>();
        var selected = Selected.ToHashSet();
        lock (s.Things)
        {
            lock (_objLock)
            {
                for (int i = 0; i < s.Things.Count; i++)
                {
                    var t = s.Things[i];
                    if (t.Id < 0 && !t.Gone && i < _kinds.Length && _shown[(int)_kinds[i]] && !InGame(t.Id) && !selected.Contains(i))
                    {
                        list.Add(ToView(s, t.Position + new Vector3(0, 0.4f, 0)));
                    }
                }
            }
        }
        return list;
    }

    /// <summary>Has the new objects' markers found again at the next frame (after a save or apply, say).</summary>
    public void RefreshNewMarkers()
    {
        _marksDirty = true;
        _spawnsDirty = true;
        Wake();
    }
}
