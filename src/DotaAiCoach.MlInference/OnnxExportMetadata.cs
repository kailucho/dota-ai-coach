using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotaAiCoach.MlInference;

/// <summary>
/// Minimal deserialization shape for the subset of onnx_export_metadata.json
/// this project actually consumes at runtime (calibration breakpoints).
/// The full file is bundled and available for humans/debugging, but only
/// the calibration_replication block is read programmatically here — this
/// keeps the C# side of calibration replication tied to the single
/// authoritative source written by export_onnx.py (docs/adr/0022) rather
/// than a hand-copied duplicate that could silently drift.
/// </summary>
internal sealed class OnnxExportMetadata
{
    [JsonPropertyName("calibration_replication")]
    public CalibrationReplication? CalibrationReplication { get; init; }

    public static OnnxExportMetadata Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<OnnxExportMetadata>(json)
            ?? throw new InvalidOperationException($"Could not parse onnx_export_metadata.json at {path}");
    }
}

internal sealed class CalibrationReplication
{
    [JsonPropertyName("x_thresholds")]
    public required double[] XThresholds { get; init; }

    [JsonPropertyName("y_thresholds")]
    public required double[] YThresholds { get; init; }
}
