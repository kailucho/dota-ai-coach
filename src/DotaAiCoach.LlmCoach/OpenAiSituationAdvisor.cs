using System.ClientModel;
using System.Text.Json;
using DotaAiCoach.Domain.LlmCoach;
using DotaAiCoach.Domain.Recommendations;
using OpenAI;
using OpenAI.Chat;

namespace DotaAiCoach.LlmCoach;

/// <summary>
/// Calls OpenAI for a one-off item plan from a manually captured situation
/// snapshot (own KDA + enemy scoreboard extraction, as visible on the
/// player's own screen). Deliberately separate from the trained ML Item
/// Ranker (see DotaAiCoach.MlInference) -- this is the "ask an LLM" path
/// the user's prior manual ChatGPT workflow already used, now wired into
/// the app and logged (see SituationLogWriter) so those responses can
/// later inform retraining the trained model. Returns the same ItemPlan
/// shape the ML-driven panel uses (see Domain.Recommendations.ItemPlan)
/// so the overlay can display either source in the same EARLY/MID/LATE/
/// SITUATIONAL grid without a second rendering path.
/// </summary>
public sealed class OpenAiSituationAdvisor : ISituationAdvisor
{
    private const string ModelId = "gpt-5";

    private static readonly BinaryData Schema = BinaryData.FromBytes("""
        {
          "type": "object",
          "properties": {
            "item_plan": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "phase": { "type": "string", "enum": ["Early", "Mid", "Late", "Situational"] },
                  "item_display_name": { "type": "string" },
                  "item_id": { "type": "string" },
                  "priority_percent": { "type": "integer" }
                },
                "required": ["phase", "item_display_name", "item_id", "priority_percent"],
                "additionalProperties": false
              }
            }
          },
          "required": ["item_plan"],
          "additionalProperties": false
        }
        """u8.ToArray());

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

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "emit_item_plan",
                Schema,
                jsonSchemaIsStrict: true),
        };

        var response = await _client.CompleteChatAsync(
            [ChatMessage.CreateUserMessage(prompt)],
            options,
            ct);

        var json = response.Value.Content[0].Text;
        var itemPlan = ParseItemPlan(json);

        return new LlmSituationAdvice(itemPlan, DateTimeOffset.UtcNow, ModelId);
    }

    private static string BuildPrompt(ManualSituationInput input)
    {
        var ownKda = input.OwnKda is { } k ? $"{k.Kills}/{k.Deaths}/{k.Assists}" : "desconocido";
        var enemyLines = input.EnemyScoreboard.Enemies.Count == 0
            ? "(sin datos de scoreboard enemigo)"
            : string.Join("\n", input.EnemyScoreboard.Enemies.Select(FormatEnemy));

        var notes = string.IsNullOrWhiteSpace(input.ExtraNotes) ? "" : $"\nNotas adicionales: {input.ExtraNotes}";

        var gold = input.Gold is { } g ? g.ToString() : "desconocido";
        var level = input.HeroLevel is { } lvl ? lvl.ToString() : "desconocido";
        var ownItems = input.OwnItems is { Count: > 0 } items
            ? string.Join(", ", items)
            : "(inventario vacio o desconocido)";
        var rankLine = input.RankTier is { } rank ? $"\nMi rango promedio (rank_tier): {rank}" : "";
        var patchLine = string.IsNullOrWhiteSpace(input.GamePatch) ? "" : $"\nParche actual de Dota 2: {input.GamePatch}";

        return $"""
            Eres un coach de Dota 2. Da un plan de items completo (early,
            mid, late y situational) para la siguiente situacion de una
            partida en curso, tal como se veria en una guia de items del
            propio Dota 2.

            Para cada item que recomiendes, indica:
            - phase: "Early", "Mid", "Late" o "Situational".
            - item_display_name: el nombre del item en ingles tal como
              aparece en Dota 2 (ej. "Black King Bar").
            - item_id: el nombre interno del item en formato Steam CDN,
              en minusculas y guion bajo, SIN el prefijo "item_" (ej.
              "black_king_bar", "power_treads", "blink").
            - priority_percent: un entero de 0 a 100 que indica que tan
              prioritario es este item AHORA MISMO frente a los demas de
              su misma fase, tal como los porcentajes que Dota 2 muestra
              en su propia guia de items post-partida. No es una tasa de
              victoria -- es tu prioridad relativa dada la situacion
              actual (heroes enemigos, mi oro, mi inventario, el minuto
              de partida). El item mas prioritario de cada fase debe
              tener el porcentaje mas alto de esa fase.

            No recomiendes items que ya tengo en mi inventario actual, ni
            items redundantes con ellos. Ten en cuenta mi oro actual para
            priorizar lo que es comprable pronto, no solo lo teoricamente
            ideal. Da entre 2 y 5 items por fase, en orden de prioridad de
            compra (de mayor a menor priority_percent). No repitas items
            entre fases.

            Minuto de partida: {input.MinuteOfMatch}
            Mi heroe: {input.OwnHero}
            Mi nivel: {level}
            Mi KDA: {ownKda}
            Mi oro actual: {gold}
            Mi inventario actual: {ownItems}{rankLine}{patchLine}
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

    internal static ItemPlan ParseItemPlan(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var entries = new List<ItemPlanEntry>();
        foreach (var item in doc.RootElement.GetProperty("item_plan").EnumerateArray())
        {
            var phase = item.GetProperty("phase").GetString();
            var displayName = item.GetProperty("item_display_name").GetString();
            var itemId = item.GetProperty("item_id").GetString();

            if (string.IsNullOrWhiteSpace(phase) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(itemId))
            {
                continue;
            }

            var normalizedId = itemId.StartsWith("item_", StringComparison.Ordinal) ? itemId : $"item_{itemId}";
            var priorityPercent = item.TryGetProperty("priority_percent", out var priorityProp) && priorityProp.ValueKind == JsonValueKind.Number
                ? Math.Clamp(priorityProp.GetInt32(), 0, 100)
                : (int?)null;
            entries.Add(new ItemPlanEntry(phase, displayName, normalizedId, priorityPercent));
        }

        return new ItemPlan(entries);
    }
}
