namespace DotaAiCoach.PlayerProfile.Identity;

/// <summary>
/// A player identity normalized to Steam32 (OpenDota's account_id form).
/// Accepts either a pasted Steam64 (as seen in GSI's player.steamid) or a
/// bare Steam32 and converts consistently, per dota-data's identifier
/// guidance: pick one canonical form and never let both coexist unlabeled.
/// </summary>
public readonly record struct SteamId
{
    public const long Steam64Base = 76561197960265728L;

    /// <summary>Steam32 is a 32-bit unsigned range in practice, but Steam
    /// IDs are issued sequentially from a small number, so this upper
    /// bound is generous headroom rather than the literal uint.MaxValue.</summary>
    private const long MaxPlausibleSteam32 = 4_000_000_000L;

    public long Steam32 { get; }

    private SteamId(long steam32) => Steam32 = steam32;

    public long Steam64 => Steam32 + Steam64Base;

    public static bool TryParse(string? input, out SteamId steamId)
    {
        steamId = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!long.TryParse(input.Trim(), out var value) || value <= 0)
        {
            return false;
        }

        var steam32 = value >= Steam64Base ? value - Steam64Base : value;

        if (steam32 <= 0 || steam32 > MaxPlausibleSteam32)
        {
            return false;
        }

        steamId = new SteamId(steam32);
        return true;
    }

    public static SteamId FromSteam32(long steam32) => new(steam32);

    public override string ToString() => Steam32.ToString();
}
