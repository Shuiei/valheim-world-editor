using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// How long ago a blueprint was saved, as the start page words it: today, yesterday, a number of days up to two weeks,
/// and the date after that.
/// </summary>
public sealed class StartPageAgoTests
{
    /// <summary>Calls the start page's age check.</summary>
    private static string Ago(DateTime when) =>
        (string)PrivateMethods.Call(typeof(StartPage), "Ago", null, when)!;

    /// <summary>A save from today reads as today.</summary>
    [Fact]
    public void ASaveFromTodayReadsAsToday() =>
        Assert.Equal(Lang.T("start.ago.today"), Ago(DateTime.Now));

    /// <summary>A save from yesterday reads as yesterday.</summary>
    [Fact]
    public void ASaveFromYesterdayReadsAsYesterday() =>
        Assert.Equal(Lang.T("start.ago.yesterday"), Ago(DateTime.Now.AddDays(-1)));

    /// <summary>Thirteen days ago is still counted in days.</summary>
    [Fact]
    public void ThirteenDaysAgoIsCountedInDays() =>
        Assert.Equal(Lang.T("start.ago.days", 13), Ago(DateTime.Now.AddDays(-13)));

    /// <summary>Fourteen days ago is shown as a date, not a count of days.</summary>
    [Fact]
    public void FourteenDaysAgoIsShownAsADate()
    {
        var when = DateTime.Now.AddDays(-14);

        Assert.Equal(Lang.T("start.ago.date", when), Ago(when));
    }
}
