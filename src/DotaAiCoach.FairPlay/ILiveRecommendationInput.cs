namespace DotaAiCoach.FairPlay;

/// <summary>
/// Marker interface for any type reachable as input to a live
/// recommendation. Combined with <see cref="FairPlaySourceAttribute"/>,
/// this lets DotaAiCoach.FairPlay.Tests walk the reachable type graph from
/// a live recommendation engine's entry point and assert, mechanically,
/// that nothing PostgameOnly/SpectatorOnly/Forbidden can be smuggled in —
/// per dota-fairplay's "structural unreachability" testing guidance.
/// </summary>
public interface ILiveRecommendationInput
{
}
