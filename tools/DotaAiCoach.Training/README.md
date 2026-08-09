# DotaAiCoach.Training

Offline ML training pipeline for the Win Probability baseline model. Not
part of the shipped .NET application — deliberately separate toolchain
(Python), never added to `DotaAiCoach.sln`. See `docs/adr/0017` for the
full rationale.

## Setup

```powershell
cd tools/DotaAiCoach.Training
python -m venv .venv
./.venv/Scripts/pip install -r requirements.txt
```

## Running the pipeline stages, in order

```powershell
./.venv/Scripts/python -m dota_training.opendota.acquire   # RAW: fetch matches from OpenDota
./.venv/Scripts/python -m dota_training.stages.bronze       # RAW -> BRONZE
./.venv/Scripts/python -m dota_training.stages.normalized   # BRONZE -> NORMALIZED
./.venv/Scripts/python -m dota_training.stages.features      # NORMALIZED -> FEATURES
./.venv/Scripts/python -m dota_training.stages.dataset       # FEATURES -> DATASET (4-way temporal split)
./.venv/Scripts/python -m dota_training.models.train         # train + calibrate + evaluate
```

Each stage reads the previous stage's Parquet output from `data/` and
never re-runs an earlier stage — this makes the pipeline resumable and
keeps provenance traceable (per `dota-data`'s RAW→BRONZE→NORMALIZED→
FEATURES→DATASET discipline).

## Tests

```powershell
./.venv/Scripts/python -m pytest tests -v
```

`tests/test_leakage.py` is the most important file here — it enforces
the project's core ML rule (`dota-ml`): *a live model may never train
using information unavailable to the player at the equivalent game
timestamp.* This baseline is research-only (see `features_spec.yaml`'s
`POSTGAME_ONLY`-tagged features) and is explicitly not wired into the
live app — see `docs/adr/0017`'s "out of scope" section.

## Data and model artifacts

`data/raw/` holds one JSON envelope per OpenDota response, in the same
format the C# `RawResponseCache` uses
(`{endpoint,fetchedAtUtc,statusCode,sourceName,schemaVersion,body,isParsed}`)
— gitignored (large, regenerable from OpenDota, and per dota-data's own
provenance rule this is fine to lose as long as it's documented, which
`metadata.json` does).

`models/<model-id>/metadata.json` and `evaluation.json` ARE committed —
they're the reproducibility record, not a binary artifact. The `.pkl`
model/calibrator files are gitignored (regenerable from the recorded
data version + code version + hyperparameters + seed).
