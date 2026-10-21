using System.Globalization;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>Opens on a thing of the scene; false when its data cannot be read.</summary>
    public bool Open(int index)
    {
        var s = _session();
        if (s == null || s.Scene.World == null)
        {
            return false;
        }
        var t = s.Scene.Things[index];
        byte[]? bytes;
        try
        {
            bytes = ObjectData.Bytes(s.Scene.World, s.Edits, t.Id);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading an object's data to inspect it");
            bytes = null;
        }
        if (bytes == null)
        {
            Message?.Invoke(Said.Of("inspect.msg.cannotRead"));
            return false;
        }
        Index = index;
        _bytes = bytes;
        _z = ZdoData.Parse(bytes);
        Reset();
        Card.IsVisible = true;
        return true;
    }

    /// <summary>A float as text that reads back to the same value, whatever the culture.</summary>
    private static string S(float v) => v.ToString("R", CultureInfo.InvariantCulture);
}
