using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace TerrainEditor.Desktop;

/// <summary>The Avalonia application: the Fluent theme in dark with the editor's styles, and the main window.</summary>
public sealed class App : Application
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Ui.Apply(this);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    /// <summary>
    /// Opens the main window, and catches errors in the UI thread's handlers: logged and shown instead of closing the
    /// app and losing what is not saved.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            // An error in a button's or a key's handler would close the app and lose what is not saved:
            // it is logged and shown instead.
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Options.Say($"error: {TerrainEditor.App.Log.Scrub(e.Exception.ToString())}");
                e.Handled = true;
                window.ShowError(e.Exception);
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
