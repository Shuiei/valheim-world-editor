using System.Collections.Generic;

namespace ValheimWorldEditor.Sdk;

/// <summary>A world whose rules an <see cref="IWorldRulesProvider"/> is asked for.</summary>
/// <param name="Name">The world's name (for the log).</param>
/// <param name="StartingGlobalKeys">The starting global keys its world file keeps (a mod's settings stored there).</param>
/// <param name="LoadedGlobalKeys">The global keys its save holds now (null when not read).</param>
/// <param name="GridWidth">How many zones wide its save's chunk grid is.</param>
/// <param name="Live">The game it runs in, when live (its own files are read through its bridge); null for a saved world.</param>
public sealed record WorldRulesRequest(string Name, IReadOnlyList<string> StartingGlobalKeys, IReadOnlyList<string>? LoadedGlobalKeys, int GridWidth, ILiveGame? Live);
