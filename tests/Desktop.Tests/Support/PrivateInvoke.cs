using System.Reflection;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Calls the private members of an editor type from a test, the way its own code calls them. Only the access is widened
/// here, so production code stays as it is.
/// </summary>
internal static class PrivateInvoke
{
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>Calls a method by name (unique in its type) and returns what it returns (null for a void method).</summary>
    /// <typeparam name="T">The type the result is read as.</typeparam>
    /// <param name="type">The type declaring the method.</param>
    /// <param name="name">The method's name.</param>
    /// <param name="target">The instance to call on; null for a static method.</param>
    /// <param name="args">The arguments.</param>
    public static T Call<T>(Type type, string name, object? target, params object?[] args) =>
        (T)(type.GetMethod(name, Any) ?? throw new MissingMethodException(type.FullName, name)).Invoke(target, args)!;
}
