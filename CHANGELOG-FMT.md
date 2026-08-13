# FMTPlanner release history

## FMTPlanner v1.0.3 — Unreleased

### Airspace safety checks

- Extended the Taiwan CAA airspace check to include the takeoff path from Home to the first waypoint and the return path from the final waypoint back to Home.
- Airspace warnings now identify whether a crossing occurs on the takeoff path, a mission segment, or the return path.
- The check requires a valid Home location so it cannot incorrectly report a clear route while the takeoff and return paths are unknown.

### Version display

- Changed the application and login window titles to `FeiMaoTecPlanner V1.0.3`.
- Removed the upstream Mission Planner `1.3.83 build ...` text from the title bar.
- Aligned the Windows assembly and file version metadata with `1.0.3.0`.

### FMT theme lock

- Restricted the theme selector to the single `FMT-SkyBlue.mpsystheme` branded theme.
- Removed the custom theme editor entry and automatically restores the FMT theme at startup.

### Flight quick actions

- Added a prominent red Arm/Disarm button to the left side of the main toolbar. It follows the live armed state and delegates to the existing Flight Data Arm/Disarm safety flow.
- Added an Airspeed Zero button beside it. The action is available only while connected and disarmed, requires a pitot-cover confirmation, and sends an airspeed-only `MAV_CMD_PREFLIGHT_CALIBRATION` request (`param6=2`).

### GPS toolbar status

- Added a live two-line GPS display beside the FMT logo with satellite count, fix type, HDOP, and VDOP. Missing telemetry is shown as `--`, and the fix state is color coded.
- Removed the duplicate satellite-count and HDOP values from the lower map overlay.
- Localized the GPS fix labels, map heading legend, and Altitude Angel sign-in action for the Traditional Chinese interface.

## FMTPlanner v1.0.2 — 2026-08-13

### Stability and map interaction

- Fixed a `KeyNotFoundException` when executing a built-in action from the Flight Data Actions tab. Built-in actions now bypass the custom-action dictionary and continue through the normal MAVLink command path.
- Restored the clickable **Ready to Arm / Not Ready to Arm** HUD status and its pre-arm reason dialog.
- Fixed waypoint dragging so the marker and route line follow the mouse continuously before the grid value is committed.
- Debounced Taiwan CAA and airport refresh work during map dragging to reduce repeated loading and log noise.

### Mission checks

- Added **Altitude Check** above Upload. It warns for less than 30 m terrain clearance or more than Taiwan's 120 m general AGL ceiling, then displays the mission altitude and terrain profile.
- Added **Airspace Check** above Upload. It detects mission segments that enter or cross Taiwan CAA prohibited (red) or restricted (yellow) polygons and lists the affected WP segments.
- Both checks are advisory and do not silently block mission upload.

### FMT interface

- Added the FMT logo to the left of the ArduPilot logo; clicking it opens `https://www.feimaotec.com`.
- Removed the Simulator and Help/About screens from the main interface.
- Removed the Optional Hardware page tree from Initial Setup while retaining shared drivers required by other flight functions.
- Renamed the application binary to `FMTPlanner.exe` and added versioned portable filenames.
- Changed the public package to `FMTPlanner-V1.0.2.zip`. The self-extracting EXE was withheld after McAfee Real Protect quarantined its extract-and-launch behavior; the ZIP runs the application directly after extraction.

### Validation

- Full `MissionPlanner.sln` Release build completed successfully.
- Added automated verification for compiled button handlers, Ready-to-Arm hit testing, waypoint drag updates, airspace geometry, removed screens, embedded logos, GUI subsystem, and package naming.
- V1.0.2 verification suite: 31 checks passed, 0 failed.
- With McAfee protection enabled, the extracted `FMTPlanner.exe` opened its main window and produced 0 new McAfee detections during the startup smoke test.

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
