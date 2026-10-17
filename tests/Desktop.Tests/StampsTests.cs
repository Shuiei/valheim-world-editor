using System.Buffers.Binary;
using System.IO.Compression;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Stamps: pictures as brush shapes, built in or loaded, and stamping one in at once.
/// </summary>
public class StampsTests
{
    private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

    [Fact]
    public void TheBuiltInStampsAreTheWebEditors()
    {
        // Values from the web editor's stamps.js for the same pixels.
        float[] mountain = Stamps.BuiltIn.Single(s => s.Name == "stamp:mountain").Data;
        float[] rocky = Stamps.BuiltIn.Single(s => s.Name == "stamp:rocky").Data;
        Assert.Equal(0.980590f, mountain[64 * 128 + 64], 4);
        Assert.Equal(0.416947f, mountain[80 * 128 + 40], 4);
        Assert.Equal(0.203913f, rocky[50 * 128 + 70], 4);
        Assert.Equal(0, Stamps.Sample(mountain, 0.99f, 0.99f), 3);
    }

    [Fact]
    public void APictureBecomesAStamp()
    {
        // A white disc on black.
        using var bmp = new SKBitmap(300, 300);
        using (var g = new SKCanvas(bmp))
        {
            g.Clear(SKColors.Black);
            g.DrawCircle(150, 150, 100, new SKPaint { Color = SKColors.White, IsAntialias = true });
        }
        using var png = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 100);
        float[] data = Stamps.FromPicture(png.ToArray())!;
        Assert.Equal(1, Stamps.Sample(data, 0, 0), 2);
        Assert.Equal(0, Stamps.Sample(data, 0.9f, 0.9f), 2);
        Assert.Null(Stamps.FromPicture([1, 2, 3]));
    }

    // A picture whose pixels would take more memory than can be had (a PNG of 400 000 × 400 000): not read, rather
    // than Skia throwing when it cannot allocate them.
    [Fact]
    public void APictureTooLargeToDecodeIsNotRead()
    {
        Assert.Null(Stamps.FromPicture(HugePng(400_000)));
    }

    /// <summary>
    /// A PNG of <paramref name="side"/> × <paramref name="side"/> colours with opacity whose data stops after one byte.
    /// </summary>
    /// <param name="side">Its width and height in pixels.</param>
    private static byte[] HugePng(int side)
    {
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, side);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), side);
        // 8 bits a channel, colours with opacity.
        (header[8], header[9]) = (8, 6);
        Chunk(png, "IHDR", header);
        using var data = new MemoryStream();
        using (var z = new ZLibStream(data, CompressionLevel.Fastest, leaveOpen: true))
        {
            z.WriteByte(0);
        }
        Chunk(png, "IDAT", data.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>Writes one PNG chunk: its length, type, data and the CRC-32 of its type and data.</summary>
    /// <param name="png">The file being written.</param>
    /// <param name="type">The chunk's four-letter type.</param>
    /// <param name="data">Its data.</param>
    private static void Chunk(MemoryStream png, string type, byte[] data)
    {
        byte[] typed = [.. System.Text.Encoding.ASCII.GetBytes(type), .. data];
        byte[] number = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        png.Write(number);
        png.Write(typed);
        uint crc = uint.MaxValue;
        foreach (byte b in typed)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
        }
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc);
        png.Write(number);
    }

    [Fact]
    public void StampOnceRaisesByThePicture()
    {
        var s = TestScenes.Flat(2);
        var b = s.Brush;
        b.Shape = BrushShape.Stamp;
        b.StampData = Stamps.BuiltIn.Single(x => x.Name == "stamp:mesa").Data;
        b.Radius = 20;
        s.EditGround("stamp", g => { var (t, r, _) = Sculpt.StampOnce(g, b, 64, 64, 4, null); return (t, r); });
        // The flat top of the mesa goes up the full 4 m; outside the stamp nothing moves.
        Assert.Equal(34, H(s, 64, 64), 2);
        Assert.Equal(30, H(s, 64, 90), 3);
    }

    [AvaloniaFact]
    public void ThePanelChoosesStampsAndLoadsPictures()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.KeyPress(Key.D1, RawInputModifiers.None, PhysicalKey.Digit1, "1");
        var t = w.Tools;
        t.ShapeBox.SelectedIndex = 4;
        Assert.Equal(BrushShape.Stamp, t.Brush.Shape);
        Assert.Equal("Mountain", t.Brush.StampLabel);
        Assert.True(t.StampOnceBox.IsEffectivelyVisible);
        Assert.False(t.FalloffBox.IsEffectivelyVisible);
        t.StampOnceBox.IsChecked = true;
        t.StampHeightBox.Value = 3;
        w.StampAt(64, 64);
        Assert.Equal(30 + 3 * Stamps.Sample(Stamps.BuiltIn[0].Data, 0, 0), H(s, 64, 64), 2);
        Assert.Equal("Stamp: Mountain", s.UndoLabel);
        // A loaded picture is kept for next time, and can be forgotten.
        int before = t.StampList.Count;
        t.AddStamp("Disc", [.. Enumerable.Repeat(1f, 128 * 128)]);
        Assert.Contains(Stamps.LoadKept(), k => k.Label == "Disc");
        Assert.Equal(before + 1, t.StampList.Count);
        Assert.True(t.ForgetStampButton.IsVisible);
        t.ForgetStampButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.DoesNotContain(Stamps.LoadKept(), k => k.Label == "Disc");
        Assert.Equal(0, t.ShapeBox.SelectedIndex);
    }
}
