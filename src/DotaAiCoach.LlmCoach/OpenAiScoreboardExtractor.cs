using System.ClientModel;
using System.Text.Json;
using DotaAiCoach.Domain.LlmCoach;
using OpenAI;
using OpenAI.Chat;

namespace DotaAiCoach.LlmCoach;

/// <summary>
/// Extracts enemy hero level/KDA from a player-captured scoreboard
/// screenshot via OpenAI vision + Structured Outputs (a strict JSON
/// Schema response format, guaranteeing valid, parseable output). Ports
/// the same prompt/schema shape as the reference project
/// dota-plus-free's packages/server/src/routes/tickExtract.ts (Responses
/// API + forced tool call there; Chat Completions + response_format
/// json_schema here — same guarantee, different SDK surface).
/// </summary>
public sealed class OpenAiScoreboardExtractor : IScoreboardExtractor
{
    private const string ModelId = "gpt-5";

    private static readonly BinaryData Schema = BinaryData.FromBytes("""
        {
          "type": "object",
          "properties": {
            "enemy_status": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "hero": { "type": "string" },
                  "level": { "type": ["integer", "null"] },
                  "kda": {
                    "type": ["object", "null"],
                    "properties": {
                      "k": { "type": "integer" },
                      "d": { "type": "integer" },
                      "a": { "type": "integer" }
                    },
                    "required": ["k", "d", "a"],
                    "additionalProperties": false
                  }
                },
                "required": ["hero", "level", "kda"],
                "additionalProperties": false
              }
            }
          },
          "required": ["enemy_status"],
          "additionalProperties": false
        }
        """u8.ToArray());

    private readonly ChatClient _client;

    public OpenAiScoreboardExtractor(ChatClient client)
    {
        _client = client;
    }

    public static OpenAiScoreboardExtractor? TryCreate()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var client = new OpenAIClient(new ApiKeyCredential(apiKey)).GetChatClient(ModelId);
        return new OpenAiScoreboardExtractor(client);
    }

    public async Task<ScoreboardExtractionResult> ExtractAsync(
        byte[] imageBytes,
        string mediaType,
        IReadOnlyList<string> expectedEnemyHeroes,
        CancellationToken ct)
    {
        var system = """
            Eres un extractor de datos de Dota 2. Recibiras un screenshot
            de la tabla de heroes (scoreboard) de una partida en curso.

            Devuelve SOLO para los heroes ENEMIGOS de la lista de entrada:
            - hero: igual al nombre en la lista.
            - level: columna "LVL". Si no es legible, usa null.
            - kda {k,d,a}: columnas "K","D","A" en ese orden. Si no son
              legibles con claridad, usa null para todo el objeto kda -- no
              adivines.

            Limita la salida a los heroes de la lista de entrada
            (normaliza mayusculas/minusculas). No inventes heroes que no
            esten en la lista.
            """;

        var enemiesHint = expectedEnemyHeroes.Count > 0
            ? $"Enemigos esperados (restringe la salida a estos): {string.Join(", ", expectedEnemyHeroes)}"
            : "No se proporciono lista de enemigos esperados; extrae todos los heroes que veas en la mitad enemiga de la tabla.";

        var messages = new ChatMessage[]
        {
            ChatMessage.CreateSystemMessage(system),
            ChatMessage.CreateUserMessage(
                ChatMessageContentPart.CreateTextPart($"Extrae los datos del screenshot.\n{enemiesHint}"),
                ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(imageBytes), mediaType)),
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "emit_scoreboard_extraction",
                Schema,
                jsonSchemaIsStrict: true),
        };

        var response = await _client.CompleteChatAsync(messages, options, ct);
        var json = response.Value.Content[0].Text;

        return ParseResult(json, expectedEnemyHeroes);
    }

    internal static ScoreboardExtractionResult ParseResult(string json, IReadOnlyList<string> expectedEnemyHeroes)
    {
        using var doc = JsonDocument.Parse(json);
        var enemyStatus = doc.RootElement.GetProperty("enemy_status");

        // Normalize each returned hero name against the expected list
        // (case-insensitive exact match, then substring match), mirroring
        // tickExtract.ts's normalization -- and drop entries that don't
        // match anything on the expected list, when one was provided.
        var entries = new List<EnemyScoreboardEntry>();
        foreach (var item in enemyStatus.EnumerateArray())
        {
            var rawHero = item.GetProperty("hero").GetString() ?? "";
            var hero = NormalizeHero(rawHero, expectedEnemyHeroes);
            if (hero is null)
            {
                continue;
            }

            int? level = item.TryGetProperty("level", out var levelEl) && levelEl.ValueKind == JsonValueKind.Number
                ? levelEl.GetInt32()
                : null;

            ScoreboardKda? kda = null;
            if (item.TryGetProperty("kda", out var kdaEl) && kdaEl.ValueKind == JsonValueKind.Object)
            {
                kda = new ScoreboardKda(
                    kdaEl.GetProperty("k").GetInt32(),
                    kdaEl.GetProperty("d").GetInt32(),
                    kdaEl.GetProperty("a").GetInt32());
            }

            entries.Add(new EnemyScoreboardEntry(hero, level, kda));
        }

        return new ScoreboardExtractionResult(entries);
    }

    internal static string? NormalizeHero(string rawHero, IReadOnlyList<string> expectedEnemyHeroes)
    {
        if (expectedEnemyHeroes.Count == 0)
        {
            return rawHero;
        }

        var exact = expectedEnemyHeroes.FirstOrDefault(e => string.Equals(e, rawHero, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        return expectedEnemyHeroes.FirstOrDefault(e =>
            rawHero.Contains(e, StringComparison.OrdinalIgnoreCase));
    }
}
