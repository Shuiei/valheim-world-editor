using System;
using System.Collections.Generic;

namespace WorldEditorBridge.Api;

/// <summary>
/// Where a mod's plugin registers with the WorldEditorBridge, to take part when the Valheim World Editor is connected
/// to this game: told what the editor applied (<see cref="IBridgeListener"/>), and answering the editor's calls on
/// routes of its own (<see cref="IBridgeRoutes"/>). Register from your plugin's Awake, with a soft dependency on the
/// bridge ("Tie.WorldEditorBridge"): without it, nothing listens, and nothing breaks. Everything here is called on
/// the game's main thread.
/// </summary>
public static class BridgeApi
{
    /// <summary>
    /// This API's version: a plugin built against it works with a bridge whose API version is this or later. Raised
    /// only when the API changes in a way that breaks plugins; additions keep it.
    /// </summary>
    public const int Version = 1;

    /// <summary>The bridge's plugin id, for a soft dependency.</summary>
    public const string BridgeGuid = "Tie.WorldEditorBridge";

    private static readonly object Lock = new();
    private static readonly Dictionary<string, IBridgeRoutes> RoutesById = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, IBridgeListener> ListenersById = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers what answers the editor's calls on <c>/mods/&lt;modId&gt;/&lt;name&gt;</c>, in place of any registered
    /// under that id before.
    /// </summary>
    /// <param name="modId">Your mod's id: its BepInEx plugin id is a good one. Letters, digits, dots, dashes and underscores.</param>
    /// <param name="routes">What answers.</param>
    public static void AddRoutes(string modId, IBridgeRoutes routes)
    {
        CheckId(modId);
        lock (Lock)
        {
            RoutesById[modId] = routes ?? throw new ArgumentNullException(nameof(routes));
        }
    }

    /// <summary>Registers what is told of the editor's changes, in place of any registered under that id before.</summary>
    /// <param name="modId">Your mod's id (see <see cref="AddRoutes"/>).</param>
    /// <param name="listener">What is told.</param>
    public static void AddListener(string modId, IBridgeListener listener)
    {
        CheckId(modId);
        lock (Lock)
        {
            ListenersById[modId] = listener ?? throw new ArgumentNullException(nameof(listener));
        }
    }

    /// <summary>Takes a mod's routes and listener away (a plugin shutting down).</summary>
    public static void Remove(string modId)
    {
        lock (Lock)
        {
            RoutesById.Remove(modId);
            ListenersById.Remove(modId);
        }
    }

    /// <summary>The ids registered (routes or listener), in no particular order. For the bridge.</summary>
    public static IReadOnlyList<string> ModIds
    {
        get
        {
            lock (Lock)
            {
                var ids = new HashSet<string>(RoutesById.Keys, StringComparer.Ordinal);
                ids.UnionWith(ListenersById.Keys);
                return [.. ids];
            }
        }
    }

    /// <summary>A mod's routes; null when it registered none. For the bridge.</summary>
    public static IBridgeRoutes? RoutesOf(string modId)
    {
        lock (Lock)
        {
            return RoutesById.TryGetValue(modId, out var r) ? r : null;
        }
    }

    /// <summary>The listeners registered, with their ids. For the bridge.</summary>
    public static IReadOnlyList<KeyValuePair<string, IBridgeListener>> Listeners
    {
        get
        {
            lock (Lock)
            {
                return [.. ListenersById];
            }
        }
    }

    /// <summary>Whether an id is one a route can carry.</summary>
    public static bool IsValidId(string? modId)
    {
        if (string.IsNullOrEmpty(modId) || modId!.Length > 100)
        {
            return false;
        }
        foreach (char c in modId)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != '_')
            {
                return false;
            }
        }
        return true;
    }

    private static void CheckId(string modId)
    {
        if (!IsValidId(modId))
        {
            throw new ArgumentException("a mod id is letters, digits, dots, dashes and underscores, 1 to 100 of them", nameof(modId));
        }
    }
}
