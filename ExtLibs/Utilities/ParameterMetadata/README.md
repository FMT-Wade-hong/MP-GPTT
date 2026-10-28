# ArduPilot release parameter snapshots

These compressed XML resources are derived from the official immutable release commits listed in `manifest.json`, using each release's own `Tools/autotest/param_metadata/param_parse.py`. They retain the upstream project's licensing. `sha256` identifies the uncompressed bundled XML.

`FMT/Emit-ParameterMetadata.py` wraps the original XML emitter to add only a `source` attribute containing the parser's `real_path` (which may be relative to a library). No documentation fields are changed. This allows the loader to choose the helicopter or multirotor attitude-controller/autotune definition when Copter contains both. Other hardware-dependent duplicates retain upstream ordering; all alternatives are recorded by the audit, not discarded from the resources.

To reproduce, capture official tags with `git ls-remote --tags https://github.com/ArduPilot/ardupilot.git 'refs/tags/*-4.6.*' 'refs/tags/*-4.7.*'`, then run `FMT/Build-VersionedParameterMetadata.py --tags <saved-output>` using Python with lxml. Run `FMT/Audit-ArduPilotParameters.py` to validate hashes and export the complete release differences, including alternative definitions. No vehicle data is uploaded by these scripts.

The 31 snapshots cover the actual stable tags available during the 2026-10-27 audit: Copter, Plane, Rover and Tracker 4.6.0 through 4.6.3 and 4.7.0 through 4.7.2; Sub 4.7.0 through 4.7.2. No Sub 4.6, Blimp, or AP_Periph release is invented. Documentation includes optional features and scripts; actual board/build parameter availability comes from the connected vehicle. This is metadata verification, not hardware/flight validation.
