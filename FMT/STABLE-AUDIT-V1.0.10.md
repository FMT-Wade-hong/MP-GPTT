# FMTPlanner V1.0.10 stable audit

This audit is scoped to cleanup, lifecycle correctness and measurable performance. It does not redesign the flight workflow or remove V1.0.9 operator features.

## Baseline and test boundary

- Baseline: tag `FMTPlanner-v1.0.9`, commit `aa381f185`.
- Stable branch: `codex/stable-v1.0.10`.
- Windows 11, AMD Ryzen 9 8940HX, 32 logical processors, 31.2 GiB usable RAM.
- Automated login and disconnected-main-window measurements are recorded in `PERFORMANCE-V1.0.10.md`.
- No flight controller was attached during the automated audit. USB/UDP/TCP, ArduCopter, ArduPlane and QuadPlane connected-state results must be recorded as real-hardware tests before claiming them as passed.

## Startup component classification

| Class | Components | Stable decision |
|---|---|---|
| A — mandatory | Login/authentication, MainV2 shell, Flight Data, MAVLink serial reader, settings, core maps, HUD and FMT branding | Retained at startup. |
| B — on demand | Configuration pages, mission planner heavy controls, camera/video, log analysis, parameter pages, scripting tools | Retained; pages remain lazily created where the existing switcher supports it. No risky architectural rewrite in V1.0.10. |
| C — background | Airport/map metadata, no-fly/airspace data, log metadata, firmware metadata, K-index and update check | Retained on background workers. Network and disk work remains isolated from the UI thread. Candidate for cancellation-token modernization in a later release. |
| D — optional/external | DLL plugins and top-level C# plugins | Framework retained. Missing/empty plugin directories no longer create a compilation task or permanent runner thread. |

## Plugin audit

Findings in `Plugin/PluginLoader.cs` and `MainV2.cs`:

- The assembly resolver was registered once per candidate DLL. It is now registered once per process.
- Assembly file indexing used an unsynchronized mutable dictionary. Index creation is now locked and failed scans produce a cached empty result instead of repeated exceptions.
- Duplicate `LoadAll` calls could repeat plugin initialization. Loading is now one-shot.
- An empty C# plugin list still scheduled a background task. The task is now created only when a `.cs` plugin exists.
- The main window always started a plugin runner, even when no runnable plugin existed. The runner is now conditional.
- DLL plugin contracts, `Init`, `Loaded`, `Loop`, `Exit`, disabled-plugin settings and runtime script compilation remain compatible.

## UI, timer and redraw audit

Static inventory excluding the vendored Mono source tree:

- WinForms timers: 79
- threading timers: 10
- `Task.Delay`: 38
- `Invalidate`: 401
- `Refresh`: 75
- `BeginInvoke`: 128
- `Application.Idle`: 15

Active high-frequency review:

- Flight Data main loop sleeps 50 ms and gates binding work to about 10 Hz; map updates are separately throttled. These rates are tied to live telemetry and were not globally reduced without connected-flight evidence.
- The FMT common-mode bar was restyled and reassigned at telemetry refresh rate even when firmware, connection and active mode were unchanged. It now returns early for an identical state.
- Flight Planner already suppresses expensive updates during waypoint drag and uses a 1200 ms timer; no further speculative change was made.
- Message and graph timers are already enabled only while their respective view/function is active.

## Event and object lifetime audit

Low-risk fixes applied:

- Changing the active MAVLink interface now detaches `MavChanged` from the previous interface before attaching the new one.
- MainV2 detaches static layout, warning-engine and Windows power events during close.
- Flight Data detaches parameter, POI, no-fly and camera events during close.
- Camera refresh no longer adds the same image callback repeatedly after every parameter-list change.

Large-scale conversion of all anonymous WinForms event handlers was deferred because the main form and Flight Data are process-lifetime singletons and a broad rewrite would add regression risk without a measured benefit.

## Background work and exception policy

- Existing background startup workers remain isolated from the UI thread.
- Empty catch blocks in flight and third-party compatibility paths were inventoried but not globally rewritten. Changing error semantics across MAVLink, maps and native libraries is outside a stable cleanup release.
- New plugin indexing failures are logged with context.
- The FMT release updater now uses explicit UTF-8 and a typed WinForms dialog; it no longer compares an ambiguous integer return value with `DialogResult`.

## Release package audit

V1.0.9 Release output contained 1,603 files and 432,528,436 bytes before documentation packaging. The public ZIP also included development/platform artifacts:

- `.pdb`: 108 files, 12,918,296 bytes — debug symbols, not required at runtime.
- `.dylib`: 2 files, 30,169,280 bytes — macOS native libraries.
- `.so`: 6 files, 42,578,152 bytes — Linux native libraries.
- `plugins/example*.cs`: developer sample plugins that are compiled at startup when present.

V1.0.10 Windows package policy excludes those files from the public ZIP only. Source files and complete build output remain available for development. Operational DLL plugins, `AnonymizeBinlogPlugin.cs`, `generator.cs`, drivers, map data, language resources and Python tools remain bundled.

## Dependency review

The existing solution reports known package advisories during restore/build, including legacy versions of log4net, SSH.NET, SharpCompress and SkiaSharp, plus end-of-life target-framework warnings. They are recorded as upgrade work rather than silently removed because they are shared by serial, map, video, log and plugin projects. A dependency migration needs a dedicated compatibility release and connected hardware regression tests.

## Deferred items

- Convert background worker ownership to cancellation tokens.
- Evaluate lazy startup of the HTTP KML/MJPEG listener with API compatibility tests.
- Upgrade vulnerable/EOL dependencies in isolated batches.
- Validate long-duration connected memory behavior with ArduCopter, ArduPlane and QuadPlane hardware.
- Measure real map-pan FPS and telemetry delay with representative Taiwan airspace data.
