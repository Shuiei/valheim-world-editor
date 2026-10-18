using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>Builds the dedicated server page: the saved servers, then the form (with More options).</summary>
    private void BuildServerPanel()
    {
        Control Field(string label, Control box)
        {
            var l = new TextBlock { Text = label, FontSize = 12, Foreground = Muted };
            Tips.Label(l, box);
            return new StackPanel { Spacing = 3, Children = { l, box } };
        }
        SName.Tip("start.name");
        SHost.Tip("start.host");
        SPort.Tip("start.sshPort");
        SUser.Tip("start.user");
        SPass.Tip("start.password");
        SKey.Tip("start.keyFile");
        SToken.Tip("start.token");
        SFolder.Tip("start.gameFolder");
        SSave.Tip("start.savePassword");
        SPhrase.Tip("start.passphrase");
        SBridge.Tip("start.bridgePort");
        LiveUrl.Tip("start.bridgeUrl");
        LiveToken.Tip("start.token");
        Control Two(Control a, Control b, string cols = "*,*") => new Grid { ColumnDefinitions = new ColumnDefinitions(cols), ColumnSpacing = 10, Children = { a, Ui.Col(b, 1) } };
        var keyBrowse = new Button { Content = Lang.T("common.browse"), VerticalAlignment = VerticalAlignment.Bottom }.Tip("start.browse");
        keyBrowse.Click += async (_, _) => { if (await PickFile(Lang.T("start.pickKey")) is string p) { SKey.Text = p; } };
        var connect = new Button { Content = Lang.T("start.label.connect"), HorizontalAlignment = HorizontalAlignment.Right }.Classed("primary").Tip("start.connect");
        connect.Click += async (_, _) => await ConnectNew();
        var connectUrl = new Button { Content = Lang.T("start.label.connect"), VerticalAlignment = VerticalAlignment.Bottom }.Tip("start.connectUrl");
        connectUrl.Click += async (_, _) => await ConnectUrl();
        ServerPanel.Children.Add(SavedServers);
        ServerPanel.Children.Add(_savedError);
        // The form folds away once there are saved servers (they connect with one click).
        ServerForm.Content = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 0),
            Children =
            {
                Field(Lang.T("dialog.label.name"), SName),
                Two(Field(Lang.T("start.label.host"), SHost), Field(Lang.T("start.label.sshPort"), SPort), "*,110"),
                Two(Field(Lang.T("start.label.user"), SUser), Field(Lang.T("start.label.password"), SPass)),
                Two(Field(Lang.T("start.label.keyFile"), SKey), keyBrowse, "*,Auto"),
                Field(Lang.T("start.label.pluginToken"), SToken),
                Field(Lang.T("start.label.gameFolder"), SFolder),
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { SSave, Ui.Col(connect, 1) } },
                ServerError,
                Hint(Lang.T("start.hint.server")),
                new Expander
                {
                    Header = Lang.T("start.label.moreOptions"),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            Two(Field(Lang.T("start.label.passphrase"), SPhrase), Field(Lang.T("start.label.pluginPort"), SBridge), "*,130"),
                            new TextBlock { Text = Lang.T("start.head.ownTunnel"), FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) },
                            new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,Auto"), ColumnSpacing = 10, Children = { Field(Lang.T("start.label.bridgeAddress"), LiveUrl), Ui.Col(Field(Lang.T("start.label.token"), LiveToken), 1), Ui.Col(connectUrl, 2) } },
                            UrlError,
                        },
                    },
                },
            },
        };
        ServerPanel.Children.Add(Card(ServerForm));
    }

    /// <summary>
    /// A row per saved server: Connect (asking for what is not saved: token, password, key passphrase), edit (into
    /// the form) and forget.
    /// </summary>
    internal void FillServers()
    {
        SavedServers.Children.Clear();
        var servers = ServerConfig.Load();
        bool any = servers.Count > 0;
        SavedServers.IsVisible = any;
        ServerForm.IsExpanded = !any;
        ((TextBlock)ServerForm.Header!).Text = any ? Lang.T("start.label.connectAnother") : Lang.T("start.label.connectServer");
        foreach (var s in servers)
        {
            var token = s.Token == null ? new TextBox { PlaceholderText = Lang.T("start.label.pluginToken"), Width = 180 }.Tip("start.savedToken") : null;
            var pass = s.Password == null && s.KeyFile == null ? new TextBox { PlaceholderText = Lang.T("start.label.password"), PasswordChar = '•', Width = 180 }.Tip("start.savedPassword") : null;
            // A key file with a passphrase: never saved, so asked here (it could not connect again).
            var phrase = s.KeyFile != null ? new TextBox { PlaceholderText = Lang.T("start.hint.savedPassphrase"), PasswordChar = '•', Width = 180 }.Tip("start.savedPassphrase") : null;
            var go = new Button { Content = Lang.T("start.label.connect") }.Classed("primary").Tip("start.savedConnect");
            var server = s;
            go.Click += async (_, _) => await Connect(new Tunnel.Request(server.Host, server.SshPort, server.User, pass?.Text ?? server.Password, server.KeyFile,
                string.IsNullOrEmpty(phrase?.Text) ? null : phrase.Text, token?.Text ?? server.Token, null,
                server.Password != null, server.Name, server.GameFolder), _savedError, Lang.T("start.msg.connectingTo", server.Name));
            var edit = new Button { Content = Lang.T("start.label.edit"), Foreground = Muted }.Classed("ghost").Tip("start.savedEdit");
            edit.Click += (_, _) =>
            {
                ServerForm.IsExpanded = true;
                (SHost.Text, SPort.Value, SUser.Text, SKey.Text, SFolder.Text, SName.Text) = (server.Host, server.SshPort, server.User, server.KeyFile, server.GameFolder, server.Name);
                // All of the form is this server's: what was typed for another one (its password, token) is never
                // sent here, and a saved password stays saved.
                (SPass.Text, SToken.Text, SPhrase.Text, SBridge.Value, SSave.IsChecked) = (server.Password, server.Token, null, null, server.Password != null);
                ServerError.Text = Lang.T("start.hint.editing");
            };
            var forget = new Button { Content = Lang.T("start.label.forget"), Foreground = Muted }.Classed("ghost").Tip("start.savedForget");
            forget.Click += async (_, _) =>
            {
                if (await Confirm(Lang.T("start.ask.forget.text", server.Name)))
                {
                    ServerConfig.Forget(server.Id);
                    FillServers();
                }
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new StackPanel
            {
                Width = 330,
                Children =
                {
                    new TextBlock { Text = s.Name, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = $"{s.User}@{s.Host}{(s.SshPort != 22 ? ":" + s.SshPort : "")} · {(s.KeyFile != null ? Lang.T("start.server.keyFile") : s.Password != null ? Lang.T("start.server.passwordSaved") : Lang.T("start.server.passwordAsked"))}{(s.GameFolder != null ? " · " + s.GameFolder : "")}", FontSize = 11, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis },
                },
            });
            if (token != null)
            {
                row.Children.Add(token);
            }

            if (pass != null)
            {
                row.Children.Add(pass);
            }

            if (phrase != null)
            {
                row.Children.Add(phrase);
            }

            row.Children.Add(go);
            row.Children.Add(edit);
            row.Children.Add(forget);
            SavedServers.Children.Add(Card(row));
        }
    }
}
