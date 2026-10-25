namespace TerrainEditor.Editing;

public sealed partial class EditStore
{
    /// <summary>
    /// Zones whose No limit ground the world holds as ground discs (saved, or applied live): that ground, and its
    /// <see cref="ZoneEdit.Fingerprint"/>. The working edit has the lift, the world (and the baseline) the discs and
    /// the edits worked out for them. While the zone's ground is still that one, it is not a change.
    /// </summary>
    private readonly Dictionary<(int, int), (ZoneEdit Form, long Fingerprint)> _planned = [];

    /// <summary>
    /// A zone's No limit ground over the ground discs the world holds for it (worked out from it): the working edit
    /// becomes it, not a change to save or apply while it stays so; the baseline (what the world holds) is left as it
    /// is.
    /// </summary>
    /// <param name="liftForm">The zone's edits with their lift, on the generated ground with the discs.</param>
    public void PutPlanned(ZoneEdit liftForm)
    {
        liftForm.Sanitize();
        lock (_lock)
        {
            var key = (liftForm.ZoneX, liftForm.ZoneZ);
            liftForm.ExistsInWorld = _baseline.ContainsKey(key);
            liftForm.Changed = false;
            _zones[key] = liftForm;
            _planned[key] = (liftForm.Clone(), liftForm.Fingerprint());
            Version++;
        }
    }

    /// <summary>
    /// Whether a zone's No limit ground is still the one the world holds as ground discs (<see cref="PutPlanned"/>):
    /// its discs need not be worked out again.
    /// </summary>
    public bool AsPlanned(int zx, int zz)
    {
        lock (_lock)
        {
            return AsPlannedLocked((zx, zz), _zones.GetValueOrDefault((zx, zz)));
        }
    }

    /// <summary>
    /// A zone's No limit ground as the world holds it as ground discs (<see cref="PutPlanned"/>), whatever was done to
    /// the zone since; null when the world holds none for it.
    /// </summary>
    public ZoneEdit? PlannedOf(int zx, int zz)
    {
        lock (_lock)
        {
            return _planned.TryGetValue((zx, zz), out var p) ? p.Form.Clone() : null;
        }
    }

    /// <summary><see cref="AsPlanned"/> for an edit, under the lock.</summary>
    private bool AsPlannedLocked((int, int) key, ZoneEdit? e) => e != null && _planned.TryGetValue(key, out var p) && e.Fingerprint() == p.Fingerprint;

    /// <summary>The least lift the ground keeps: one under a millimetre is none (as Ground.SetHeight drops it).</summary>
    public const float LeastLift = 1e-3f;

    /// <summary>A lift as the ground keeps it: within the editor's reach, and none under <see cref="LeastLift"/>.</summary>
    public static float KeepLift(float lift) => MathF.Abs(lift) < LeastLift ? 0 : Math.Clamp(lift, -MaxLift, MaxLift);
}
