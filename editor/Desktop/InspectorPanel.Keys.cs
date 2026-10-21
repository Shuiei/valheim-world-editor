using System.Globalization;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>The contents in a short text form, to tell whether they changed.</summary>
    private string ItemsKey() => Items == null ? "" : string.Join(";", Items.Select(i => $"{i.Name},{i.Stack},{i.Quality},{i.Durability},{i.X},{i.Y}"));

    /// <summary>The stand's items and orientation or pose in a short text form, to tell whether they changed.</summary>
    private string StandKey() => Stand == null ? "" : string.Join(";", Stand.Select(r => $"{r.Name},{r.Quality},{r.Variant}")) + "|" + StandWay.ToString(CultureInfo.InvariantCulture);

    /// <summary>A value's label: the plain one, else a mod's manifest's, else its name, else its key's hash.</summary>
    private static string Label(Field f) => f.Name is string n ? PlainLabel(n) ?? ModManifests.DataOf(n)?.Label ?? n : $"#{f.Key}";
}
