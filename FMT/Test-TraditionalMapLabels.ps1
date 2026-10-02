param([string]$Directory = 'bin/TraditionalMap119/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.GCSViews.FlightPlanner',$true)
$method = $type.GetMethod('GetFmtMapTileStatus',[Reflection.BindingFlags]'Static,NonPublic')
$originalCulture = [Threading.Thread]::CurrentThread.CurrentUICulture
try {
    foreach ($culture in @('zh-TW','zh-Hant')) {
        [Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]$culture
        if ($method.Invoke($null,@($true)) -cne '狀態：載入地圖中…') { throw 'Loading label incorrect' }
        if ($method.Invoke($null,@($false)) -cne '狀態：地圖已載入') { throw 'Loaded label incorrect' }
    }
    [Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]'en-US'
    if ($method.Invoke($null,@($false)) -cne 'Status: loaded tiles') { throw 'English label changed' }
    $source = Get-Content "$PSScriptRoot/../GCSViews/FlightPlanner.cs" -Raw -Encoding UTF8
    foreach ($caption in @('chk_grid.Text = "網格"','lnk_kml.Text = "檢視 KML"','BUT_InjectCustomMap.Text = "匯入自訂地圖"')) {
        if (!$source.Contains($caption)) { throw "Missing caption: $caption" }
    }
    'PASS: Traditional Chinese map captions and both dynamic tile states; English preserved'
} finally { [Threading.Thread]::CurrentThread.CurrentUICulture = $originalCulture }
