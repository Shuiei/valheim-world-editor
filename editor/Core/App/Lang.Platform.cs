using System.Runtime.InteropServices;

namespace TerrainEditor.App;

/// <summary>
/// The computer's languages, as the system says them: the Windows display languages, the macOS preferred ones and the locale variables.
/// </summary>
public static partial class Lang
{
    /// <summary>
    /// The computer's languages, most wanted first, as codes ("fr-FR", "de"): on Windows the user's display languages
    /// (not the regional format's); on macOS the user's preferred languages (an app opened from Finder has no LANG),
    /// then, as elsewhere, LANGUAGE, LC_ALL, LC_MESSAGES and LANG ("fr_FR.UTF-8"). The app runs without .NET's culture
    /// data, so its CultureInfo cannot say.
    /// </summary>
    internal static List<string> SystemLanguages()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsLanguages();
        }
        var codes = new List<string>();
        if (OperatingSystem.IsMacOS())
        {
            codes.AddRange(MacLanguages());
        }
        foreach (string var in new[] { "LANGUAGE", "LC_ALL", "LC_MESSAGES", "LANG" })
        {
            codes.AddRange(FromLocales(Environment.GetEnvironmentVariable(var)));
        }
        return [.. codes.Distinct()];
    }

    /// <summary>Codes from a locale variable: "fr_FR.UTF-8" is fr-FR, "de:en" is de then en; C and POSIX are none.</summary>
    internal static IEnumerable<string> FromLocales(string? value) =>
        (value ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Split('.', '@')[0].Replace('_', '-'))
            .Where(v => v.Length > 0 && v is not ("C" or "POSIX"));

    /// <summary>The names of a Windows multi-string ("fr-FR\0en-US\0\0"), in order.</summary>
    internal static List<string> FromMultiString(ReadOnlySpan<char> names) =>
        [.. names.ToString().Split('\0', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>GetUserPreferredUILanguages: languages as names ("fr-FR"), not ids.</summary>
    private const uint MuiLanguageName = 0x8;

    /// <summary>The Windows user's display languages ("fr-FR"), most wanted first; none when they cannot be read.</summary>
    private static unsafe List<string> WindowsLanguages()
    {
        uint count = 0, length = 0;
        if (GetUserPreferredUILanguages(MuiLanguageName, &count, null, &length) && length > 0)
        {
            char[] names = new char[length];
            fixed (char* buffer = names)
            {
                if (GetUserPreferredUILanguages(MuiLanguageName, &count, buffer, &length))
                {
                    return FromMultiString(names.AsSpan(0, (int)Math.Min(length, (uint)names.Length)));
                }
            }
        }
        Console.WriteLine($"lang: the Windows display languages could not be read (error {Marshal.GetLastPInvokeError()}).");
        return [];
    }

    /// <summary>Windows: the user's display languages, as a multi-string.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool GetUserPreferredUILanguages(uint flags, uint* count, char* buffer, uint* length);

    /// <summary>macOS: the CoreFoundation framework.</summary>
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    /// <summary>kCFStringEncodingUTF8.</summary>
    private const uint CfUtf8 = 0x08000100;

    /// <summary>The macOS user's preferred languages ("fr-FR", "zh-Hans-CN"), most wanted first.</summary>
    private static unsafe List<string> MacLanguages()
    {
        var codes = new List<string>();
        nint list = CFLocaleCopyPreferredLanguages();
        if (list == 0)
        {
            Console.WriteLine("lang: the macOS preferred languages could not be read.");
            return codes;
        }
        try
        {
            byte* name = stackalloc byte[128];
            for (nint k = 0, n = CFArrayGetCount(list); k < n; k++)
            {
                if (CFStringGetCString(CFArrayGetValueAtIndex(list, k), name, 128, CfUtf8))
                {
                    codes.Add(new string((sbyte*)name));
                }
            }
        }
        finally
        {
            CFRelease(list);
        }
        return codes;
    }

    /// <summary>macOS: the user's preferred languages, a CFArray of CFStrings (released by the caller).</summary>
    [LibraryImport(CoreFoundation)]
    private static partial nint CFLocaleCopyPreferredLanguages();

    /// <summary>macOS: how many values a CFArray holds.</summary>
    [LibraryImport(CoreFoundation)]
    private static partial nint CFArrayGetCount(nint array);

    /// <summary>macOS: a CFArray's value (not retained).</summary>
    [LibraryImport(CoreFoundation)]
    private static partial nint CFArrayGetValueAtIndex(nint array, nint index);

    /// <summary>macOS: a CFString as a C string in the encoding given; false when it does not fit.</summary>
    [LibraryImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static unsafe partial bool CFStringGetCString(nint text, byte* buffer, nint size, uint encoding);

    /// <summary>macOS: releases a CoreFoundation object.</summary>
    [LibraryImport(CoreFoundation)]
    private static partial void CFRelease(nint value);
}
