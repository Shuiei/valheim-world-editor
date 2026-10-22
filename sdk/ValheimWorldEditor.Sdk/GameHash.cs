namespace ValheimWorldEditor.Sdk;

/// <summary>The game's string hash (Valheim's GetStableHashCode), as its prefab names and data keys are hashed.</summary>
public static class GameHash
{
    /// <summary>A string's hash, as the game computes it for prefab names and data keys.</summary>
    /// <param name="str">The string.</param>
    public static int Of(string str)
    {
        unchecked
        {
            int num = 5381;
            int num2 = num;
            for (int i = 0; i < str.Length && str[i] != 0; i += 2)
            {
                num = ((num << 5) + num) ^ str[i];
                if (i == str.Length - 1 || str[i + 1] == '\0')
                {
                    break;
                }
                num2 = ((num2 << 5) + num2) ^ str[i + 1];
            }
            return num + num2 * 1566083941;
        }
    }
}
