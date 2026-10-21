using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// A piece's entry as the editor's pieces data has it: category "cat", comfort, the box around its active solid
    /// box colliders "box" (min x, max x, min z, max z, min y, max y), its snap points "snap" (children tagged or named
    /// snappoint, inactive ones too), all in the piece's own frame, and its build tool.
    /// </summary>
    private static string PieceEntry(GameObject go, Piece piece, string tool)
    {
        Transform root = go.transform;
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        StringBuilder sb = new($"{{\"cat\":{(int)piece.m_category},\"comfort\":{piece.m_comfort}");
        List<Vector3> snaps = [];
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t != root && (t.CompareTag("snappoint") || t.name.IndexOf("snappoint", StringComparison.OrdinalIgnoreCase) >= 0)
                && toRoot.MultiplyPoint3x4(t.position) is var p && Finite(p))
            {
                snaps.Add(p);
            }
        }
        if (snaps.Count > 0)
        {
            sb.Append(",\"snap\":[").Append(string.Join(",", snaps.Select(p => $"[{R(p.x)},{R(p.y)},{R(p.z)}]"))).Append(']');
        }
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (BoxCollider c in go.GetComponentsInChildren<BoxCollider>(true))
        {
            if (c.isTrigger || !c.enabled || !ActiveUnder(c.transform, root))
            {
                continue;
            }
            Matrix4x4 m = toRoot * c.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c.center + Vector3.Scale(c.size * 0.5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
        // A degenerate transform (zero scale) gives no box: NaN or infinity is not JSON.
        if (min.x <= max.x && Finite(min) && Finite(max))
        {
            sb.Append($",\"box\":[{R(min.x)},{R(max.x)},{R(min.z)},{R(max.z)},{R(min.y)},{R(max.y)}]");
        }
        if (tool != null)
        {
            sb.Append(",\"tool\":").Append(Json(tool));
        }
        return sb.Append('}').ToString();
    }
}
