using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using TerrainEditor.Save;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The names of the data an object (ZDO) can hold, by hash (the save keeps only the hash of each
    /// name), for the object inspector (ZdoKeys), read at the editor's start (GameData). Every string literal
    /// of the game's code (the user strings of assembly_valheim.dll) is a candidate, plus the names the game
    /// builds at run time ("pu_id" + i for ward permissions, "{index}_item" for armour stands...).
    /// </summary>
    public static string ZdoKeysJson(string valheim) => CatalogueJson.Write(ZdoKeyNames(valheim), new CatalogueJson.Style(",", ":", SortKeys: true));

    /// <summary>
    /// What a data name looks like: a letter or "_" first, then letters, digits, "_" or spaces, 40 characters at most.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex KeyName = new(@"\A[A-Za-z_][A-Za-z0-9_ ]{0,39}\z");

    /// <summary>
    /// The candidate names (see <c>ZdoKeysJson</c>) by the text of their stable hash; the shortest name wins a shared
    /// hash.
    /// </summary>
    private static Dict ZdoKeyNames(string valheim)
    {
        string dll = GameLook.DataDir(valheim) is string data && Path.Combine(data, "Managed", "assembly_valheim.dll") is var path && File.Exists(path)
            ? path : throw new FileNotFoundException("assembly_valheim.dll is not in the game's folder");
        var names = KeyNamesIn(dll);
        // Built at run time by the game.
        for (int i = 0; i < 64; i++)
        {
            names.UnionWith([$"pu_id{i}", $"pu_name{i}", $"{i}_seed", $"data_{i}", $"data__{i}"]);
        }
        for (int i = 0; i < 16; i++)
        {
            foreach (string n in new[] { "item", "variant", "quality", "itemData", "data", "sockets" })
            {
                names.Add($"{i}_{n}");
            }
        }
        var table = new Dict();
        foreach (var (hash, n) in ByHash(names))
        {
            table[hash.ToString(System.Globalization.CultureInfo.InvariantCulture)] = n;
        }
        return table;
    }

    /// <summary>
    /// The string literals of a .NET assembly that look like data names (<c>KeyName</c>): the user strings of its
    /// metadata, read as bytes (the assembly is not loaded). Empty for a file that is not a .NET assembly.
    /// </summary>
    internal static HashSet<string> KeyNamesIn(string dll)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        using var pe = new PEReader(File.OpenRead(dll));
        if (!pe.HasMetadata)
        {
            return names;
        }
        var md = pe.GetMetadataReader();
        for (var h = MetadataTokens.UserStringHandle(1); !h.IsNil; h = md.GetNextHandle(h))
        {
            string s = md.GetUserString(h);
            if (KeyName.IsMatch(s))
            {
                names.Add(s);
            }
        }
        return names;
    }

    /// <summary>
    /// Names by their stable hash, with the "_u" and "_i" each name gets for a ZDOID value (kept as two longs); the
    /// shortest name wins a shared hash.
    /// </summary>
    internal static Dictionary<int, string> ByHash(IEnumerable<string> names)
    {
        var all = new HashSet<string>(StringComparer.Ordinal);
        foreach (string n in names)
        {
            all.UnionWith([n, n + "_u", n + "_i"]);
        }
        var table = new Dictionary<int, string>();
        foreach (string n in all.OrderBy(n => n.Length).ThenBy(n => n, StringComparer.Ordinal))
        {
            table.TryAdd(StableHash.Of(n), n);
        }
        return table;
    }
}
