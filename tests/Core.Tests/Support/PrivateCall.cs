using System.Reflection;
using System.Runtime.ExceptionServices;

namespace WorldEditor.Tests;

/// <summary>
/// Calls the private members of a dungeon generator class from a test, the way the generator itself calls them.
/// The arguments array is the one reflection writes out parameters into, so an out argument reads back from it.
/// </summary>
internal static class PrivateCall
{
    /// <summary>Calls a private static method with this name and argument count.</summary>
    public static object? Static(Type type, string name, params object?[] args) => Invoke(type, name, null, args);

    /// <summary>Calls a private instance method with this name and argument count.</summary>
    public static object? Instance(object target, string name, params object?[] args) => Invoke(target.GetType(), name, target, args);

    /// <summary>Reads a private field of an instance.</summary>
    public static object? Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target);

    /// <summary>
    /// Makes an instance through a constructor with these parameter types, of any access: a record's primary constructor
    /// is public even when the record itself is private.
    /// </summary>
    public static object Create(Type type, Type[] parameterTypes, params object?[] args) =>
        type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameterTypes, null)!.Invoke(args);

    private static object? Invoke(Type type, string name, object? target, object?[] args)
    {
        var flags = BindingFlags.NonPublic | (target == null ? BindingFlags.Static : BindingFlags.Instance);
        var method = type.GetMethods(flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length);
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }
}
