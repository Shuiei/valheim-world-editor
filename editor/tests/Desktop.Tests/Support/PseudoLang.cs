using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TerrainEditor.App;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A made-up language for the tests ("xx"): every English text wrapped as ⟦…⟧, its places for values kept. While it is
/// in use, a text a window shows outside ⟦⟧ did not come from the catalogue: <see cref="Untranslated"/> finds them.
/// Taken when made, English again when disposed. It changes the language in use, so a test using it runs in the
/// DataDir collection; tests running beside it would see ⟦…⟧ instead of their English.
/// </summary>
internal sealed partial class PseudoLang : IDisposable
{
    /// <summary>The language's code.</summary>
    public const string Code = "xx";

    /// <summary>What opens a text of the pseudo language.</summary>
    public const char Open = '⟦';

    /// <summary>What closes a text of the pseudo language.</summary>
    public const char Close = '⟧';

    /// <summary>The folder its catalogue is written to, deleted when disposed.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vwe-pseudo-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The catalogues' folder before (the tests' or none).</summary>
    private readonly string? _folderBefore = Lang.FolderOverride;

    /// <summary>Writes the catalogue and takes the language.</summary>
    /// <exception cref="InvalidOperationException">Lang did not take it (the catalogue was refused).</exception>
    public PseudoLang()
    {
        Directory.CreateDirectory(_folder);
        var texts = Lang.English.ToDictionary(kv => kv.Key, kv => Wrap(kv.Value));
        File.WriteAllText(Path.Combine(_folder, Code + ".json"), JsonSerializer.Serialize(texts));
        Lang.FolderOverride = _folder;
        if (Lang.Use(Code) != Code)
        {
            throw new InvalidOperationException("The pseudo language was not taken.");
        }
    }

    /// <summary>A text as the pseudo language shows it.</summary>
    /// <param name="text">The English text.</param>
    public static string Wrap(string text) => Open + text + Close;

    /// <summary>English again, the catalogues' folder as it was, the catalogue deleted.</summary>
    public void Dispose()
    {
        Lang.FolderOverride = _folderBefore;
        Lang.Use(Lang.Source);
        Directory.Delete(_folder, true);
    }

    /// <summary>A number and the unit after it ("12 m", "1.5 km", "60 fps", "×3"): the same in every language.</summary>
    [GeneratedRegex(@"[×x]?\d+(?:[.,]\d+)*\s?(?:km|cm|mm|m|px|ms|s|fps|KB|MB|GB|x|×|%|°)?(?![\p{L}])")]
    private static partial Regex Number();

    /// <summary>
    /// Whether nothing outside the text's ⟦…⟧ (nested ones counted) has a letter, once <paramref name="data"/>
    /// (longest first) and numbers with their units are taken out.
    /// </summary>
    /// <param name="text">A text as shown.</param>
    /// <param name="data">Texts that are data.</param>
    internal static bool Translated(string text, IEnumerable<string> data)
    {
        var outside = new StringBuilder();
        int depth = 0;
        foreach (char ch in text)
        {
            if (ch == Open)
            {
                depth++;
            }
            else if (ch == Close && depth > 0)
            {
                depth--;
            }
            else if (depth == 0)
            {
                outside.Append(ch);
            }
        }
        string rest = outside.ToString();
        foreach (string d in data.Where(d => d.Length > 0).OrderByDescending(d => d.Length))
        {
            rest = rest.Replace(d, "", StringComparison.Ordinal);
        }
        return !Number().Replace(rest, "").Any(char.IsLetter);
    }
}
