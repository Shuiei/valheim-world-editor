using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

/// <summary>
/// The player-built pieces and ruins drawn over the map: footprints, outlines and dots, made again when the zoom or the set changes.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// Built once per zoom kind: footprints close up (edges very close), dots of two pixels further out. What the
    /// building shapes were last built for (the pieces, the kind, for dots the zoom, and which are shown).
    /// </summary>
    private (float[]? Pieces, int Kind, float Mpp, bool Buildings, bool Ruins) _buildingsKey;
    /// <summary>
    /// The building shapes last built: footprints or dots, and close up their edges.
    /// </summary>
    private float[] _buildingFill = [], _buildingEdge = [];
    /// <summary>
    /// Draws the player-built pieces: footprints under 2.5 m a pixel (outlined under 0.8 m), dots up to 10 m, nothing
    /// further out.
    /// </summary>
    private void DrawBuildings()
    {
        float[]? built = _pieces;
        float[] ruins = _ruins ?? [];
        float mpp = MetersPerPixel;
        if (built == null || !ShowBuildings && !ShowRuins || mpp > 10)
        {
            return;
        }
        int kind = KindFor(mpp);
        if (BuildingsStale(built, kind, mpp))
        {
            _buildingsKey = (built, kind, mpp, ShowBuildings, ShowRuins);
            var fill = new List<float>();
            var edge = new List<float>();
            AddPieces(fill, edge, built, ruins, kind, mpp);
            _buildingFill = [.. fill];
            _buildingEdge = [.. edge];
        }
        Fill(_buildingFill, PrimitiveType.Triangles);
        Fill(_buildingEdge, PrimitiveType.Lines);
    }

    /// <summary>The zoom kind of the buildings: 0 dots, 1 footprints, 2 footprints with outlines.</summary>
    /// <param name="mpp">Metres per map pixel of the view.</param>
    /// <returns>The kind.</returns>
    internal static int KindFor(float mpp) => mpp < 0.8f ? 2 : mpp < 2.5f ? 1 : 0;

    /// <summary>Whether the building shapes must be made again: the pieces, the zoom kind, the zoom (for dots), or what is shown changed.</summary>
    /// <param name="built">The player-built pieces now shown.</param>
    /// <param name="kind">The zoom kind now.</param>
    /// <param name="mpp">Metres per map pixel of the view now.</param>
    /// <returns>True when the shapes must be made again.</returns>
    internal bool BuildingsStale(float[] built, int kind, float mpp) =>
        _buildingsKey.Pieces != built || _buildingsKey.Kind != kind || (kind == 0 && _buildingsKey.Mpp != mpp) || _buildingsKey.Buildings != ShowBuildings || _buildingsKey.Ruins != ShowRuins;
}
