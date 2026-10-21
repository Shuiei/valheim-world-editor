using System.Globalization;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public static partial class SettingsDialog
{
    internal sealed partial class Form
    {
        /// <summary>
        /// Checks the port and applies the window's choices, saving them with the folders. A problem leaves the window open
        /// with its message, and the choices as they were; the extensions turned on run now, those turned off stop, and the
        /// history keeps the steps chosen from the next change on.
        /// </summary>
        private void Save()
        {
            if (!int.TryParse(_port.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int claudePort) || claudePort is < 1024 or > 65535)
            {
                _error.Text = Lang.T("settings.badPort");
                return;
            }
            if (ReadHistorySteps() is not int historySteps)
            {
                _error.Text = Lang.T("settings.badHistorySteps");
                return;
            }
            // Set before Apply, which saves them with the folders; taken back when the folders are refused, so a
            // Cancel after that keeps nothing.
            var before = (_settings.ClaudeConnect, _settings.ClaudeScripts, _settings.ClaudePort, _settings.ClaudeToken, _settings.DriverOpenGl, _settings.Language, _settings.ExpandWorld, _settings.ExpandWorldFolder, _settings.Riverheim, _settings.HistorySteps);
            var allowedBefore = new Dictionary<string, string>(_settings.Extensions);
            foreach (var (box, x) in _extensionBoxes)
            {
                ModExtensions.Allow(_settings, x, box.IsChecked == true);
            }
            _settings.ClaudeConnect = _claude.IsChecked == true;
            _settings.ClaudeScripts = _scripts.IsChecked == true;
            _settings.ClaudePort = claudePort;
            _settings.ClaudeToken = _token;
            _settings.DriverOpenGl = _driverGl.IsChecked == true;
            _settings.ExpandWorld = _expandWorld.IsChecked == true;
            _settings.ExpandWorldFolder = string.IsNullOrWhiteSpace(_expandWorldFolder.Text) ? null : _expandWorldFolder.Text.Trim();
            _settings.Riverheim = _riverheim.IsChecked == true;
            _settings.HistorySteps = historySteps;
            _settings.Language = _language.SelectedIndex > 0 ? _languages[_language.SelectedIndex - 1].Code : null;
            if (Apply(_settings, _valheim.Text, _readBep(), _readWorlds()) is string problem)
            {
                (_settings.ClaudeConnect, _settings.ClaudeScripts, _settings.ClaudePort, _settings.ClaudeToken, _settings.DriverOpenGl, _settings.Language, _settings.ExpandWorld, _settings.ExpandWorldFolder, _settings.Riverheim, _settings.HistorySteps) = before;
                _settings.Extensions = allowedBefore;
                _error.Text = problem;
                return;
            }
            // The extensions turned on run now, those turned off stop.
            ModExtensions.Refresh(_settings);
            EditSession.StepLimit = historySteps;
            Saved = true;
            Dialog.Close();
        }
    }
}
