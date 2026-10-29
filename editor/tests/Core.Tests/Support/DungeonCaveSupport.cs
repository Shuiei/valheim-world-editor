using System.Numerics;
using TerrainEditor.Editing;

namespace WorldEditor.Tests;

/// <summary>
/// The Frost Cave the game generated itself, as the dungeon tests read it: its room list, where it stands, and the
/// comparisons of positions and rotations the tests make of its rooms.
/// </summary>
public static class DungeonCaveSupport
{
    public const string GeneratedCave = "HgAAAJd4m14AAMA1L2ihRQAgJcUAAACAAAC0QgAAAADXtLSx/v8/wS9goUUAwCTFAAAAgJMOIjcAAAAACRvPzAAAoDUvaKFFAAAkxQAAAIAAAIdDAAAAAA+Nnz/wYya1L3ChRQDgIsUAAAAAk6ImuQAAAAAPjZ8//v+PwS9woUUAACTFAAAAAPv/hkMAAAAAi7rYjhMAQMEveqFFAIAixQAAAAAMAIdDAAAAAHlwoYEAAMDBL1KhRQDAJMUAAACAAQA0QwAAAACZiJRnBQDAwS92oUUAQCPFAAAAAAEAh0MAAAAAY59FIu7/P0EvdqFFAIAixQAAAIADALRCAAAAAPBFMewKAMDBL3qhRQCAIsUAAACAMAC0QgAAAADIKp5/9/+/QS92oUUAgCLFAAAAgAQAtEIAAACAUupdjeb/f0EvaqFFAEAjxQAAAIAEALRCAAAAgIjTrNJhAEDBL2qhRQBAI8UAAACAGAA0QwAAAACI06zS3P8/QS9qoUUAACTFAAAAgAIANEMAAAAAQqG7uAcAwEAvaKFFAMAkxQAAAICTDiI3AAAAgF0FKu0AAMBAL2ihRQAAJMUAAACA3hVzNwAAAICzcESuIDMOuC92oUUAICLFAAAAgOz/s0IAAAAAhyon3v7/78EvdqFFAAAkxQAAAAA13RK5AAAAAIcqJ972/7/BL3ahRQBgJMUAAACADACHQwAAAAB2QLA77f8/wS92oUUAICLFAAAAgC8AtEIAAAAAXgUq7f//v8EvUqFFAGAkxQAAAIABAIdDAAAAAKwp/4AEAJDBL3ahRQBAI8UAAAAAAgA0QwAAAACsKf+A8f8/QS92oUUAICLFAAAAgAQAtEIAAAAAdkCwOwoA8MEvdqFFAIAixQAAAIDDgcU5AAAAAHZAsDv2/7/BL3ahRQAgIsUAAACAMQC0QgAAAACsKf+AHgDAwS92oUUA4CLFAAAAgAwAh0MAAAAAdkCwO/f/70EvdqFFAIAixQAAAAADADRDAAAAAIcqJ975/79BL3ahRQAgIsUAAACABAC0QgAAAICHKife9f+/QS92oUUA4CLFAAAAAAEAh0MAAAAArCn/gMj/v0AvdqFFAEAjxQAAAIBlUDY4AAAAgA==";

    /// <summary>Where the generated cave stands.</summary>
    public static readonly Vector3 CaveAt = new(0, 5165.023f, -2654);

    /// <summary>
    /// The rooms of the generated Frost Cave.
    /// </summary>
    public static List<Dungeons.Placed> Cave() => Dungeons.Read(Convert.FromBase64String(GeneratedCave));

    /// <summary>
    /// Whether two rotations are the same turn (either sign, nearly exactly).
    /// </summary>
    public static bool Same(Quaternion a, Quaternion b) => MathF.Abs(Quaternion.Dot(Quaternion.Normalize(a), Quaternion.Normalize(b))) > 0.99999f;

    /// <summary>
    /// Positions equal when within this distance of each other.
    /// </summary>
    public sealed class Vector3Comparer(float tolerance) : IEqualityComparer<Vector3>
    {
        /// <summary>Whether the two positions are within the tolerance of each other.</summary>
        public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= tolerance;

        /// <summary>A constant hash: positions are compared, never looked up.</summary>
        public int GetHashCode(Vector3 v) => 0;
    }
}
