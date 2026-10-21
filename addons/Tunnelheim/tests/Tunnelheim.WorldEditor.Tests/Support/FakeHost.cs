using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>The editor as the add-on sees it outside the hooks: no game connected, no game components (no hoe strokes).</summary>
internal sealed class FakeHost : IEditorHost, IGameData
{
    public ILiveGame? Live => null;

    public string? Setting(string file, string section, string key) => null;

    public IGameData Game => this;

    public void Redraw()
    {
    }

    public IReadOnlyList<GameComponent> Components(string scriptClass) => [];
}
