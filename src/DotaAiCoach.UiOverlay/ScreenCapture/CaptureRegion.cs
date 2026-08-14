namespace DotaAiCoach.UiOverlay.ScreenCapture;

/// <summary>
/// A screen region (physical pixels, primary-monitor-relative) the player
/// has configured as "where my scoreboard/KDA appears" — set once via the
/// visual region picker (see RegionPickerWindow) and reused for every
/// subsequent "Capturar pantalla" click. Deliberately NOT auto-detected
/// (no fixed geometry assumption): screen resolution, UI scaling, and
/// where the player positions their scoreboard hotkey view all vary, per
/// dota-cv's resolution-and-scaling guidance — even though this feature
/// doesn't use the dota-cv pipeline (no recognition happens locally; the
/// captured image is sent to the LLM for extraction), the same "don't
/// assume fixed screen geometry" principle applies to defining the
/// capture rectangle itself.
/// </summary>
public sealed record CaptureRegion(int X, int Y, int Width, int Height)
{
    public bool IsValid => Width > 0 && Height > 0;
}
