using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// A bundle's internal file, as it is kept for reading.
/// </summary>
public static partial class GameCatalogues
{
    internal sealed partial class Bundle
    {
        /// <summary>
        /// One internal file of a bundle that has objects.
        /// </summary>
        /// <param name="Cab">The file's name, lower case.</param>
        /// <param name="Assets">The file as AssetsTools opened it.</param>
        public sealed record Inner(string Cab, AssetsFileInstance Assets)
        {
            /// <summary>
            /// A reference's file (m_FileID &gt; 0: one this file lists as needed), by name (lower case).
            /// </summary>
            public string? FileOf(int fileId)
            {
                if (fileId == 0)
                {
                    return Cab;
                }
                var ext = Assets.file.Metadata.Externals;
                return fileId - 1 < ext.Count ? ext[fileId - 1].PathName.Split('/').Last().ToLowerInvariant() : null;
            }
        }
    }
}
