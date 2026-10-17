using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The frame's camera: whatever the eye is (orbit, walk or fly), the view matrix puts the eye at the origin of the
/// view's space, so what the camera looks at is in front of it.
/// </summary>
public class GlViewCameraFrameTests
{
    [AvaloniaFact]
    public void TheViewPutsTheEyeAtItsOriginInEveryEyeMode()
    {
        var s = TestScenes.Flat(2);
        var v = new GlView();
        v.Show(s.Scene, null);
        for (int mode = 0; mode < 3; mode++)
        {
            var (view, eye) = v.CameraView();
            var inView = Vector3.Transform(eye, view);
            Assert.Equal(0f, inView.X, 3);
            Assert.Equal(0f, inView.Y, 3);
            Assert.Equal(0f, inView.Z, 3);
            v.CycleEye();
        }
    }
}
