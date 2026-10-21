using UnityEngine;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>Whether a point's coordinates are all numbers (not NaN or infinite), as JSON needs.</summary>
    private static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);

    /// <summary>Whether a number is one (not NaN or infinite), as JSON needs.</summary>
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    /// <summary>Whether a child and every parent up to the root is active (the root's own state does not count).</summary>
    private static bool ActiveUnder(Transform t, Transform root)
    {
        for (; t && t != root; t = t.parent)
        {
            if (!t.gameObject.activeSelf)
            {
                return false;
            }
        }
        return true;
    }
}
