using DotaAiCoach.Domain.MlInference;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DotaAiCoach.MlInference;

/// <summary>
/// ONNX Runtime-backed implementation of IWinProbabilityPredictor, for
/// the LIVE_ALLOWED win-probability model (docs/adr/0022). Feature order
/// below is copied verbatim from
/// Models/win-probability-live/onnx_export_metadata.json's
/// input_schema.feature_order — do not reorder without re-checking that
/// file, this is the single most important correctness point in this
/// class.
/// </summary>
public sealed class WinProbabilityPredictor : IWinProbabilityPredictor, IDisposable
{
    private const string ModelFolder = "win-probability-live";
    private readonly InferenceSession _session;
    private readonly IsotonicCalibrator _calibrator;

    public WinProbabilityPredictor()
        : this(
            ModelPathResolver.ResolvePath(ModelFolder, "model.onnx"),
            ModelPathResolver.ResolvePath(ModelFolder, "onnx_export_metadata.json"))
    {
    }

    internal WinProbabilityPredictor(string onnxModelPath, string onnxExportMetadataPath)
    {
        _session = new InferenceSession(onnxModelPath);

        var metadata = OnnxExportMetadata.Load(onnxExportMetadataPath);
        var calibration = metadata.CalibrationReplication
            ?? throw new InvalidOperationException("win-probability-live model is missing calibration_replication metadata.");
        _calibrator = new IsotonicCalibrator(calibration.XThresholds, calibration.YThresholds);
    }

    public WinProbabilityOutput Predict(WinProbabilityInput input)
    {
        // Exact order per onnx_export_metadata.json's input_schema.feature_order.
        var features = new float[]
        {
            (float)input.GoldAdvantageAtMinute,
            (float)input.XpAdvantageAtMinute,
            (float)input.GoldAdvantageSlope5,
            (float)input.XpAdvantageSlope5,
            (float)input.KillsDifferenceAtMinute,
            (float)input.DeathsInLast5MinDifference,
            (float)input.TowersKilledDifference,
            (float)input.BarracksKilledDifference,
            (float)input.RoshanKilledDifference,
            (float)input.HeroPickWinrateDifference,
            (float)input.AverageRankTier,
            (float)input.MinuteOfMatch,
            EncodeFirstBloodRadiant(input.FirstBloodRadiant),
        };

        var tensor = new DenseTensor<float>(features, [1, features.Length]);
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", tensor) };

        using var results = _session.Run(inputs);
        var probabilities = results.First(r => r.Name == "probabilities").AsEnumerable<float>().ToArray();

        // output_schema: column 1 (index 1) is P(radiant_win=1).
        var rawRadiantWinProbability = (double)probabilities[1];
        var calibrated = _calibrator.Predict(rawRadiantWinProbability);

        return new WinProbabilityOutput
        {
            RawRadiantWinProbability = rawRadiantWinProbability,
            CalibratedRadiantWinProbability = calibrated,
        };
    }

    // Matches train.py/train_live.py's _prepare_xy encoding: True=1.0,
    // False=0.0, missing=0.5.
    private static float EncodeFirstBloodRadiant(bool? value) =>
        value switch
        {
            true => 1.0f,
            false => 0.0f,
            null => 0.5f,
        };

    public void Dispose() => _session.Dispose();
}
