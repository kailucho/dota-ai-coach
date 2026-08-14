using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace DotaAiCoach.UiOverlay.ScreenCapture;

/// <summary>
/// Captures a fixed screen region as a PNG byte array, for the "Capturar
/// pantalla" one-click flow (player has already opened Dota's scoreboard/
/// KDA view on their own screen — this only reads pixels already
/// rendered there, fairPlayClassification: VISIBLE_SCREEN). Physical-
/// pixel coordinates via Graphics.CopyFromScreen, same primary-monitor-
/// relative coordinate space RegionPickerWindow records the region in.
/// </summary>
internal static class RegionScreenshotCapturer
{
    public static byte[] Capture(CaptureRegion region)
    {
        using var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(region.X, region.Y, 0, 0, new Size(region.Width, region.Height));
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
