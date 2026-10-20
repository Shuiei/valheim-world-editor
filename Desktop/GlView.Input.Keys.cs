using Avalonia.Controls;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// The keys from the window: the camera keys held in _keys, Space (which the view takes before the focused button),
// and F, which cycles the eye.
public sealed partial class GlView
{
    /// <summary>Space pressed or released in the window while the view takes keys: held or let go.</summary>
    /// <param name="surface">The view's surface, which tells whether the view is on screen.</param>
    /// <param name="e">The key event.</param>
    /// <param name="down">True for the press, false for the release.</param>
    private void OnSpaceKey(Control surface, KeyEventArgs e, bool down)
    {
        if (e.Key != Key.Space || !TakesKey(surface, e))
        {
            return;
        }
        lock (_keys)
        {
            if (down)
            {
                _keys.Add(Key.Space);
            }
            else
            {
                _keys.Remove(Key.Space);
            }
        }
        e.Handled = true;
        Wake();
    }

    /// <summary>
    /// A key pressed in the window: F cycles the eye, any other is held for the camera; none with Ctrl or Alt held
    /// (Ctrl + A, Alt + H… are shortcuts, not moves).
    /// </summary>
    /// <param name="surface">The view's surface, which tells whether the view is on screen.</param>
    /// <param name="e">The key event.</param>
    private void OnWindowKeyDown(Control surface, KeyEventArgs e)
    {
        if (!TakesKey(surface, e) || Ctrl(e.KeyModifiers) || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return;
        }
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.None)
        {
            CycleEye();
            return;
        }
        lock (_keys)
        {
            _keys.Add(e.Key);
        }
        Wake();
    }

    /// <summary>A key let go in the window: it is no longer held.</summary>
    /// <param name="e">The key event.</param>
    private void OnWindowKeyUp(KeyEventArgs e)
    {
        lock (_keys)
        {
            _keys.Remove(e.Key);
        }
        Wake();
    }

    /// <summary>Forgets every key held (the window lost the focus, or the view left the screen).</summary>
    private void ClearKeys()
    {
        lock (_keys)
        {
            _keys.Clear();
        }
    }

    /// <summary>A box took the focus: the keys held are the box's now, so the view forgets them.</summary>
    /// <param name="source">The element that got the focus.</param>
    private void ClearKeysForTextBox(object? source)
    {
        if (source is TextBox)
        {
            ClearKeys();
        }
    }
}
