using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>A row per saved server, to connect, edit or forget it.</summary>
    internal StackPanel SavedServers { get; } = new() { Spacing = 6 };
    /// <summary>A saved server's connection error.</summary>
    private readonly TextBlock _savedError = Err();
    /// <summary>The server form's error (or progress) line.</summary>
    internal TextBlock ServerError { get; } = Err();
    /// <summary>The own tunnel's error line.</summary>
    internal TextBlock UrlError { get; } = Err();
    /// <summary>My game's card: the game's state and what to do.</summary>
    internal ContentControl GameState { get; } = new();

    /// <summary>The server form: the name it shows under in the list (optional).</summary>
    internal TextBox SName { get; } = new() { PlaceholderText = Lang.T("start.hint.name") };
    /// <summary>The server's address.</summary>
    internal TextBox SHost { get; } = new() { PlaceholderText = Lang.T("start.hint.host") };
    /// <summary>The SSH port (22).</summary>
    internal NumericUpDown SPort { get; } = new() { Value = 22, Minimum = 1, Maximum = 65535, FormatString = "0" };
    /// <summary>The SSH user.</summary>
    internal TextBox SUser { get; } = new() { PlaceholderText = Lang.T("start.hint.user") };
    /// <summary>The SSH password (or a key file instead).</summary>
    internal TextBox SPass { get; } = new() { PasswordChar = '•', PlaceholderText = Lang.T("start.hint.password") };
    /// <summary>The SSH key file (optional).</summary>
    internal TextBox SKey { get; } = new() { PlaceholderText = Lang.T("start.hint.keyFile") };
    /// <summary>The plugin's token, from its config file on the server.</summary>
    internal TextBox SToken { get; } = new() { PlaceholderText = Lang.T("start.hint.token") };
    /// <summary>The server's Valheim folder (optional: the plugin's port and precise errors).</summary>
    internal TextBox SFolder { get; } = new() { PlaceholderText = Lang.T("start.hint.gameFolder") };
    /// <summary>Save the password with the server.</summary>
    internal CheckBox SSave { get; } = new() { Content = Lang.T("start.label.savePassword") };
    /// <summary>The key file's passphrase (never saved).</summary>
    internal TextBox SPhrase { get; } = new() { PasswordChar = '•' };
    /// <summary>The plugin's port on the server, when not the default.</summary>
    internal NumericUpDown SBridge { get; } = new() { Minimum = 1, Maximum = 65535, FormatString = "0", PlaceholderText = "5182" };
    /// <summary>I made my own tunnel: the bridge's address.</summary>
    internal TextBox LiveUrl { get; } = new() { Text = "127.0.0.1:5182" };
    /// <summary>I made my own tunnel: the plugin's token.</summary>
    internal TextBox LiveToken { get; } = new() { PlaceholderText = Lang.T("start.hint.bridgeToken") };

}
