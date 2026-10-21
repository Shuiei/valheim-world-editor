using System.Reflection;
using System.Runtime.ExceptionServices;

namespace WorldEditor.Tests;

/// <summary>
/// Reaches the private members of an editor type by name, for the tests of helper steps that are not public. The
/// member's own code runs as it does in the editor; only the access is widened here, so production code stays as it is.
/// </summary>
internal static class PrivateMember
{
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>A nested type of a class, private or not, by its name.</summary>
    /// <param name="owner">The class holding the type.</param>
    /// <param name="name">The nested type's name.</param>
    public static Type Nested(Type owner, string name) =>
        owner.GetNestedType(name, Any) ?? throw new MissingMemberException(owner.FullName, name);

    /// <summary>
    /// Calls a method by name and returns what it returns. The arguments array is passed as it is, so an out or ref
    /// argument's new value is read back from it afterwards.
    /// </summary>
    /// <param name="type">The type declaring the method.</param>
    /// <param name="name">The method's name (unique in the type).</param>
    /// <param name="target">The instance to call on; null for a static method.</param>
    /// <param name="args">The arguments.</param>
    public static object? Call(Type type, string name, object? target, params object?[] args)
    {
        MethodInfo method = type.GetMethod(name, Any) ?? throw new MissingMethodException(type.FullName, name);
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            // The method's own exception, as the caller would see it without reflection.
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }

    /// <summary>As <see cref="Call"/>, with the result converted to <typeparamref name="T"/>.</summary>
    public static T Call<T>(Type type, string name, object? target, params object?[] args) => (T)Call(type, name, target, args)!;

    /// <summary>Makes an instance of a type through its constructor with these arguments (any access).</summary>
    public static object New(Type type, params object?[] args) =>
        Activator.CreateInstance(type, Any, null, args, null) ?? throw new MissingMethodException(type.FullName, ".ctor");

    /// <summary>Sets a field or a property of an object by name.</summary>
    public static void Set(object target, string name, object? value)
    {
        Type type = target.GetType();
        FieldInfo? field = type.GetField(name, Any);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }
        PropertyInfo property = type.GetProperty(name, Any) ?? throw new MissingMemberException(type.FullName, name);
        property.SetValue(target, value);
    }

    /// <summary>Reads a field or a property of an object by name.</summary>
    public static object? Get(object target, string name)
    {
        Type type = target.GetType();
        FieldInfo? field = type.GetField(name, Any);
        if (field != null)
        {
            return field.GetValue(target);
        }
        PropertyInfo property = type.GetProperty(name, Any) ?? throw new MissingMemberException(type.FullName, name);
        return property.GetValue(target);
    }
}
