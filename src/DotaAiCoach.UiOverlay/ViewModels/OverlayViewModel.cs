using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using DotaAiCoach.Domain.Match;
using DotaAiCoach.Domain.Recommendations;
using DotaAiCoach.Host;

namespace DotaAiCoach.UiOverlay.ViewModels;

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
    private readonly Dispatcher _dispatcher;

    public OverlayViewModel(ICoachSession session, Dispatcher dispatcher)
    {
        _session = session;
        _dispatcher = dispatcher;
        _session.Updated += OnUpdated;
        Messages = new ObservableCollection<LiveMessage>();
        ItemNames = new ObservableCollection<string>();
        Refresh();
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

    /// <summary>The local player's Steam64 as reported by GSI, if any — pre-fills the profile window's Steam ID field.</summary>
    public string? DetectedSteamId { get; private set; }
    public ObservableCollection<string> ItemNames { get; }
    public ObservableCollection<LiveMessage> Messages { get; }

    private void OnUpdated(object? sender, CoachUpdate update)
    {
        if (_dispatcher.CheckAccess())
        {
            Apply(update.State, update.Health, update.Messages);
        }
        else
        {
            _dispatcher.Invoke(() => Apply(update.State, update.Health, update.Messages));
        }
    }

    private void Refresh() => Apply(_session.CurrentState, _session.Health, _session.ActiveMessages);

    private void Apply(MatchState state, FeedHealth health, IReadOnlyList<LiveMessage> messages)
    {
        HeroName = FormatHeroName(state.Hero.Name);
        ClockText = FormatClock(state.Match.ClockTime);
        KdaText = $"{state.Player.Kills ?? 0}/{state.Player.Deaths ?? 0}/{state.Player.Assists ?? 0}";
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

        RaiseAllPropertiesChanged();
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
}
