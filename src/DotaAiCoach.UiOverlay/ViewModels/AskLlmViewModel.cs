using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DotaAiCoach.Domain.LlmCoach;
using DotaAiCoach.Host;

namespace DotaAiCoach.UiOverlay.ViewModels;

/// <summary>
/// Backs the manual "ask for a build" panel: the player uploads a
/// screenshot of their own scoreboard (auto-extracted into enemy hero
/// level/KDA via OpenAiScoreboardExtractor -- mirrors the reference
/// project dota-plus-free's drag-and-drop-then-auto-extract flow), then
/// gets a one-off LLM build recommendation. Separate from
/// OverlayViewModel's live ItemPlan -- this is user-triggered, not a
/// continuous GSI-driven feed.
/// </summary>
public sealed class AskLlmViewModel : INotifyPropertyChanged
{
    private readonly ILlmCoachSession _session;
    private readonly Dispatcher _dispatcher;

    private string _minuteOfMatch = "";
    private string _ownHero = "";
    private string _ownKda = "";
    private string _enemyHeroesHint = "";
    private string _extraNotes = "";
    private string _responseText = "";
    private bool _isBusy;
    private bool _isExtracting;
    private string? _errorText;
    private BitmapImage? _screenshotPreview;
    private ScoreboardExtractionResult? _extraction;

    public AskLlmViewModel(ILlmCoachSession session, Dispatcher dispatcher)
    {
        _session = session;
        _dispatcher = dispatcher;
    }

    public bool IsAvailable => _session.IsAvailable;

    public string AvailabilityText => IsAvailable
        ? ""
        : "No hay OPENAI_API_KEY configurada. Configura la variable de entorno y reinicia la app.";

    public string StatusText => IsExtracting ? "Leyendo captura..." : IsBusy ? "Pensando..." : "";

    public string MinuteOfMatch { get => _minuteOfMatch; set => SetField(ref _minuteOfMatch, value); }
    public string OwnHero { get => _ownHero; set => SetField(ref _ownHero, value); }
    public string OwnKda { get => _ownKda; set => SetField(ref _ownKda, value); }

    /// <summary>
    /// Comma-separated expected enemy hero names -- optional, but
    /// restricts/normalizes what the extractor returns (mirrors
    /// tickExtract.ts's "enemies" hint).
    /// </summary>
    public string EnemyHeroesHint { get => _enemyHeroesHint; set => SetField(ref _enemyHeroesHint, value); }
    public string ExtraNotes { get => _extraNotes; set => SetField(ref _extraNotes, value); }
    public string ResponseText { get => _responseText; private set => SetField(ref _responseText, value); }
    public BitmapImage? ScreenshotPreview { get => _screenshotPreview; private set => SetField(ref _screenshotPreview, value); }

    public string ExtractionSummary => _extraction is null
        ? "Sube una captura del scoreboard para autocompletar los enemigos."
        : _extraction.Enemies.Count == 0
            ? "No se detectaron heroes enemigos en la imagen."
            : string.Join("\n", _extraction.Enemies.Select(e =>
                $"{e.Hero}: nivel {(e.Level?.ToString() ?? "?")}, KDA {(e.Kda is { } k ? $"{k.Kills}/{k.Deaths}/{k.Assists}" : "?")}"));

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            }
        }
    }

    public bool IsExtracting
    {
        get => _isExtracting;
        private set
        {
            if (SetField(ref _isExtracting, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            }
        }
    }

    public string? ErrorText { get => _errorText; private set => SetField(ref _errorText, value); }

    public async Task ExtractScoreboardAsync(string filePath, CancellationToken ct)
    {
        IsExtracting = true;
        ErrorText = null;

        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath, ct);
            var mediaType = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".gif" => "image/gif",
                _ => "image/png",
            };

            var expectedHeroes = EnemyHeroesHint
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var result = await _session.ExtractScoreboardAsync(bytes, mediaType, expectedHeroes, ct);

            await _dispatcher.InvokeAsync(() =>
            {
                _extraction = result;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExtractionSummary)));

                var preview = new BitmapImage();
                preview.BeginInit();
                preview.CacheOption = BitmapCacheOption.OnLoad;
                preview.UriSource = new Uri(filePath);
                preview.EndInit();
                preview.Freeze();
                ScreenshotPreview = preview;
            });
        }
        catch (Exception ex)
        {
            await _dispatcher.InvokeAsync(() => ErrorText = $"No se pudo leer la captura: {ex.Message}");
        }
        finally
        {
            await _dispatcher.InvokeAsync(() => IsExtracting = false);
        }
    }

    public async Task AskAsync(CancellationToken ct)
    {
        if (!int.TryParse(MinuteOfMatch, out var minute))
        {
            ErrorText = "El minuto debe ser un numero.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OwnHero))
        {
            ErrorText = "Completa tu heroe.";
            return;
        }

        var scoreboard = _extraction ?? new ScoreboardExtractionResult(Array.Empty<EnemyScoreboardEntry>());
        var input = new ManualSituationInput(minute, OwnHero, OwnKda, scoreboard, ExtraNotes);

        IsBusy = true;
        ErrorText = null;
        ResponseText = "";

        try
        {
            var advice = await _session.AskForAdviceAsync(input, ct);
            await _dispatcher.InvokeAsync(() => ResponseText = advice.ResponseText);
        }
        catch (Exception ex)
        {
            await _dispatcher.InvokeAsync(() => ErrorText = ex.Message);
        }
        finally
        {
            await _dispatcher.InvokeAsync(() => IsBusy = false);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
