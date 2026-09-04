$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$accelerometerPath = Join-Path $repositoryRoot 'GCSViews\ConfigurationView\ConfigAccelerometerCalibration.cs'
$compassPath = Join-Path $repositoryRoot 'GCSViews\ConfigurationView\ConfigHWCompass2.cs'

$accelerometer = Get-Content -LiteralPath $accelerometerPath -Raw
$compass = Get-Content -LiteralPath $compassPath -Raw

function Assert-Contains {
    param(
        [string]$Content,
        [string]$Expected,
        [string]$Description
    )

    if (-not $Content.Contains($Expected)) {
        throw "驗證失敗：$Description"
    }
}

Assert-Contains $accelerometer 'Name = "FmtAccelPoseGrid"' '加速度計頁缺少六面姿態圖卡。'
Assert-Contains $accelerometer '"水平", "左側", "右側", "機頭向下", "機頭向上", "倒置"' '加速度計頁未涵蓋六個校正方向。'
Assert-Contains $accelerometer 'Color.FromArgb(35, 170, 78)' '加速度計頁缺少已完成的綠色狀態。'
Assert-Contains $accelerometer 'Color.FromArgb(245, 196, 24)' '加速度計頁缺少目前姿態的黃色狀態。'
Assert-Contains $accelerometer 'Color.FromArgb(105, 112, 118)' '加速度計頁缺少尚未校正的灰色狀態。'
Assert-Contains $accelerometer 'SetAccelCurrentPose(poseText)' '加速度計圖卡未連接飛控姿態提示。'
Assert-Contains $accelerometer 'CompleteAccelCalibration();' '加速度計圖卡未連接完成狀態。'
Assert-Contains $accelerometer 'Dock = DockStyle.None' '加速度計頁仍會填滿整個視窗。'
Assert-Contains $accelerometer 'Anchor = AnchorStyles.Top | AnchorStyles.Left' '加速度計頁未固定於左上角。'
Assert-Contains $accelerometer 'lbl_Accel_user.Dock = DockStyle.None' '加速度計訊息列會覆蓋姿態圖卡。'

Assert-Contains $compass 'Size = new Size(760, 610)' '羅盤頁沒有使用緊湊固定尺寸。'
Assert-Contains $compass 'Dock = DockStyle.None' '羅盤頁仍會填滿整個視窗。'
Assert-Contains $compass 'Anchor = AnchorStyles.Top | AnchorStyles.Left' '羅盤頁未固定於左上角。'
Assert-Contains $compass 'label1.Size = new Size(660, 24)' '羅盤說明文字沒有受控寬度。'

Write-Host 'FMT calibration layout verification passed.'
