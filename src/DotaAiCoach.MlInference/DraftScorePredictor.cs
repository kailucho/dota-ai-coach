using DotaAiCoach.Domain.MlInference;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DotaAiCoach.MlInference;

/// <summary>
/// ONNX Runtime-backed implementation of IDraftScorePredictor, for the
/// draft-baseline model (docs/adr/0022). Feature order copied verbatim
/// from Models/draft-baseline/onnx_export_metadata.json's
/// input_schema.feature_order (hero_winrate_diff, matchup_advantage).
/// </summary>
public sealed class DraftScorePredictor : IDraftScorePredictor, IDisposable
{
    private const string ModelFolder = "draft-baseline";
    private readonly InferenceSession _session;
    private readonly IsotonicCalibrator _calibrator;

    public DraftScorePredictor()
        : this(
            ModelPathResolver.ResolvePath(ModelFolder, "model.onnx"),
            ModelPathResolver.ResolvePath(ModelFolder, "onnx_export_metadata.json"))
    {
    }

    internal DraftScorePredictor(string onnxModelPath, string onnxExportMetadataPath)
    {
        _session = new InferenceSession(onnxModelPath);

        var metadata = OnnxExportMetadata.Load(onnxExportMetadataPath);
        var calibration = metadata.CalibrationReplication
            ?? throw new InvalidOperationException("draft-baseline model is missing calibration_replication metadata.");
        _calibrator = new IsotonicCalibrator(calibration.XThresholds, calibration.YThresholds);
    }

    public DraftScoreOutput Predict(DraftScoreInput input)
    {
        // Exact order per onnx_export_metadata.json's input_schema.feature_order.
        var features = new float[]
        {
            (float)input.HeroWinrateDifference,
            (float)input.MatchupAdvantage,
        };

        var tensor = new DenseTensor<float>(features, [1, features.Length]);
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", tensor) };

        using var results = _session.Run(inputs);
        var probabilities = results.First(r => r.Name == "probabilities").AsEnumerable<float>().ToArray();

        var rawRadiantWinProbability = (double)probabilities[1];
        var calibrated = _calibrator.Predict(rawRadiantWinProbability);

        return new DraftScoreOutput
        {
            RawRadiantWinProbability = rawRadiantWinProbability,
            CalibratedRadiantWinProbability = calibrated,
        };
    }

    public void Dispose() => _session.Dispose();
}
