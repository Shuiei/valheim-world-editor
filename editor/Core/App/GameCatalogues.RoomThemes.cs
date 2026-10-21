using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The dungeon room themes by name, with their flag values (the game's Room.Theme), read from the game's code
    /// (assembly_valheim.dll's metadata; the assembly is not loaded) at the editor's start (GameData): Expand World
    /// Data's room and dungeon files name the themes. Empty when the code has no such list.
    /// </summary>
    /// <param name="valheim">Valheim's folder.</param>
    public static string RoomThemesJson(string valheim)
    {
        string dll = GameLook.DataDir(valheim) is string data && Path.Combine(data, "Managed", "assembly_valheim.dll") is var path && File.Exists(path)
            ? path : throw new FileNotFoundException("assembly_valheim.dll is not in the game's folder");
        return CatalogueJson.Write(RoomThemesIn(dll), new CatalogueJson.Style(",", ":"));
    }

    /// <summary>
    /// The named values of the enum Theme nested in the class Room of a .NET assembly, in their order: the values the
    /// metadata keeps for them (4-byte whole numbers); none for an assembly without it, or a file that is no assembly.
    /// </summary>
    /// <param name="dll">The assembly.</param>
    internal static Dict RoomThemesIn(string dll)
    {
        var themes = new Dict();
        using var pe = new PEReader(File.OpenRead(dll));
        if (!pe.HasMetadata)
        {
            return themes;
        }
        var md = pe.GetMetadataReader();
        foreach (var handle in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(handle);
            if (md.GetString(type.Name) != "Theme" || type.GetDeclaringType() is not { IsNil: false } outer || md.GetString(md.GetTypeDefinition(outer).Name) != "Room")
            {
                continue;
            }
            foreach (var f in type.GetFields().Select(md.GetFieldDefinition))
            {
                if (f.GetDefaultValue() is { IsNil: false } c && md.GetConstant(c) is { TypeCode: ConstantTypeCode.Int32 } constant)
                {
                    themes[md.GetString(f.Name)] = (long)md.GetBlobReader(constant.Value).ReadInt32();
                }
            }
        }
        return themes;
    }
}
