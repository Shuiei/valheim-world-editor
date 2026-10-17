using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public static partial class SettingsDialog
{
    private sealed partial class Form
    {
        /// <summary>
        /// Builds Claude's connection section: the allow switch, the port, the command for Claude Code, the scripts switch,
        /// Claude Desktop's config, and the connection's status.
        /// </summary>
        private void BuildClaude()
        {
            _claude = new CheckBox { Content = Lang.T("settings.claudeAllow"), IsChecked = _settings.ClaudeConnect }.Classed("switch").Tip("settings.claude");
            _port = new TextBox { Name = "ClaudePort", Text = _settings.ClaudePort.ToString(System.Globalization.CultureInfo.InvariantCulture), Width = 90 }.Tip("settings.claudePort");
            _token = string.IsNullOrEmpty(_settings.ClaudeToken) ? ClaudeServer.NewToken() : _settings.ClaudeToken;
            _command = new TextBox { Name = "ClaudeCommand", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, FontFamily = new FontFamily("monospace") }.Tip("settings.claudeCommand");
            ShowCommand();
            _port.TextChanged += (_, _) => ShowCommand();
            var copy = new Button { Content = Lang.T("common.copy") }.Tip("settings.claudeCopy");
            copy.Click += async (_, _) => { if (TopLevel.GetTopLevel(Dialog)?.Clipboard is { } cb) { await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(cb, _command.Text ?? ""); } };
            var renew = new Button { Content = Lang.T("settings.newToken") }.Tip("settings.claudeToken");
            renew.Click += (_, _) => { _token = ClaudeServer.NewToken(); ShowCommand(); };
            _claudeDetails = new StackPanel { Spacing = 6, IsVisible = _claude.IsChecked == true };
            _claude.IsCheckedChanged += (_, _) => _claudeDetails.IsVisible = _claude.IsChecked == true;
            // Scripts have the editor's rights on this computer: the user's leave, once.
            _scripts = new CheckBox { Content = Lang.T("settings.claudeScripts"), IsChecked = _settings.ClaudeScripts }.Classed("switch").Tip("settings.claudeScripts");
            _claudeDetails.Children.Add(_scripts);
            _claudeDetails.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = Lang.T("settings.port"), VerticalAlignment = VerticalAlignment.Center }, _port, renew } });
            _claudeDetails.Children.Add(Hint(Lang.T("settings.claudeCodeHint")));
            _claudeDetails.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4, Children = { _command, Ui.Col(copy, 1) } });
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

        /// <summary>Shows the command that connects Claude Code on the port and with the token typed (or the saved port).</summary>
        private void ShowCommand() => _command.Text = ClaudeServer.ClaudeCodeCommand(int.TryParse(_port.Text, out int p) ? p : _settings.ClaudePort, _token);
    }
}
