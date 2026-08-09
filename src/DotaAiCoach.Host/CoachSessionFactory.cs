namespace DotaAiCoach.Host;

/// <summary>
/// The only construction entry point consumers (the overlay) should use.
/// Keeps GsiOptions/GsiListener construction details inside this assembly
/// so a caller like DotaAiCoach.UiOverlay never needs to reference
/// DotaAiCoach.Gsi types directly, even though .NET's default transitive
/// compilation would otherwise make them visible — see
/// DotaAiCoach.Architecture.Tests for the assertion that UiOverlay has no
/// reference edge to Gsi at all.
/// </summary>
public static class CoachSessionFactory
{
    public static ICoachSession Create(int port = 3000, string route = "/gsi", string? authToken = null)
    {
        var options = new Gsi.GsiOptions { Port = port, Route = route, AuthToken = authToken };
        return new CoachSession(options, Microsoft.Extensions.Logging.Abstractions.NullLogger<Gsi.Listener.GsiListener>.Instance);
    }
}
