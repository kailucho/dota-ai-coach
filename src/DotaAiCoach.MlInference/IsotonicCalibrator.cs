namespace DotaAiCoach.MlInference;

/// <summary>
/// Replicates scikit-learn's IsotonicRegression.predict(out_of_bounds='clip')
/// as piecewise-linear interpolation over fitted breakpoints, per
/// docs/adr/0022's "calibration_replication" reconstruction recipe:
/// clip x to [min(x_thresholds), max(x_thresholds)], then linearly
/// interpolate y_thresholds over x_thresholds at the clipped x
/// (equivalent to numpy.interp). x_thresholds must be sorted ascending,
/// matching the exported metadata.
/// </summary>
public sealed class IsotonicCalibrator
{
    private readonly double[] _xThresholds;
    private readonly double[] _yThresholds;

    public IsotonicCalibrator(double[] xThresholds, double[] yThresholds)
    {
        if (xThresholds.Length == 0 || xThresholds.Length != yThresholds.Length)
        {
            throw new ArgumentException("x/y threshold arrays must be non-empty and equal length.");
        }

        _xThresholds = xThresholds;
        _yThresholds = yThresholds;
    }

    public double Predict(double x)
    {
        var clipped = Math.Clamp(x, _xThresholds[0], _xThresholds[^1]);

        // np.interp semantics: find the bracketing segment and linearly
        // interpolate; exact matches and out-of-range (already clipped)
        // are handled naturally by the walk below.
        if (clipped <= _xThresholds[0])
        {
            return _yThresholds[0];
        }

        for (var i = 1; i < _xThresholds.Length; i++)
        {
            if (clipped <= _xThresholds[i])
            {
                var x0 = _xThresholds[i - 1];
                var x1 = _xThresholds[i];
                var y0 = _yThresholds[i - 1];
                var y1 = _yThresholds[i];

                if (x1 == x0)
                {
                    // Duplicate x (a vertical step in the isotonic fit) —
                    // np.interp returns the later (right-hand) y value.
                    return y1;
                }

                var t = (clipped - x0) / (x1 - x0);
                return y0 + t * (y1 - y0);
            }
        }

        return _yThresholds[^1];
    }
}
