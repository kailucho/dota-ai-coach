using DotaAiCoach.MlInference;
using FluentAssertions;

namespace DotaAiCoach.MlInference.Tests;

public class IsotonicCalibratorTests
{
    // Small, hand-picked monotone breakpoint table (not copied from a
    // real model) so the arithmetic is easy to verify by hand.
    private static IsotonicCalibrator MakeCalibrator() =>
        new(
            xThresholds: [0.0, 0.5, 1.0, 1.0, 2.0],
            yThresholds: [0.1, 0.1, 0.4, 0.6, 0.6]);

    [Fact]
    public void Predict_returns_exact_value_at_a_breakpoint()
    {
        var calibrator = MakeCalibrator();

        calibrator.Predict(0.5).Should().BeApproximately(0.1, 1e-9);
        calibrator.Predict(2.0).Should().BeApproximately(0.6, 1e-9);
    }

    [Fact]
    public void Predict_linearly_interpolates_between_breakpoints()
    {
        var calibrator = MakeCalibrator();

        // Midpoint between (0.0, 0.1) and (0.5, 0.1) is flat.
        calibrator.Predict(0.25).Should().BeApproximately(0.1, 1e-9);
    }

    [Fact]
    public void Predict_handles_duplicate_x_step_by_taking_the_left_hand_y()
    {
        var calibrator = MakeCalibrator();

        // x=1.0 appears twice (a vertical step from y=0.4 to y=0.6).
        // np.interp scans thresholds left-to-right and returns the first
        // (left-hand) y for an exact match at a duplicated x.
        calibrator.Predict(1.0).Should().BeApproximately(0.4, 1e-9);
    }

    [Fact]
    public void Predict_clips_below_the_minimum_threshold()
    {
        var calibrator = MakeCalibrator();

        calibrator.Predict(-5.0).Should().BeApproximately(0.1, 1e-9);
    }

    [Fact]
    public void Predict_clips_above_the_maximum_threshold()
    {
        var calibrator = MakeCalibrator();

        calibrator.Predict(10.0).Should().BeApproximately(0.6, 1e-9);
    }

    [Fact]
    public void Constructor_rejects_mismatched_array_lengths()
    {
        var act = () => new IsotonicCalibrator([0.0, 1.0], [0.0]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_rejects_empty_arrays()
    {
        var act = () => new IsotonicCalibrator([], []);

        act.Should().Throw<ArgumentException>();
    }
}
