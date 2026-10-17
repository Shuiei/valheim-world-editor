using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Built by: the world's builders, this computer's characters, named players, another id, or nobody.
    /// </summary>
    private void FillBuilders()
    {
        if (Session?.Scene.World is not { } w)
        {
            return;
        }
        _fillingBuilders = true;
        var players = Builders.Players(w);
        BuilderIds.Clear();
        BuilderIds.AddRange(players.Select(p => p.Id));
        BuilderIds.Add(-1);
        BuilderIds.Add(0);
        BuilderBox.ItemsSource = players.Select(p => p.Label).Append(Lang.T("select.builder.other")).Append(Lang.T("select.builder.nobody")).ToList();
        BuilderBox.SelectedIndex = BuilderIds.IndexOf(TerrainEditor.Save.WorldSave.Builder);
        _fillingBuilders = false;
    }

    /// <summary>
    /// Asks for a player id ("Other player id…"; replaced by tests).
    /// </summary>
    internal Func<Task<string?>> AskPlayerId { get; set; }

    /// <summary>
    /// A Built by choice: new pieces get that builder, remembered for the world ("Other player id…" asks for one).
    /// </summary>
    private async Task BuilderChosen()
    {
        if (_fillingBuilders || BuilderBox.SelectedIndex < 0 || Session?.Scene.World is not { } w)
        {
            return;
        }
        long id = BuilderIds[BuilderBox.SelectedIndex];
        if (id == -1)
        {
            string? text = (await AskPlayerId())?.Trim();
            if (!long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out id) || id == 0)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    Msg("select.msg.badPlayerId");
                }
                FillBuilders();
                return;
            }
        }
        TerrainEditor.Save.WorldSave.Builder = id;
        PlacePanel.Memory.Builders[w.Name] = id;
        PlacePanel.Memory.Save();
        FillBuilders();
        Msg(id == 0 ? Said.Of("select.msg.builtByNobody")
            : Builders.Players(w).FirstOrDefault(p => p.Id == id)?.Words is { } who ? Said.Of("select.msg.builtBy", who)
            : Said.Of("select.msg.builtByPlayer", id));
    }
}
