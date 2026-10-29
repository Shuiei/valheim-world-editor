namespace Tunnelheim.WorldEditor;

/// <summary>The hash the game names prefabs and data keys by (its strings' stable hash code).</summary>
internal static class Hash
{
    /// <summary>A name's hash, as the game works it out: two running sums over the even and the odd characters.</summary>
    /// <param name="name">The name.</param>
    public static int Of(string name)
    {
        unchecked
        {
            int even = 5381, odd = 5381;
            for (int i = 0; i < name.Length && name[i] != '\0'; i += 2)
            {
                even = ((even << 5) + even) ^ name[i];
                if (i + 1 >= name.Length || name[i + 1] == '\0')
                {
                    break;
                }
                odd = ((odd << 5) + odd) ^ name[i + 1];
            }
            return even + odd * 1566083941;
        }
    }
}
