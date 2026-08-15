# FMTPlanner release history

## FMTPlanner v1.0.10 — 2026-08-15

### Performance

- Plugin assembly resolution is registered once per process and assembly directory indexes are cached safely.
- Missing or empty plugin directories no longer create a C# compilation task or permanent plugin runner thread.
- The common flight-mode bar no longer reapplies labels, colors and button state when vehicle state has not changed.
- Child forms no longer receive a complete repeated Theme pass on every non-client activation.

### Stability

- Fixed active MAVLink interface replacement leaving callbacks attached to the previous interface.
- MainV2 now detaches static layout, warning-engine and Windows power events during shutdown.
- Flight Data detaches parameter, POI, no-fly and camera events, and prevents duplicate camera callbacks after parameter refresh.
- Replaced the release-update message box with a typed, DPI-aware, scrollable Traditional Chinese dialog using explicit UTF-8; fixed the `RuntimeBinderException` caused by comparing an integer return value with `DialogResult`.

### Cleanup

- The public Windows ZIP excludes PDB debug symbols, macOS/Linux native libraries and developer `plugins/example*.cs` samples without deleting their source or build outputs.
- Added a package manifest, repeatable performance measurement script, V1.0.9 baseline and stable-audit report.
- Retained DLL plugins, drivers, maps, language fallback resources, parameter/firmware metadata, Python tools and all V1.0.9 FMT features.

### Validation boundary

- Automated Release build, startup/login smoke, disconnected HUD/map resource measurement, static regression checks and package-content checks are documented with results.
- USB Serial, UDP, TCP and ArduCopter/ArduPlane/QuadPlane connected-state validation still requires representative hardware or an approved repeatable endpoint and is not claimed by this release audit.

## FMTPlanner v1.0.9 — 2026-08-14

- 修正飛行資料主畫面未建立飛行軌跡時不顯示飛機位置，未解鎖狀態仍會顯示即時飛行器圖示。
- 修正「自動平移地圖」不追蹤飛機位置，改為直接依目前飛機座標更新地圖中心。
- 修正完整參數頁首次載入的空值例外、頂部版面遮蔽及固定翼參數名稱消失。
- 修正 Servo Output 最下方 CH16／CH32 被裁切。
- 將 GCS 地面站失控保護選項繁體中文化並改善配置，避免文字重疊。
- 停止 Altitude Angel 啟動時自動登入，移除主畫面與任務規劃地圖的登入提示列。

## FMTPlanner v1.0.8 — 2026-08-14

- 在主工具列新增獨立「參數設定」頁面，完整參數列表及密碼設定集中管理；移除完整參數表內的巢狀密碼提示，解決參數無法編輯問題。
- 固定翼基本調校頁全面繁體中文化，新增 Servo Roll／Pitch／Yaw、L1、TECS、導航角度、油門及空速控制的中文用途與調整方向說明。
- HUD 航點距離移至右側高度計與 GPS 定位狀態之間，以天空藍高對比資訊框顯示；AUTO、RTL、Guided 與 Loiter 顯示對應距離名稱。
- HUD 油門名稱置於百分比上方，改善狹窄高度帶的閱讀性。
- 強制解鎖／上鎖警告視窗的標題、風險說明及按鈕繁體中文化。
- 強化固定翼與 VTOL／QuadPlane 地圖圖示的判斷、建立與重建流程，並縮小啟用中的天空藍背光外框。
- 移除 Altitude Angel 地圖登入提示，避免遮蔽地圖及飛行資訊。

## FMTPlanner v1.0.7 — 2026-08-14

- 修正參數頁首次解鎖後仍無法修改數值，Enter 可直接解鎖並進入編輯。
- 限禁航區依飛機位置僅顯示 50 公里範圍，新增「顯示限禁航區」開關並降低地圖負載。
- HUD 彩色柱下新增即時油門百分比。
- VTOL／QuadPlane 依 Q_ENABLE 正確顯示 VTOL 地圖圖示，縮小啟用外框。
- 未解鎖飛機不再繪製飛行軌跡，上鎖後清除上一段軌跡。
- 高度與限禁航區檢查改為可調大小、可捲動且靠左的結果視窗。
- 高度地形曲線標題、警語、圖例及座標軸繁體中文化。
- 任務高度模式繁體中文化，補回航點半徑、盤旋半徑及預設高度初始值。

## FMTPlanner v1.0.6 — 2026-08-14

- Repositioned the flight-mode bar and fixed ALT HOLD plus VTOL/fixed-wing group indicators.
- Added session and controller total flight time to the top toolbar.
- Moved Motor Test into Mandatory Hardware.
- Added Enter-to-unlock and reliable first-entry parameter editing.
- Reduced waypoint drag redraw frequency and renamed Flight Planner to 任務規劃.
- Localized and centered mission types, and restored radius/default-altitude value visibility.

## FMTPlanner v1.0.5 — 2026-08-14

### Flight interface

- Added a vehicle-aware common flight-mode panel between the HUD and telemetry information. Multirotor, Fixed Wing, VTOL/QuadPlane, Rover, and Sub use their own safe mode sets.
- Added a TitanPlanner-inspired visual refresh for Radio Calibration, Motor Setup, and Servo Output while retaining the original MAVLink parameter and calibration paths.
- Added dedicated fixed-wing, multirotor, and VTOL map markers based on the supplied FMT aircraft images. The active vehicle uses a cyan glow for visibility over satellite maps.

### Traditional Chinese telemetry

- Changed all three telemetry field-selection windows to the Traditional Chinese title `選擇顯示項目`.
- Localized visible CurrentState field names, QuickView dashboard labels, units, and Flight Data tab names. Internal MAVLink and CurrentState keys remain unchanged for compatibility.
- Added readable terminology for acceleration, pressure, battery cells, ESC telemetry, GPS accuracy, range finders, channels, flight time, airspeed, wind, heading, and distance-to-home values.

### Branding and compatibility

- Added a stable Windows AppUserModelID so the running taskbar icon groups under FMTPlanner.
- Added ArduSub flight-mode lookup support and kept all quick mode changes behind connected/armed-state safety checks.

## FMTPlanner v1.0.4 — 2026-08-13

### Parameter interface

- Fixed the protected Full Parameter List being created after theme initialization, which could leave the parameter tree and table white with unreadable text.
- The parameter tree, rows, alternating rows, headers, selection colors, and grid lines now explicitly use the FMT theme whenever the protected page is opened.
- The cached parameter control is hidden while the password prompt and parameter data are loading, preventing the previous white page from flashing behind the dialog.

### Flight quick actions

- Added a QNH sea-level pressure button immediately after Airspeed Zero in the top toolbar.
- QNH entry uses pascals with hPa guidance, validates a safe atmospheric range, requires confirmation, and is available only while connected and disarmed.
- Unsupported or read-only flight-controller pressure parameters now produce a clear error instead of failing silently.

### GPS toolbar status

- Added a white satellite icon to the left of the two-line satellite, fix, HDOP, and VDOP display.

### Windows application icon

- Fixed the running taskbar and window icon so the splash screen, login, main window, and FMT parameter dialogs consistently use the embedded FMT icon.
- Disabled the legacy external `icon.png` override so a file beside the executable cannot replace FMT branding at runtime.

### Flight mode common settings

- Added three Traditional Chinese common-settings frames below the flight-mode controls: Multirotor, Fixed Wing, and VTOL/QuadPlane.
- Multirotor includes navigation speed, GPS position-control speed, waypoint radius, waypoint/RTL yaw, and RTL speed. Fixed Wing includes cruise airspeed, minimum GPS ground speed, and waypoint radius. VTOL includes fixed-wing cruise speed, VTOL waypoint/return speed, VTOL GPS speed, VTOL waypoint radius, and QRTL behavior.
- Added an in-frame Traditional Chinese explanation for every vehicle type so the operator can see which flight phase each value controls.
- The connected firmware and `Q_` parameters determine which vehicle frame is enabled; other frames remain visible but cannot write parameters.
- All displayed speeds use `m/s`; legacy `cm/s` parameters are converted automatically, while renamed parameters such as `WP_SPD`, `AIRSPEED_CRUISE`, `Q_WP_SPD`, and `Q_LOIT_SPEED_MS` are written directly.
- Every frame permits writes only while connected, disarmed, and not read-only.

## FMTPlanner v1.0.3 — 2026-08-13

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
