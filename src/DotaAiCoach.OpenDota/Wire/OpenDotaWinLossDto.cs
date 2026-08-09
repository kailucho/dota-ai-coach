using System.Text.Json.Serialization;

namespace DotaAiCoach.OpenDota.Wire;

internal sealed class OpenDotaWinLossDto
{
    [JsonPropertyName("win")] public int? Win { get; init; }
    [JsonPropertyName("lose")] public int? Lose { get; init; }
}
