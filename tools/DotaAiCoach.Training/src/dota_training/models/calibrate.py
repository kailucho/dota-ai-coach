"""Calibration fix: isotonic regression fit on the CALIB split (never
val -- val is used only to check calibration, per validation-and-calibration.md's
explicit "use a third split" rule)."""

from __future__ import annotations

from sklearn.isotonic import IsotonicRegression


def fit_calibrator(raw_proba_calib, y_calib) -> IsotonicRegression:
    calibrator = IsotonicRegression(out_of_bounds="clip")
    calibrator.fit(raw_proba_calib, y_calib)
    return calibrator


def apply_calibrator(calibrator: IsotonicRegression, raw_proba):
    return calibrator.predict(raw_proba)
