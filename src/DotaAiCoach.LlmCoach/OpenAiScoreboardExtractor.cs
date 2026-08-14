using System.ClientModel;
using System.IO;
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
/// json_schema here — same guarantee, different SDK surface). Unlike
/// tickExtract.ts, no "expected enemies" hint is sent — the extractor
/// reports every enemy hero it can read, as-is. The player's own KDA is
/// never asked of the image — it's already known live from GSI (see
/// ManualSituationInput.OwnKda).
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
        CancellationToken ct)
    {
        await SaveDebugImageAsync(imageBytes, mediaType, ct);

        const string system = """
            Eres un extractor de datos de Dota 2. Recibiras un screenshot
            de la tabla de heroes (scoreboard) de una partida en curso,
            con ambos equipos.

            Devuelve un elemento en enemy_status por cada heroe ENEMIGO
            visible en la imagen (nunca incluyas heroes de mi propio
            equipo, solo del equipo contrario):
            - hero: el nombre del heroe tal como aparece en pantalla.
            - level: columna "LVL". Si no es legible, usa null.
            - kda {k,d,a}: columnas "K","D","A" en ese orden. Si no son
              legibles con claridad, usa null para todo el objeto kda --
              no adivines.

            No inventes heroes ni datos que no esten realmente visibles
            en la imagen.
            """;

        var messages = new ChatMessage[]
        {
            ChatMessage.CreateSystemMessage(system),
            ChatMessage.CreateUserMessage(
                ChatMessageContentPart.CreateTextPart("Extrae los datos del screenshot."),
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

        return ParseResult(json);
    }

    /// <summary>
    /// Saves every image sent for extraction to
    /// %LocalAppData%\DotaAiCoach\llm-situations\images\, timestamped, so
    /// the player can inspect exactly what was sent to OpenAI when
    /// debugging a bad extraction or an API error -- saved BEFORE the
    /// API call, so a request failure (e.g. an invalid key) still leaves
    /// the image on disk to inspect. Best-effort: never blocks the
    /// actual extraction call.
    /// </summary>
    private static async Task SaveDebugImageAsync(byte[] imageBytes, string mediaType, CancellationToken ct)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DotaAiCoach", "llm-situations", "images");
            Directory.CreateDirectory(directory);

            var extension = mediaType switch
            {
                "image/jpeg" => "jpg",
                "image/bmp" => "bmp",
                "image/gif" => "gif",
                _ => "png",
            };
            var fileName = $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}.{extension}";
            await File.WriteAllBytesAsync(Path.Combine(directory, fileName), imageBytes, ct);
        }
        catch
        {
            // Debug-only convenience -- never a reason to fail the
            // player-facing extraction/recommendation flow.
        }
    }

    internal static ScoreboardExtractionResult ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var entries = new List<EnemyScoreboardEntry>();
        foreach (var item in doc.RootElement.GetProperty("enemy_status").EnumerateArray())
        {
            var hero = item.GetProperty("hero").GetString();
            if (string.IsNullOrWhiteSpace(hero))
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
}
