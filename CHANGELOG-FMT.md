# FMTPlanner release history

## FMTPlanner v1.0.1 — 2026-08-12

### Stability

- Prevented a production crash when MAVLink serial-port ownership is requested twice. The condition is still logged, while the diagnostic breakpoint now runs only when a debugger is attached.

## FMTPlanner v1.0.0 — 2026-08-12

First packaged release of the FMT 飛貓科技 Mission Planner customization.

### Branding and startup

- Product renamed to **FeiMaoTecPlanner V1** by **FMT飛貓科技**.
- Added FMT application icons, splash branding, and a centered FMT logo on the login screen.
- Added startup authentication with the default account `FMT` and password `1234`.
- Changed the Windows output type so no command-prompt window appears during startup.
- Added the sky-blue FMT theme while keeping English as the default language.

### Flight interface

- Added the TitanPlanner-style attitude indicator layout and flight-status presentation.
- Added severity-colored message rows for critical, warning, notice, success, and general messages.
- Added distance labels between consecutive waypoints on the map.
- Added Taiwan CAA airspace overlays: prohibited areas in red and restricted areas in yellow.

### Parameters and language

- Added password protection and password settings for the full-parameter interface.
- Default parameter password: `1234`; required universal password: `9103`.
- Converted Simplified Chinese resources to Traditional Chinese with Taiwan terminology.
- Removed the Simplified Chinese choice and retained Traditional Chinese (`zh-TW`).

### Reliability fixes

- Fixed the Flight Data page failing to render when the optional `accel_air` field is unavailable.
- Fixed the Altitude Angel map-click cast exception when Taiwan CAA overlays are present.
- Added a repeatable single-file portable packaging script that produces `FMTPlanner.exe`.

### Validation

- Full Visual Studio/MSBuild Release build.
- Traditional Chinese XML and format-placeholder validation.
- Portable launcher payload, extraction, and Windows GUI subsystem checks.
