using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DotaAiCoach.UiOverlay.ScreenCapture;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace DotaAiCoach.UiOverlay;

/// <summary>
/// Full-screen, semi-transparent overlay for the player to drag a
/// rectangle over their scoreboard/KDA area once, per the user's
/// explicit request for a visual region picker rather than typing
/// coordinates. Coordinates are recorded in this window's own
/// (DIP-space) coordinates and converted to physical pixels via the
/// window's CompositionTarget transform, so the saved region matches
/// what Graphics.CopyFromScreen expects regardless of DPI scaling.
/// </summary>
public partial class RegionPickerWindow : Window
{
    private Point? _startPoint;
    public CaptureRegion? PickedRegion { get; private set; }

    public RegionPickerWindow()
    {
        InitializeComponent();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(this);
        Canvas.SetLeft(SelectionRectangle, _startPoint.Value.X);
        Canvas.SetTop(SelectionRectangle, _startPoint.Value.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
        SelectionRectangle.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_startPoint is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        var x = Math.Min(start.X, current.X);
        var y = Math.Min(start.Y, current.Y);
        var width = Math.Abs(current.X - start.X);
        var height = Math.Abs(current.Y - start.Y);

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();

        if (_startPoint is not { } start)
        {
            return;
        }

        var end = e.GetPosition(this);
        var dipX = Math.Min(start.X, end.X);
        var dipY = Math.Min(start.Y, end.Y);
        var dipWidth = Math.Abs(end.X - start.X);
        var dipHeight = Math.Abs(end.Y - start.Y);

        // Convert DIP (device-independent pixel) coordinates to physical
        // pixels, since Graphics.CopyFromScreen (RegionScreenshotCapturer)
        // works in physical pixels -- without this conversion, the saved
        // region would be wrong on any display scaled above 100%.
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var physicalTopLeft = transform.Transform(new Point(dipX, dipY));
        var physicalSize = transform.Transform(new Point(dipWidth, dipHeight));

        if (physicalSize.X < 5 || physicalSize.Y < 5)
        {
            // Too small to be a deliberate selection -- treat as a
            // cancel rather than saving a useless region.
            _startPoint = null;
            SelectionRectangle.Visibility = Visibility.Collapsed;
            return;
        }

        PickedRegion = new CaptureRegion((int)physicalTopLeft.X, (int)physicalTopLeft.Y, (int)physicalSize.X, (int)physicalSize.Y);
        DialogResult = true;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
