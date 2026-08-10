using System.ClientModel;
using DotaAiCoach.Domain.LlmCoach;
using OpenAI;
using OpenAI.Chat;

namespace DotaAiCoach.LlmCoach;

/// <summary>
/// Calls OpenAI for a one-off build recommendation from a manually
/// entered situation snapshot (own KDA + enemy scoreboard extraction, as
/// visible on the player's own screen). Deliberately separate from the
/// trained ML Item Ranker (see DotaAiCoach.MlInference) — this is the
/// "ask an LLM" path the user's prior manual ChatGPT workflow already
/// used, now wired into the app and logged (see SituationLogWriter) so
/// those responses can later inform retraining the trained model.
/// </summary>
public sealed class OpenAiSituationAdvisor : ISituationAdvisor
{
    private const string ModelId = "gpt-5";

    private readonly ChatClient _client;

    public OpenAiSituationAdvisor(ChatClient client)
    {
        _client = client;
    }

    public static OpenAiSituationAdvisor? TryCreate()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var client = new OpenAIClient(new ApiKeyCredential(apiKey)).GetChatClient(ModelId);
        return new OpenAiSituationAdvisor(client);
    }

    public async Task<LlmSituationAdvice> GetAdviceAsync(ManualSituationInput input, CancellationToken ct)
    {
        var prompt = BuildPrompt(input);

        var response = await _client.CompleteChatAsync(
            [ChatMessage.CreateUserMessage(prompt)],
            cancellationToken: ct);

        var text = string.Concat(response.Value.Content.Select(part => part.Text));

        return new LlmSituationAdvice(text, DateTimeOffset.UtcNow, ModelId);
    }

    private static string BuildPrompt(ManualSituationInput input)
    {
        var enemyLines = input.EnemyScoreboard.Enemies.Count == 0
            ? "(sin datos de scoreboard enemigo)"
            : string.Join("\n", input.EnemyScoreboard.Enemies.Select(FormatEnemy));

        var notes = string.IsNullOrWhiteSpace(input.ExtraNotes) ? "" : $"\nNotas adicionales: {input.ExtraNotes}";

        return $"""
            Eres un coach de Dota 2. Da una recomendacion de build de items
            concisa (siguiente 1-3 items, en orden) para la siguiente
            situacion de una partida en curso. Responde en espanol, con la
            recomendacion primero y una razon breve despues.

            Minuto de partida: {input.MinuteOfMatch}
            Mi heroe: {input.OwnHero}
            Mi KDA: {input.OwnKda}
            Heroes enemigos (del scoreboard):
            {enemyLines}{notes}
            """;
    }

    private static string FormatEnemy(EnemyScoreboardEntry entry)
    {
        var level = entry.Level is { } lvl ? $"nivel {lvl}" : "nivel desconocido";
        var kda = entry.Kda is { } k ? $"KDA {k.Kills}/{k.Deaths}/{k.Assists}" : "KDA desconocido";
        return $"- {entry.Hero}: {level}, {kda}";
    }
}
