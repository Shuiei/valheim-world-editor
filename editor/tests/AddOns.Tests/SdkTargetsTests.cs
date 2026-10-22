using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The SDK's two builds, as add-ons build against them: netstandard2.0 (the extensions' API, without the world-rules
/// model) and net10.0 (with it, in ValheimGen, for add-ons that make world rules).
/// </summary>
public class SdkTargetsTests
{
    /// <summary>The namespaces and names of the types an assembly file defines, read without loading it.</summary>
    private static List<string> Types(string dll)
    {
        using var file = File.OpenRead(dll);
        using var pe = new PEReader(file);
        var md = pe.GetMetadataReader();
        return [.. md.TypeDefinitions.Select(md.GetTypeDefinition).Select(t => md.GetString(t.Namespace) + "." + md.GetString(t.Name))];
    }

    /// <summary>The SDK's build for a target, in this run's configuration (a netstandard2.0 add-on in the repository has it built).</summary>
    private static string Built(string target)
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        return Path.Combine(Path.GetDirectoryName(RepositoryAddOns.Folder)!, "sdk", "ValheimWorldEditor.Sdk", "bin", configuration, target, "ValheimWorldEditor.Sdk.dll");
    }

    [Fact]
    public void OnlyTheNet10BuildHoldsTheWorldRulesModel()
    {
        var standard = Types(Built("netstandard2.0"));
        var net10 = Types(Built("net10.0"));

        Assert.Contains("ValheimWorldEditor.Sdk.IExtensionRegistry", standard);
        Assert.DoesNotContain(standard, t => t.StartsWith("ValheimGen.", StringComparison.Ordinal));
        Assert.Contains("ValheimWorldEditor.Sdk.IExtensionRegistry", net10);
        Assert.Contains("ValheimGen.WorldRules", net10);
        Assert.Contains("ValheimGen.LocationRuleSet", net10);
    }
}
