using TerrainEditor.App;

namespace TerrainEditor.Terrain;

public static partial class PieceCost
{
    /// <summary>
    /// A piece's English name, else its prefab name.
    /// </summary>
    public static string PieceName(string prefab) => Get(prefab)?.Name ?? prefab;

    /// <summary>
    /// What a building costs.
    /// </summary>
    /// <param name="Materials">Item prefab name, English name and amount, most first.</param>
    /// <param name="Stations">The crafting stations it needs, sorted.</param>
    /// <param name="Unknown">The kinds of piece the game has no recipe for (from mods, or not buildable) with how
    /// many there are.</param>
    public sealed record Total(List<(string Item, string Name, int Amount)> Materials, List<string> Stations, Dictionary<string, int> Unknown)
    {
        /// <summary>
        /// "Wood 24 · Stone 8 · Resin 2", the first few (… when there are more), and its stations; "Free" when
        /// there are no materials, or "Cost unknown" when only pieces without a recipe.
        /// </summary>
        /// <param name="most">How many materials to list.</param>
        /// <param name="english">In English whatever the language in use (for Claude).</param>
        public string Describe(int most = 4, bool english = false)
        {
            Func<string, object?[], string> say = english ? Lang.En : Lang.T;
            if (Materials.Count == 0)
            {
                return say(Unknown.Count > 0 ? "cost.unknown" : "cost.free", []);
            }
            string text = string.Join(" · ", Materials.Take(most).Select(m => $"{m.Name} {m.Amount}")) + (Materials.Count > most ? " · …" : "");
            return Stations.Count > 0 ? say("cost.needs", [text, string.Join(", ", Stations)]) : text;
        }
    }
}
