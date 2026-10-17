using System.Reflection;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Calls the private and internal methods of the editor from a test, for the helper steps that are not public. The
/// method's own code runs as it does in the editor; only the access is widened here, so production code stays as it is.
/// </summary>
internal static class PrivateMethods
{
    /// <summary>Calls a method by name with the arguments given, on the target (null for a static method).</summary>
    /// <param name="type">The type declaring the method.</param>
    /// <param name="name">The method's name (unique in the type).</param>
    /// <param name="target">The instance to call on; null for a static method.</param>
    /// <param name="args">The arguments.</param>
    public static object? Call(Type type, string name, object? target, params object?[] args) =>
        type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!.Invoke(target, args);
}
