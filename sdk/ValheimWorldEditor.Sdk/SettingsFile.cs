using System.Collections.Generic;
using System.Linq;

namespace ValheimWorldEditor.Sdk;

/// <summary>A BepInEx settings file (.cfg), as the editor and add-ons read one.</summary>
public static class SettingsFile
{
    /// <summary>
    /// A BepInEx settings file's "Key = value" lines in order, each with the section its heading names ("" before the
    /// first); comments and blank lines passed over.
    /// </summary>
    /// <param name="text">The file's text.</param>
    public static IEnumerable<(string Section, string Key, string Value)> Entries(string text)
    {
        string section = "";
        // The same code for both builds: netstandard2.0 has neither ranges nor EndsWith(char).
#pragma warning disable IDE0056, IDE0057, CA1865
        foreach (string line in text.TrimStart('\uFEFF').Split('\n').Select(l => l.Trim()))
        {
            if (line.Length > 1 && line[0] == '[' && line[line.Length - 1] == ']')
            {
                section = line.Substring(1, line.Length - 2).Trim();
                continue;
            }
            int equals = line.IndexOf('=');
            if (equals > 0 && line[0] is not ('#' or '['))
            {
                yield return (section, line.Substring(0, equals).Trim(), line.Substring(equals + 1).Trim());
            }
        }
#pragma warning restore IDE0056, IDE0057, CA1865
    }
}
