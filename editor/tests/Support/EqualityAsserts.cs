using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Value equality checked the ways a struct offers it: typed, boxed, and by hash code.
/// </summary>
public static class EqualityAsserts
{
    /// <summary>
    /// The two are equal typed and boxed, hash alike, and neither equals an object of another type.
    /// </summary>
    public static void Same<T>(T a, T b)
        where T : struct, IEquatable<T>
    {
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(a.Equals(new object()));
    }

    /// <summary>
    /// The two are unequal typed and boxed.
    /// </summary>
    public static void Differ<T>(T a, T b)
        where T : struct, IEquatable<T>
    {
        Assert.False(a.Equals(b));
        Assert.False(a.Equals((object)b));
    }
}
