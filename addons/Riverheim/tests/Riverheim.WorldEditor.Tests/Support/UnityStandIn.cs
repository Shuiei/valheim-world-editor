using System.Reflection;
using System.Reflection.Emit;

namespace Riverheim.WorldEditor.Tests;

/// <summary>
/// A stand-in for the game's Valheim folder where only Unity's base classes are needed: Riverheim's plugin classes
/// derive from BepInEx's, which derive from Unity's MonoBehaviour, and its generator names them when it scans itself.
/// The tests have no Valheim (CI has none): this writes a UnityEngine.dll with only those four classes (no code), in a
/// folder the editor takes for a game folder. Made up here; nothing is copied from Unity.
/// </summary>
public static class UnityStandIn
{
    /// <summary>Makes the stand-in game folder (its data folder, an empty bundles folder, the Managed stand-in).</summary>
    /// <param name="dir">The folder to make it in.</param>
    /// <returns>The folder, to give as the Valheim folder.</returns>
    public static string Game(string dir)
    {
        string data = Path.Combine(dir, "valheim_Data");
        Directory.CreateDirectory(Path.Combine(data, "StreamingAssets", "SoftRef", "Bundles"));
        string managed = Path.Combine(data, "Managed");
        Directory.CreateDirectory(managed);
        var assembly = new PersistedAssemblyBuilder(new AssemblyName("UnityEngine"), typeof(object).Assembly);
        var module = assembly.DefineDynamicModule("UnityEngine");
        Type parent = typeof(object);
        foreach (string name in new[] { "Object", "Component", "Behaviour", "MonoBehaviour" })
        {
            parent = module.DefineType("UnityEngine." + name, TypeAttributes.Public | TypeAttributes.Class, parent).CreateType();
        }
        assembly.Save(Path.Combine(managed, "UnityEngine.dll"));
        return dir;
    }
}
