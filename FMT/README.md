# FeiMaoTecPlanner V1.1.1 customization

> 使用者圖文操作手冊請見儲存庫根目錄的 [README-FMT.md](../README-FMT.md)。本文件保留開發、建置與客製技術資訊。

FeiMaoTecPlanner V1.1.1 is the FMT branded startup/login, terrain-risk, Taiwan airspace-result, Traditional Chinese UI and stability release of the FMT 飛貓科技 customization layer for Mission Planner.

## Defaults

- Product: `FeiMaoTecPlanner V1.1.1`
- Company: `FMT飛貓科技`
- Login branding: embedded `FMT/Assets/fmt-logo.png`
- Application icon: generated from `FMT/Assets/fmt-app-icon-source.png` with `FMT/Build-FmtIcon.ps1`
- Language: English (`en-US`)
- Chinese language resources use Traditional Chinese; the Simplified Chinese option is not shown in the language selector.
- Theme: `FMT-SkyBlue.mpsystheme`
- Startup account: `FMT`
- Startup password: `1234`
- Parameter password: `1234`
- Universal parameter password: `9103`

Startup and parameter passwords are stored as PBKDF2 hashes in the Mission Planner settings after initialization. The universal parameter password is intentionally embedded as required and only unlocks the protected full-parameter page; it does not bypass startup login.

## Added behavior

- Flight Data uses the [TitanPlanner](https://github.com/Titan-Dynamics/TitanPlanner) attitude-indicator layout, including the heading tape, speed and altitude scales, flight-status indicators, and home-direction cue.
- The Messages tab uses TitanPlanner-style severity rows: red for critical/error, yellow for warnings, blue for notices, green for recognized success messages, and neutral styling for general information.
- The Full Parameter List prompts for a password and provides a password-change dialog.
- The Flight Plan map labels the distance between consecutive numbered waypoints.
- The AUTO panel refreshes from Mission protocol count/item/acknowledgement traffic and calculates progress from the ordered Mission Item list index.
- Flight Plan and Flight Data load nearby Taiwan CAA airspace dynamically:
  - prohibited layer: red
  - restricted layer: yellow
- Taiwan CAA data is cached locally for 12 hours to reduce network load.

CAA GIS information is a planning reference only. The official announcement and all applicable laws remain authoritative. Official sources:

- https://drone.caa.gov.tw/zh-TW/Default/OpenData
- https://dronegis.caa.gov.tw/portal/apps/webappviewer/index.html?id=807bd21438ba4208b4a7e28569fe41aa

## Build

Open `MissionPlanner.sln` in Visual Studio 2022 and build `Debug` or `Release`.

After a Release build, run `FMT/Build-FMTPlannerPackage.ps1` to produce the versioned portable ZIP, for example `bin/Package/FMTPlanner-V1.1.1.zip`. The version comes from `FMT/VERSION` unless `-ReleaseVersion X.X.X` is supplied. Extract the ZIP and run `FMTPlanner.exe`. The Windows stable package excludes PDB symbols, macOS/Linux native libraries and developer `plugins/example*.cs` samples while retaining runtime plugins, drivers, maps, metadata and FMT resources. The package deliberately avoids self-extracting launchers because McAfee Real Protect classifies their extract-and-launch behavior as suspicious until the publisher has trusted code-signing reputation.

A locally generated self-signed certificate can verify whether a file changed, but it does not establish public publisher trust on other computers. Public releases should remain ZIP packages until FMT has a trusted code-signing certificate; future signed builds should use an RFC 3161 timestamp server.

GitHub Actions builds the solution on push, pull request, or manual dispatch and publishes `FMTPlanner-Windows-Portable-ZIP` plus the debug artifact. Tags matching `FMTPlanner-v*` automatically create a GitHub Release containing the matching versioned ZIP.
