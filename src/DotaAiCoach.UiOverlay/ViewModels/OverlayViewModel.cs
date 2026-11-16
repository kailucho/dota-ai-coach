using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DotaAiCoach.Domain.LlmCoach;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.Host;
using DotaAiCoach.UiOverlay.Interop;
using DotaAiCoach.UiOverlay.ScreenCapture;
using WpfKeyInterop = System.Windows.Input.KeyInterop;

namespace DotaAiCoach.UiOverlay.ViewModels;

/// <summary>Display-ready item icon cell within a phase's grid — icon loads asynchronously, starting null (blank cell with a tooltip name) until resolved.</summary>
public sealed class ItemPlanDisplayEntry : INotifyPropertyChanged
{
    public string ItemDisplayName { get; }

    /// <summary>0-100 LLM-assigned priority, shown as a "57%"-style label over the icon like Dota's own post-game item guide (see ItemPlanEntry.PriorityPercent). Null when the source plan didn't provide one (the rule/ML-driven path) — the label is hidden in that case, not shown as "0%".</summary>
    public int? PriorityPercent { get; }
    public bool HasPriorityPercent => PriorityPercent is not null;
    public string PriorityPercentText => PriorityPercent is { } p ? $"{p}%" : "";

    /// <summary>True if this item is already in the player's inventory/stash/neutral slots (see ItemsSnapshot.Owns) — shown with a checkmark badge instead of being removed from its phase, per explicit request to keep the full planned build visible.</summary>
    public bool IsOwned { get; }

    private BitmapImage? _iconImage;
    public BitmapImage? IconImage
    {
        get => _iconImage;
        set { _iconImage = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconImage))); }
    }

    public ItemPlanDisplayEntry(string itemDisplayName, int? priorityPercent, bool isOwned)
    {
        ItemDisplayName = itemDisplayName;
        PriorityPercent = priorityPercent;
        IsOwned = isOwned;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// One phase's labeled group of item icons — e.g. "EARLY" with a grid of
/// item cells, matching Dota's own in-game item guide layout (a heading
/// per category, several item icons in a grid underneath) per the user's
/// explicit request.
/// </summary>
public sealed class ItemPlanPhaseGroup
{
    public string PhaseLabel { get; }
    public ObservableCollection<ItemPlanDisplayEntry> Items { get; }

    public ItemPlanPhaseGroup(string phaseLabel, ObservableCollection<ItemPlanDisplayEntry> items)
    {
        PhaseLabel = phaseLabel;
        Items = items;
    }
}

/// <summary>
/// Formats and binds ICoachSession's Domain-only output for display.
/// Contains zero recommendation/strategy logic — every value here is a
/// direct format of a Domain field, per dota-architecture's "UI must not
/// contain recommendation logic" rule and dota-overlay's guidance that
/// components render already-decided content, they don't decide it.
/// </summary>
public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private readonly ICoachSession _session;
    private readonly ILlmCoachSession _llmSession;
    private readonly IProfileSession _profileSession;
    private readonly Dispatcher _dispatcher;
    private readonly ItemIconCache _iconCache;

    /// <summary>
    /// Set once the player runs the "capture scoreboard" flow — from that
    /// point on, the LLM's item plan replaces the ML-driven one in
    /// ItemPlanGroups on every subsequent GSI update, until the player
    /// captures another scoreboard image (per explicit request: the LLM's
    /// answer is a standing replacement for that match, not a temporary
    /// overlay).
    /// </summary>
    private ItemPlan? _llmItemPlan;

    /// <summary>
    /// Throttles ItemPlanGroups rebuilds to once per <see cref="ItemPlanRefreshInterval"/>
    /// — the rule-driven fallback plan otherwise recomputes on every GSI
    /// payload (~1/s), causing the item guide grid to visibly reflow every
    /// second even though nothing strategically relevant changed. Only the
    /// item guide panel is throttled; clock/gold/KDA/messages still update
    /// live on every payload.
    /// </summary>
    private static readonly TimeSpan ItemPlanRefreshInterval = TimeSpan.FromMinutes(2);
    private DateTime _lastItemPlanRefreshUtc = DateTime.MinValue;
    private CaptureRegion? _captureRegion;
    private bool _isCapturing;
    private string? _captureErrorText;
    private bool _isExpanded = true;
    private string? _gamePatch;

    public OverlayViewModel(ICoachSession session, ILlmCoachSession llmSession, IProfileSession profileSession, Dispatcher dispatcher, ItemIconCache? iconCache = null)
    {
        _session = session;
        _llmSession = llmSession;
        _profileSession = profileSession;
        _dispatcher = dispatcher;
        _iconCache = iconCache ?? new ItemIconCache();
        _captureRegion = CaptureRegionSettings.Load().Region;
        _session.Updated += OnUpdated;
        Messages = new ObservableCollection<LiveMessage>();
        ItemNames = new ObservableCollection<string>();
        ItemPlanGroups = new ObservableCollection<ItemPlanPhaseGroup>();
        Refresh();
        _ = LoadGamePatchAsync();
    }

    /// <summary>Current Dota 2 patch (e.g. "7.38c"), shown next to the hero name — fetched once from OpenDota, see IProfileSession.GetCurrentGamePatchAsync.</summary>
    public string GamePatchText => _gamePatch ?? "-";

    private async Task LoadGamePatchAsync()
    {
        var patch = await _profileSession.GetCurrentGamePatchAsync(CancellationToken.None);
        if (patch is null)
        {
            return;
        }

        await _dispatcher.InvokeAsync(() =>
        {
            _gamePatch = patch;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GamePatchText)));
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string HeroName { get; private set; } = "-";
    public string ClockText { get; private set; } = "--:--";
    public string KdaText { get; private set; } = "-/-/-";
    public string GoldText { get; private set; } = "-";
    public string GpmXpmText { get; private set; } = "-";
    public string LastHitDenyText { get; private set; } = "-";
    public string BuybackText { get; private set; } = "Unknown";
    public string FeedStatusText { get; private set; } = "Not connected";

    /// <summary>
    /// Raw (unformatted-for-display) live state, exposed so other panels
    /// (e.g. AskLlmViewModel's "ask for a build" form) can auto-fill
    /// minute/hero/own-KDA from the same GSI-derived state driving this
    /// panel, instead of asking the player to type it again.
    /// </summary>
    public bool HasActiveMatchData { get; private set; }
    public int GameMinute { get; private set; }
    public int? Kills { get; private set; }
    public int? Deaths { get; private set; }
    public int? Assists { get; private set; }
    public int? Gold { get; private set; }
    public int? HeroLevel { get; private set; }

    /// <summary>The local player's Steam64 as reported by GSI, if any — pre-fills the profile window's Steam ID field.</summary>
    public string? DetectedSteamId { get; private set; }
    public ObservableCollection<string> ItemNames { get; }
    public ObservableCollection<LiveMessage> Messages { get; }

    /// <summary>
    /// The always-visible item plan, grouped by phase (Early/Mid/Late/
    /// Situational) — deliberately not part of Messages/LiveMessageBudget's
    /// cooldown-gated feed (see ICoachSession.CurrentItemPlan /
    /// dota-live-coach). Each phase group holds several item icon cells,
    /// mirroring Dota's own in-game item guide grid layout. Icons load
    /// asynchronously from ItemIconCache.
    /// </summary>
    public ObservableCollection<ItemPlanPhaseGroup> ItemPlanGroups { get; }

    /// <summary>
    /// Replaces the live ML-driven item plan with the LLM's answer,
    /// rendered in the same EARLY/MID/LATE/SITUATIONAL grid. Stays in
    /// effect (surviving further GSI updates) until the next call.
    /// </summary>
    public void SetLlmItemPlan(ItemPlan itemPlan)
    {
        _llmItemPlan = itemPlan;
        Refresh();
    }

    public bool HasCaptureRegion => _captureRegion is { IsValid: true };

    public string CaptureRegionText => HasCaptureRegion
        ? $"Region: {_captureRegion!.Width}x{_captureRegion.Height} en ({_captureRegion.X},{_captureRegion.Y})"
        : "Sin region configurada -- usa 'Configurar region'.";

    public bool IsCapturing
    {
        get => _isCapturing;
        private set => SetField(ref _isCapturing, value);
    }

    /// <summary>
    /// Whether the body below the header is showing. False collapses the
    /// window down to just its draggable header strip (see MainWindow's
    /// minimize button) instead of hiding the window entirely.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetField(ref _isExpanded, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CollapseGlyph)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CollapseTooltip)));
            }
        }
    }

    public string CollapseGlyph => IsExpanded ? "_" : "▢";
    public string CollapseTooltip => IsExpanded ? "Minimizar a barra" : "Expandir";

    public string? CaptureErrorText { get => _captureErrorText; private set => SetField(ref _captureErrorText, value); }

    /// <summary>Records a newly-picked region (from RegionPickerWindow) and persists it for future captures.</summary>
    public void SetCaptureRegion(CaptureRegion region)
    {
        _captureRegion = region;
        new CaptureRegionSettings { Region = region }.Save();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasCaptureRegion)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CaptureRegionText)));
    }

    public string HotKeyStatusText { get; private set; } = "";

    /// <summary>Reflects the currently registered capture hotkey (or a failure to register it) for display next to the capture buttons.</summary>
    public void SetHotKeyStatus(bool registered, HotKeySettings settings)
    {
        var combo = FormatHotKeyCombo(settings);
        HotKeyStatusText = registered
            ? $"Tecla de captura: {combo}"
            : $"No se pudo registrar {combo} (en uso por otra app) -- usa 'Configurar tecla'.";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HotKeyStatusText)));
    }

    private static string FormatHotKeyCombo(HotKeySettings settings)
    {
        var modifiers = (HotKeyModifiers)settings.Modifiers;
        var parts = new List<string>();
        if (modifiers.HasFlag(HotKeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotKeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotKeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(HotKeyModifiers.Win)) parts.Add("Win");
        parts.Add(WpfKeyInterop.KeyFromVirtualKey(settings.VirtualKeyCode).ToString());
        return string.Join(" + ", parts);
    }

    /// <summary>
    /// One-click flow, triggered directly from the main overlay's
    /// "Capturar pantalla" button (per explicit request to drop the
    /// separate Ask LLM window): screenshot the configured region (the
    /// player has already opened Dota's scoreboard on their own screen --
    /// fairPlayClassification: VISIBLE_SCREEN), extract the enemy
    /// scoreboard, ask the LLM for a full item plan, and replace this
    /// panel's ItemPlanGroups with it -- no second window, no extra input.
    /// </summary>
    public async Task CaptureAndAskLlmAsync(CancellationToken ct)
    {
        if (!_llmSession.IsAvailable)
        {
            CaptureErrorText = "No hay OPENAI_API_KEY configurada.";
            return;
        }

        if (_captureRegion is not { IsValid: true } region)
        {
            CaptureErrorText = "Configura la region de captura primero.";
            return;
        }

        if (!HasActiveMatchData)
        {
            CaptureErrorText = "No hay datos de partida en vivo todavia.";
            return;
        }

        IsCapturing = true;
        CaptureErrorText = null;

        try
        {
            var bytes = RegionScreenshotCapturer.Capture(region);
            var scoreboard = await _llmSession.ExtractScoreboardAsync(bytes, "image/png", ct);

            var ownKda = Kills is not null || Deaths is not null || Assists is not null
                ? new ScoreboardKda(Kills ?? 0, Deaths ?? 0, Assists ?? 0)
                : null;

            var input = new ManualSituationInput(
                GameMinute,
                HeroName,
                ownKda,
                scoreboard,
                ExtraNotes: null,
                Gold: Gold,
                OwnItems: ItemNames.ToList(),
                HeroLevel: HeroLevel,
                RankTier: _profileSession.Current.RankTier,
                GamePatch: _gamePatch);
            var advice = await _llmSession.AskForAdviceAsync(input, ct);

            await _dispatcher.InvokeAsync(() => SetLlmItemPlan(advice.ItemPlan));
        }
        catch (Exception ex)
        {
            await _dispatcher.InvokeAsync(() => CaptureErrorText = ex.Message);
        }
        finally
        {
            await _dispatcher.InvokeAsync(() => IsCapturing = false);
        }
    }

    private void OnUpdated(object? sender, CoachUpdate update)
    {
        if (_dispatcher.CheckAccess())
        {
            Apply(update.State, update.Health, update.Messages, update.ItemPlan);
        }
        else
        {
            _dispatcher.Invoke(() => Apply(update.State, update.Health, update.Messages, update.ItemPlan));
        }
    }

    private void Refresh() => Apply(_session.CurrentState, _session.Health, _session.ActiveMessages, _session.CurrentItemPlan);

    private void Apply(MatchState state, FeedHealth health, IReadOnlyList<LiveMessage> messages, ItemPlan itemPlan)
    {
        HeroName = FormatHeroName(state.Hero.Name);
        ClockText = FormatClock(state.Match.ClockTime);
        KdaText = $"{state.Player.Kills ?? 0}/{state.Player.Deaths ?? 0}/{state.Player.Assists ?? 0}";
        HasActiveMatchData = state.Hero.Name is not null && state.Match.ClockTime is not null;
        GameMinute = (state.Match.ClockTime ?? 0) / 60;
        Kills = state.Player.Kills;
        Deaths = state.Player.Deaths;
        Assists = state.Player.Assists;
        Gold = state.Player.Gold;
        HeroLevel = state.Hero.Level;
        GoldText = state.Player.Gold?.ToString() ?? "-";
        GpmXpmText = $"{state.Player.Gpm ?? 0} / {state.Player.Xpm ?? 0}";
        LastHitDenyText = $"{state.Player.LastHits ?? 0} / {state.Player.Denies ?? 0}";
        BuybackText = FormatBuyback(state.Hero.BuybackCost, state.Hero.BuybackCooldown);
        DetectedSteamId = state.Player.SteamId;
        FeedStatusText = health.Status switch
        {
            FeedStatus.Live => "Live",
            FeedStatus.Stale => "Stale - reconnecting",
            _ => "Not connected"
        };
        ItemNames.Clear();
        foreach (var slot in state.Items.Inventory)
        {
            if (!slot.IsEmpty && slot.Name is not null)
            {
                ItemNames.Add(slot.Name);
            }
        }

        Messages.Clear();
        foreach (var message in messages)
        {
            Messages.Add(message);
        }

        var now = DateTime.UtcNow;
        if (now - _lastItemPlanRefreshUtc >= ItemPlanRefreshInterval)
        {
            _lastItemPlanRefreshUtc = now;
            ItemPlanGroups.Clear();
            // Group in the order phases already arrive (NextItemSelector emits
            // Early, then Mid, then Late, then Situational — see AllPhases),
            // never re-sorted alphabetically, so the guide reads in
            // progression order like a real Dota item guide. Once the player
            // has asked the LLM, its plan takes over from the ML plan (see
            // SetLlmItemPlan) until the next capture.
            var effectivePlan = _llmItemPlan ?? itemPlan;
            foreach (var phaseGroup in effectivePlan.Entries.GroupBy(e => e.PhaseLabel))
            {
                var items = new ObservableCollection<ItemPlanDisplayEntry>();
                var group = new ItemPlanPhaseGroup(phaseGroup.Key.ToUpperInvariant(), items);
                ItemPlanGroups.Add(group);

                foreach (var entry in phaseGroup)
                {
                    var display = new ItemPlanDisplayEntry(entry.ItemDisplayName, entry.PriorityPercent, state.Items.Owns(entry.ItemId));
                    items.Add(display);
                    LoadIconAsync(display, entry.ItemId);
                }
            }
        }

        RaiseAllPropertiesChanged();
    }

    private async void LoadIconAsync(ItemPlanDisplayEntry entry, string itemId)
    {
        // Fire-and-forget by design: this is a best-effort icon fetch for
        // a display row that already has its text visible. A failure
        // (network, unmapped id) leaves IconImage null — the XAML
        // template shows text-only in that case, never an error.
        var icon = await _iconCache.GetIconAsync(itemId, CancellationToken.None);
        if (_dispatcher.CheckAccess())
        {
            entry.IconImage = icon;
        }
        else
        {
            _dispatcher.Invoke(() => entry.IconImage = icon);
        }
    }

    private static string FormatHeroName(string? internalName)
    {
        if (string.IsNullOrEmpty(internalName))
        {
            return "-";
        }

        const string prefix = "npc_dota_hero_";
        var trimmed = internalName.StartsWith(prefix, StringComparison.Ordinal)
            ? internalName[prefix.Length..]
            : internalName;

        return trimmed.Replace('_', ' ');
    }

    private static string FormatClock(int? clockSeconds)
    {
        if (clockSeconds is not { } seconds)
        {
            return "--:--";
        }

        var sign = seconds < 0 ? "-" : "";
        var abs = Math.Abs(seconds);
        return $"{sign}{abs / 60:00}:{abs % 60:00}";
    }

    private static string FormatBuyback(int? cost, int? cooldown)
    {
        if (cost is null)
        {
            return "Unknown";
        }

        return cooldown is > 0 ? $"On cooldown ({cooldown}s)" : $"Available ({cost}g)";
    }

    private void RaiseAllPropertiesChanged([CallerMemberName] string? _ = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

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
