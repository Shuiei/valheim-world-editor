using System.Numerics;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal static partial class DungeonShell
{
    /// <summary>
    /// One doorway's wall, as its frame and its closure are placed on it: the wall's line, the opening's centre
    /// along it (C), the floor (F) and the wall's top (H), and the turn of the pieces (Yaw).
    /// </summary>
    private readonly record struct DoorSite(Material Mat, DungeonOut O, bool AlongZ, float Line, float C, float F, float H, float Yaw)
    {
        /// <summary>A point on the doorway's wall: t along it, y high.</summary>
        public Vector3 At(float t, float y) => AlongZ ? new Vector3(Line, y, t) : new Vector3(t, y, Line);

        /// <summary>The jambs either side of the opening, and over it a lintel from doorTop up.</summary>
        public void Frame(float doorTop)
        {
            Fill(Mat, O, AlongZ, Line, C - 2, C - 1, F, H);
            Fill(Mat, O, AlongZ, Line, C + 1, C + 2, F, H);
            Fill(Mat, O, AlongZ, Line, C - 1, C + 1, F + doorTop, H);
        }

        /// <summary>A framed opening: jambs, and over it a lintel, or the material's arch, held by both.</summary>
        public void Open()
        {
            if (Mat.Arch is { } arch && arch != "stone_arch")
            {
                Frame(2);
                O.Add(arch, At(C, F + (arch is "blackmarble_arch" or "dvergrtown_arch" ? 3 : 2)), Yaw);
            }
            else
            {
                Frame(3);
            }
        }

        /// <summary>A grand gate: two gates, and a timber tympanum over them.</summary>
        public void Grand()
        {
            // Over the gates (wood) a timber tympanum, not stone: stone on wood holds no better than wood's
            // best, which is stone's least, and the wall over a gate into a tall room would fall.
            O.Add("darkwood_gate", At(C - 1, F), Yaw);
            O.Add("darkwood_gate", At(C + 1, F), Yaw + 180);
            Fill(Mat.Name is "Wood" or "Stave" or "Dvergr" or "Goblin" ? Mat : MaterialOf("Wood"), O, AlongZ, Line, C - 2, C + 2, F + 4, H);
        }

        /// <summary>A secret door: a cracked stretch of stone to break through, or a hanging curtain door.</summary>
        public void Secret()
        {
            if (Mat.Name == "Stone")
            {
                // A cracked stretch of wall, to break through.
                O.Add("stone_wall_ruin_2", At(C - 1, F + 0.63f), AlongZ ? -90 : 0);
            }
            else
            {
                O.Add("cloth_hanging_door_double", At(C, F - 0.25f), Yaw);
            }
            Fill(Mat, O, AlongZ, Line, C - 2, C + 2, F + 4.2f, H);
        }
    }
}
