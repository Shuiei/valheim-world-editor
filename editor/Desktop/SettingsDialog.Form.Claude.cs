using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public static partial class SettingsDialog
{
    internal sealed partial class Form
    {
        /// <summary>
        /// Builds Claude's connection section: the allow switch, the address and port, the command for Claude Code, the
        /// scripts switch, Claude Desktop's config, and the connection's status.
        /// </summary>
        private void BuildClaude()
        {
            _claude = new CheckBox { Content = Lang.T("settings.claudeAllow"), IsChecked = _settings.ClaudeConnect }.Classed("switch").Tip("settings.claude");
            _port = new TextBox { Name = "ClaudePort", Text = _settings.ClaudePort.ToString(System.Globalization.CultureInfo.InvariantCulture), Width = 90 }.Tip("settings.claudePort");
            // The address: picked from this computer's (each said in words) or typed; the saved one shown as its choice.
            // This computer's networks and name are asked of the system once, not on every key typed.
            _networks = ClaudeServer.LocalAddresses();
            _hostName = System.Net.Dns.GetHostName();
            var choices = AddressChoices(_networks);
            var saved = ClaudeServer.ParseAddress(_settings.ClaudeAddress);
            _address = new ComboBox { Name = "ClaudeAddress", IsEditable = true, MinWidth = 260, ItemsSource = choices }.Tip("settings.claudeAddress");
            if (choices.FirstOrDefault(c => c.Address.Equals(saved)) is { } savedChoice)
            {
                _address.SelectedItem = savedChoice;
                _address.Text = savedChoice.Label;
            }
            else
            {
                _address.Text = _settings.ClaudeAddress;
            }
            _token = string.IsNullOrEmpty(_settings.ClaudeToken) ? ClaudeServer.NewToken() : _settings.ClaudeToken;
            _command = new TextBox { Name = "ClaudeCommand", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, FontFamily = new FontFamily("monospace") }.Tip("settings.claudeCommand");
            _remote = Hint("");
            _remote.Name = "ClaudeRemote";
            // Off this computer the connection is plain HTTP: said where the address is chosen, not only in its tip.
            _unencrypted = new TextBlock { Name = "ClaudeUnencrypted", Text = Lang.T("settings.claudeUnencrypted"), FontSize = 12, Foreground = Ui.Warn, TextWrapping = TextWrapping.Wrap };
            ShowCommand();
            _port.TextChanged += (_, _) => ShowCommand();
            _address.PropertyChanged += (_, e) =>
            {
                if (e.Property == ComboBox.TextProperty)
                {
                    ShowCommand();
                }
            };
            _address.SelectionChanged += (_, _) =>
            {
                if (_address.SelectedItem is AddressChoice chosen && _address.Text != chosen.Label)
                {
                    _address.Text = chosen.Label;
                }
            };
            var copy = new Button { Content = Lang.T("common.copy") }.Tip("settings.claudeCopy");
            copy.Click += async (_, _) => { if (TopLevel.GetTopLevel(Dialog)?.Clipboard is { } cb) { await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(cb, _command.Text ?? ""); } };
            var renew = new Button { Content = Lang.T("settings.newToken") }.Tip("settings.claudeToken");
            renew.Click += (_, _) => { _token = ClaudeServer.NewToken(); ShowCommand(); };
            _claudeDetails = new StackPanel { Spacing = 6, IsVisible = _claude.IsChecked == true };
            _claude.IsCheckedChanged += (_, _) => _claudeDetails.IsVisible = _claude.IsChecked == true;
            // Scripts have the editor's rights on this computer: the user's leave, once.
            _scripts = new CheckBox { Content = Lang.T("settings.claudeScripts"), IsChecked = _settings.ClaudeScripts }.Classed("switch").Tip("settings.claudeScripts");
            _claudeDetails.Children.Add(_scripts);
            _claudeDetails.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = Lang.T("settings.address"), VerticalAlignment = VerticalAlignment.Center }, _address,
                    new TextBlock { Text = Lang.T("settings.port"), VerticalAlignment = VerticalAlignment.Center }, _port, renew,
                },
            });
            _claudeDetails.Children.Add(_unencrypted);
            _claudeDetails.Children.Add(Hint(Lang.T("settings.claudeCodeHint")));
            _claudeDetails.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4, Children = { _command, Ui.Col(copy, 1) } });
            _claudeDetails.Children.Add(_remote);
            // Claude Desktop starts a program: this one, relaying to the running editor (ClaudeRelay).
            _desktop = new TextBox { Name = "ClaudeDesktop", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, FontFamily = new FontFamily("monospace"), Text = ClaudeServer.DesktopConfig(Environment.ProcessPath ?? "ValheimWorldEditor") }.Tip("settings.claudeDesktop");
            var copyDesktop = new Button { Content = Lang.T("common.copy") }.Tip("settings.claudeCopy");
            copyDesktop.Click += async (_, _) => { if (TopLevel.GetTopLevel(Dialog)?.Clipboard is { } cb) { await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(cb, _desktop.Text ?? ""); } };
            _claudeDetails.Children.Add(Hint(Lang.T("settings.claudeDesktopHint")));
            _claudeDetails.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4, Children = { _desktop, Ui.Col(copyDesktop, 1) } });
            if (ClaudeServer.Status is string status)
            {
                _claudeDetails.Children.Add(Hint(status));
            }
        }

        /// <summary>
        /// Shows the command that connects Claude Code at the address and port and with the token typed (or the saved
        /// port); on every address, also the addresses another computer uses (this computer's name, and its network
        /// addresses for networks that do not answer the name); off this computer, the warning that the connection is
        /// not encrypted.
        /// </summary>
        private void ShowCommand()
        {
            int port = int.TryParse(_port.Text, out int p) ? p : _settings.ClaudePort;
            var chosen = ChosenAddress();
            _command.Text = ClaudeServer.ClaudeCodeCommand(port, _token, chosen?.ToString());
            _unencrypted.IsVisible = chosen != null && !System.Net.IPAddress.IsLoopback(chosen);
            bool every = chosen != null && ClaudeServer.IsEveryAddress(chosen);
            _remote.IsVisible = every;
            if (!every)
            {
                _remote.Text = "";
                return;
            }
            var byAddress = ClaudeServer.RemoteAddressUrls(port, _networks);
            _remote.Text = byAddress.Count == 0
                ? Lang.T("settings.claudeRemoteHint", ClaudeServer.RemoteUrl(port, _hostName))
                : Lang.T("settings.claudeRemoteHintAddresses", ClaudeServer.RemoteUrl(port, _hostName), string.Join(", ", byAddress));
        }

        /// <summary>
        /// The address chosen: a choice's own, as long as its words are still what the box shows (they are never read
        /// back, so a language may say them in any order), else the address typed, or null when that is not one.
        /// </summary>
        private System.Net.IPAddress? ChosenAddress() =>
            _address.SelectedItem is AddressChoice c && _address.Text == c.Label ? c.Address : ClaudeServer.ParseAddress(_address.Text);

        /// <summary>
        /// The addresses to choose from, each said in words: this computer only, every network, then each network this
        /// computer is on with its address.
        /// </summary>
        /// <param name="networks">This computer's addresses (ClaudeServer.LocalAddresses).</param>
        private static List<AddressChoice> AddressChoices(IReadOnlyList<(System.Net.IPAddress Address, string Network)> networks) =>
        [
            new(System.Net.IPAddress.Loopback, Lang.T("settings.addressLoopback", "127.0.0.1")),
            new(System.Net.IPAddress.Any, Lang.T("settings.addressAny", "0.0.0.0")),
            .. networks.Select(a => new AddressChoice(a.Address, Lang.T("settings.addressOn", a.Address, a.Network))),
        ];

        /// <summary>One of Settings' address choices: the address, and the words the list shows for it.</summary>
        /// <param name="Address">The address.</param>
        /// <param name="Label">What the list shows.</param>
        internal sealed record AddressChoice(System.Net.IPAddress Address, string Label)
        {
            /// <summary>What the list and the box show.</summary>
            public override string ToString() => Label;
        }
    }
}
