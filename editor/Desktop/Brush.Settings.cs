namespace TerrainEditor.Desktop;

// The settings more than one tool uses (Naturalize's bumps, bump size and pattern, Erode's rest angle): each panel
// that shows one is told when another changes it.
public sealed partial class Brush
{
    /// <summary>
    /// A setting several panels show (bumps, bump size, rest angle) was changed in one of them: the others then show
    /// the new value (<see cref="BrushRows"/>).
    /// </summary>
    public event Action? SettingsChanged;

    /// <summary>Raises <see cref="SettingsChanged"/>.</summary>
    public void NotifySettings() => SettingsChanged?.Invoke();

    /// <summary>A new pattern of natural bumps (New pattern): another seed for the noise.</summary>
    /// <param name="seed">The noise's seed; null for a random one.</param>
    public void NewPattern(int? seed = null) => Noise = new Noise(seed ?? Random.Shared.Next());
}
