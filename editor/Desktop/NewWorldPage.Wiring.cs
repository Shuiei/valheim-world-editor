
namespace TerrainEditor.Desktop;

/// <summary>
/// The page's switches and tips: what each one shows, and its tip.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>Sets the tips of the page's controls.</summary>
    private void WireTips()
    {
        NameBox.Tip("newWorld.name");
        SeedBox.Tip("newWorld.seed");
        Roll.Tip("newWorld.roll");
        Generate.Tip("newWorld.generate");
        Where.Tip("newWorld.where");
        Create.Tip("newWorld.create");
        BigLand.Tip("newWorld.bigLand");
        LandAround.Tip("newWorld.landAround");
        OnFoot.Tip("newWorld.onFoot");
        Swamp.Tip("newWorld.near");
        Mountain.Tip("newWorld.near");
        Plains.Tip("newWorld.near");
        Haldor.Tip("newWorld.trader");
        BogWitch.Tip("newWorld.trader");
        Hildir.Tip("newWorld.trader");
        Bosses.Tip("newWorld.bosses");
        ShowStart.Tip("newWorld.showStart");
        ShowBosses.Tip("newWorld.showBosses");
        foreach (var t in new[] { ShowHaldor, ShowBogWitch, ShowHildir })
        {
            t.Tip("newWorld.showTrader");
        }
    }

    /// <summary>Wires the dungeon switches and the landmark switches to the marks they show.</summary>
    private void WireSwitches()
    {
        foreach (var t in ShowDungeons.Values)
        {
            t.Tip("newWorld.showDungeons");
            t.IsCheckedChanged += (_, _) =>
            {
                if (t.IsChecked == true && DungeonMarks == null && Shown != null)
                {
                    // Already being looked for (since the seed was shown): only waited for.
                    if (_dungeons == null)
                    {
                        FindDungeons(Shown);
                    }
                    WaitForDungeons(Shown);
                }
                PlaceDots();
            };
        }
        foreach (var t in new[] { ShowStart, ShowBosses, ShowHaldor, ShowBogWitch, ShowHildir })
        {
            t.IsCheckedChanged += (_, _) => PlaceMarks();
        }
    }
}
