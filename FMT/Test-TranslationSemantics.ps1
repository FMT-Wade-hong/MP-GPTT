param([string]$Directory = 'bin/Translation119Corrections/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$translate = $assembly.GetType('MissionPlanner.FMT.FmtParameterDrafts', $true).GetMethod('Translate', $flags)
function Assert-Meaning([string]$Source, [string[]]$Required) {
    $result = [string]$translate.Invoke($null, @($Source))
    foreach ($term in $Required) {
        if (!$result.Contains($term)) { throw "Meaning regression: $Source missing $term" }
    }
}
Assert-Meaning 'Optical flow options. Bit 0 should be set if the sensor is stabilised (e.g. mounted on a gimbal)' @('Roll','Pitch','bit 0 設為 1','穩定式')
Assert-Meaning 'This is used in rover vehicles, where the sensor is a fixed height above the ground' @('Rover','固定','離地高度','覆寫')
Assert-Meaning '0:Disable mode change following fence action until fence breach is cleared' @('bit 0 設為 1 時','禁止切換模式','直到')
Assert-Meaning 'Time in seconds that gripper close the gripper after opening; 0 to disable' @('單位秒','0 僅停用自動關閉')
Assert-Meaning 'Time in seconds that gripper will regrab the cargo to ensure grip has not weakened; 0 to disable' @('時間間隔','0 僅停用定時重新夾緊')
Assert-Meaning 'Time in seconds that EPM gripper will regrab the cargo to ensure grip has not weakened; 0 to disable' @('EPM','時間間隔','0 僅停用定時重新夾緊')
foreach ($index in 1..4) {
    Assert-Meaning "Proximity sensor ignore angle $index" @('中心方向角')
    Assert-Meaning "Proximity sensor ignore width $index" @('總角度寬度','兩側各占一半')
}
# Safety-critical distinctions already reviewed must not be collapsed into generic disable text.
Assert-Meaning 'Maximum acceleration with which obstacles will be avoided with. Set zero to disable acceleration limits' @('0','停用加速度限制')
Assert-Meaning 'Maximum speed that will be used to back away from obstacles in GPS modes (m/s). Set zero to disable' @('GPS','m/s','停用此退避功能')
foreach ($axis in @(@('X','forward','前方'), @('Y','to the right','右方'), @('Z','down','下方'))) {
    $source = "$($axis[0]) position of the optical flow sensor focal point in body frame. Positive $($axis[0]) is $($axis[1]) of the origin."
    if ($axis[0] -eq 'Z') { $source = 'Z position of the optical flow sensor focal point in body frame. Positive Z is down from the origin.' }
    Assert-Meaning $source @("正 $($axis[0])", $axis[2])
}
Assert-Meaning 'Defines the horizontal jerk in m/s/s used during missions' @('m/s/s/s','不可把 jerk 當作加速度')
Assert-Meaning 'Sets the maximum XY position change, in m/s' @('變化速率','m/s','不是位置距離')
Assert-Meaning 'FF D Gain which produces an output that is proportional to the rate of change of the error' @('目標','微分','不要與')
Assert-Meaning 'Enables double speed on high offset (finned blimp only).' @('擺動頻率','offset','不是 GPS')
Assert-Meaning 'Check that MAV_SYSID (or SYDID_THISMAV) has been set. 3 or less to prevent arming. -1 to disable.' @('MAV_SEVERITY','0～3','-1','並非')
Assert-Meaning 'Seconds since January 1st 2016 (Unix epoch+1451606400) since statistics reset (set to 0 to reset statistics)' @('時間戳','減 1451606400','不是重設後經過')
Assert-Meaning 'Third TOFSENSE-M sensors backend Instance. Setting this to 3 will pick the second backend from PRX_ or RNG_ Parameters (Depending on TOFSENSE_PRX)' @('第三個','second 是筆誤')
'PASS: 26 semantic, unit, direction and safety distinctions retained'
