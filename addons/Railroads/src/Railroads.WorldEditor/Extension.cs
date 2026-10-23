using ValheimWorldEditor.Sdk;

[assembly: WorldEditorExtension(typeof(Railroads.WorldEditor.Extension))]

namespace Railroads.WorldEditor;

/// <summary>The add-on's entry: registers what it adds to the editor for Railroads.</summary>
public sealed class Extension : IWorldEditorExtension
{
    /// <inheritdoc/>
    public void Register(IExtensionRegistry registry)
    {
        var models = new ModelLibrary(registry.Host, registry.Log);
        foreach (var piece in RailroadsMod.Pieces)
        {
            registry.AddShapes(new PieceShapes(piece, models), piece.Prefab);
        }
        var lines = new TrackLines();
        foreach (string key in new[] { RailroadsMod.Keys.Line, RailroadsMod.Keys.Trail, RailroadsMod.Keys.Route, RailroadsMod.Keys.After })
        {
            registry.AddDecoder(key, lines);
        }
        var ids = new ObjectIds();
        registry.AddDecoder(RailroadsMod.Keys.Wagons, ids);
        registry.AddDecoder(RailroadsMod.Keys.Self, ids);
        registry.AddDecoder(RailroadsMod.Keys.Legs, new TripLegs());
        registry.AddCopyRewriter(new PastedCopies(), PastedCopies.Keys);
        registry.Log($"ready (editor {registry.EditorVersion}, API {registry.ApiVersion})");
    }
}
