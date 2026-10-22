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
            var choices = AddressChoices();
            var saved = ClaudeServer.ParseAddress(_settings.ClaudeAddress);
            _address = new ComboBox { Name = "ClaudeAddress", IsEditable = true, MinWidth = 260, ItemsSource = choices }.Tip("settings.claudeAddress");
            _address.Text = choices.FirstOrDefault(c => saved != null && saved.Equals(ClaudeServer.ParseAddress(c))) ?? _settings.ClaudeAddress;
            _token = string.IsNullOrEmpty(_settings.ClaudeToken) ? ClaudeServer.NewToken() : _settings.ClaudeToken;
            _command = new TextBox { Name = "ClaudeCommand", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, FontFamily = new FontFamily("monospace") }.Tip("settings.claudeCommand");
            _remote = Hint("");
            _remote.Name = "ClaudeRemote";
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
                if (_address.SelectedItem is string chosen && _address.Text != chosen)
                {
                    _address.Text = chosen;
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
        /// port); on every address, also the address another computer uses (this computer's name).
        /// </summary>
        private void ShowCommand()
        {
            int port = int.TryParse(_port.Text, out int p) ? p : _settings.ClaudePort;
            _command.Text = ClaudeServer.ClaudeCodeCommand(port, _token, _address.Text);
            bool every = ClaudeServer.ParseAddress(_address.Text) is { } ip && ClaudeServer.IsEveryAddress(ip);
            _remote.IsVisible = every;
            _remote.Text = every ? Lang.T("settings.claudeRemoteHint", ClaudeServer.RemoteUrl(port)) : "";
        }

        /// <summary>
        /// The addresses to choose from, each said in words: this computer only, every network, then each network this
        /// computer is on with its address.
        /// </summary>
        private static List<string> AddressChoices() =>
        [
            Lang.T("settings.addressLoopback", "127.0.0.1"),
            Lang.T("settings.addressAny", "0.0.0.0"),
            .. ClaudeServer.LocalAddresses().Select(a => Lang.T("settings.addressOn", a.Address, a.Network)),
        ];
    }
}
