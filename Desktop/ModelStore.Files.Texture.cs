using SkiaSharp;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ModelStore
{
    private sealed partial class Files
    {
        /// <summary>
        /// A texture file under tex/, decoded as stored; null when it cannot be read.
        /// </summary>
        public ImageData? Texture(string file)
        {
            string path = Path.Combine(_root, "tex", file);
            try
            {
                using var codec = SKCodec.Create(path);
                if (codec == null)
                {
                    return null;
                }
                var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                byte[] rgba = new byte[info.BytesSize];
                unsafe
                {
                    fixed (byte* p = rgba)
                    {
                        if (codec.GetPixels(info, (IntPtr)p) is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                        {
                            return null;
                        }
                    }
                }
                return new ImageData(info.Width, info.Height, rgba);
            }
            catch (Exception e)
            {
                Log.CaughtBrief(e, "decoding an exported texture file");
                return null;
            }
        }
    }
}
