using System.Text.Json;

namespace TerrainEditor.Save;

public sealed partial class LiveBridge : IDisposable
{
    /// <summary>The plugin's run and what it can do, as its status says now.</summary>
    /// <param name="Run">
    /// This run of the plugin: a new id each time the game starts (1.45.3 on; "" before). A call whose answer was lost is
    /// sent again under its id only to the run it went to: another run does not know what that one did.
    /// </param>
    /// <param name="Features">The features its status lists.</param>
    public sealed record Abilities(string Run, IReadOnlySet<string> Features)
    {
        /// <summary>
        /// Whether the plugin moves objects in place (<see cref="ApplyObjects"/>'s moves; 1.40.0 on, which lists "move").
        /// </summary>
        public bool CanMove => Features.Contains("move");

        /// <summary>
        /// Whether the plugin does an <see cref="ApplyObjects"/> call sent again under the same call id only once,
        /// answering the second time with what it did the first (1.45.3 on, which lists "once").
        /// </summary>
        public bool CanCallOnce => Features.Contains("once");

        /// <summary>
        /// Whether the plugin changes an object's data in place with a move (<see cref="LiveMove.Data"/>; 1.97.0 on, which
        /// lists "update"), keeping its ZDOID; else a changed object is destroyed and made again.
        /// </summary>
        public bool CanUpdate => Features.Contains("update");
    }

    /// <summary>
    /// The plugin's run and what it can do, asked now (once per apply: a game started again with another plugin is
    /// seen). A check that fails is an error, so a move is never sent as a destroy and a creation for want of an answer.
    /// </summary>
    /// <exception cref="HttpRequestException">The bridge could not be reached.</exception>
    /// <exception cref="InvalidOperationException">The bridge answered with an error status.</exception>
    /// <exception cref="JsonException">The status is not JSON.</exception>
    public async Task<Abilities> Ask()
    {
        using var doc = JsonDocument.Parse(await Status());
        JsonElement root = doc.RootElement;
        string run = root.TryGetProperty("run", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString()! : "";
        HashSet<string> features = root.TryGetProperty("features", out var f) && f.ValueKind == JsonValueKind.Array
            ? [.. f.EnumerateArray().Select(e => e.GetString() ?? "")]
            : [];
        return new Abilities(run, features);
    }
}
