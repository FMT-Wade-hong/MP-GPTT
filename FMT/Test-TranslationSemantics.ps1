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
Assert-Meaning 'Reverse sense of elevator in FBWB and CRUISE modes. When set to 0 up elevator (pulling back on the stick) means to lower altitude. When set to 1, up elevator means to raise altitude.' @('0 時拉桿提高目標高度','1 時拉桿降低目標高度','官方已修正說明','4.5.7／4.6.3')
Assert-Meaning 'This filters the load test output. A value of 1 means no filter. 2 means values are repeated once. 3 means values are repeated 3 times, etc' @('共輸出 3 次','額外重複 2 次','0 的實作與 1 相同','ConstantThrust','不是移動平均')
Assert-Meaning 'This controls optional EKF behaviour. Setting JammingExpected will change the EKF nehaviour such that if dead reckoning navigation is possible it will require the preflight alignment GPS quality checks controlled by EK3_GPS_CHECK and EK3_CHECK_SCALE to pass before resuming GPS use if GPS lock is lost for more than 2 seconds to prevent bad' @('避免不良的位置估計','EK3_GPS_CHECK','EK3_CHECK_SCALE','不據此加入其他版本')
Assert-Meaning 'The a component of distance = a*wind + b' @('LAND_DS_SLOPE_A','LAND_DS_SLOPE_B','b 常數項','航向風分量')
Assert-Meaning 'Simulates Airspeed sensor 1 failure' @('SIM_ARSPD_FAIL','SIM_ARSPD2_FAIL','第二個感測器','0 停用、1 啟用故障模擬')
Assert-Meaning 'The baud rate used for Serial8. Most stm32-based boards can support rates of up to 1500. If you setup a rate you cannot support and then can''t connect to your board you should load a firmware from a different vehicle type. That will reset all your parameters to defaults.' @('SERIAL9_BAUD 對應 Serial9','1500','所有參數重設')
Assert-Meaning 'This is the duration (ms) with which to hold the last driven servo command before timing out and zeroing the servo outputs. To disable zeroing of outputs in event of CAN loss, use 0. Use values greater than the expected duration between two CAN frames to ensure Periph is not starved of ESC Raw Commands.' @('0 PWM','不是回到中立位置','0 停用','servo actuator')
Assert-Meaning 'Displays main battery resting voltage' @('補償負載壓降','估計內阻','不是另一個感測器','不是充滿電壓')
Assert-Meaning 'Displays resting voltage for the average cell. WARNING: this can be inaccurate if the cell count is not detected or set properly. If the the battery is far from fully charged the detected cell count might not be accurate if auto cell count detection is used (OSD_CELL_COUNT=0).' @('除以電池節數','不是逐節量測值','OSD_CELL_COUNT=0','可能估錯節數')
Assert-Meaning 'The action to take on a long (FS_LONG_TIMEOUT seconds) failsafe event. If the aircraft was in a stabilization or manual mode when failsafe started and a long failsafe occurs then it will change to RTL mode if FS_LONG_ACTN is 0 or 1, and will change to FBWA if FS_LONG_ACTN is set to 2. If the aircraft was in an auto mode (such as AUTO or GUIDED) when the failsafe started then it will continue in the auto mode if FS_LONG_ACTN is set to 0, will change to RTL mode if FS_LONG_ACTN is set to 1 and will change to FBWA mode if FS_LONG_ACTN is set to 2. If FS_LONG_ACTION is set to 3, the parachute will be deployed (make sure the chute is configured and enabled).' @('FS_LONG_ACTN=3','FS_LONG_ACTION','拼字錯誤','不是另一個參數')
Assert-Meaning 'Collective blade pitch angle at zero thrust in degrees. For symetric airfoil blades this value is zero deg. For chambered airfoil blades this value is typically negative.' @('cambered','翼型彎度','不是弧度 rad','負角度')
Assert-Meaning 'This parameter sets the gain between demanded airspeed and pitch. It has units of radians per metre per second and should generally be negative. A good starting value is -0.04 for gliders and -0.08 for draggy airframes. The default (0.0) disables this feed-forward.' @('rad/(m/s)','不是角速度','-0.04','-0.08','0.0 停用')
Assert-Meaning 'minimum PWM pulse width in microseconds. Typically 1000 is lower limit, 1500 is neutral and 2000 is upper limit.' @('微秒','µs','microseconds')
Assert-Meaning 'maximum PWM pulse width in microseconds. Typically 1000 is lower limit, 1500 is neutral and 2000 is upper limit.' @('微秒','µs','microseconds')
Assert-Meaning 'Trim PWM pulse width in microseconds. Typically 1000 is lower limit, 1500 is neutral and 2000 is upper limit.' @('微秒','µs','microseconds')
Assert-Meaning 'RC trim (neutral) PWM pulse width in microseconds. Typically 1000 is lower limit, 1500 is neutral and 2000 is upper limit.' @('微秒','µs','microseconds')
Assert-Meaning 'RC maximum PWM pulse width in microseconds. Typically 1000 is lower limit, 1500 is neutral and 2000 is upper limit.' @('微秒','µs','microseconds')
Assert-Meaning 'RC minimum PWM pulse width in microseconds. Typically 1000 is lower limit, 1500 is neutral and 2000 is upper limit.' @('微秒','µs','microseconds')
Assert-Meaning 'PWM dead zone in microseconds around trim or bottom' @('微秒','µs','microseconds')
Assert-Meaning 'This is the target altitude for TAKEOFF mode' @('單位 m','相對起飛起始位置','不是海拔高度','不是每次切入都再爬升')
Assert-Meaning 'Time (in seconds) to pause in a VTOL loiter above landing point before starting final descent. Zero disables. This applies in VTOL landing in auto mode and QRTL mode.' @('0 只略過這段等待','不是停用降落或 QRTL','AUTO')
Assert-Meaning 'Altitude the vehicle will move to as the final stage of Returning to Launch or after completing a mission. Set to zero to land.' @('以 Home 為高度基準','0 表示降落','RC failsafe','不是所有任務結束都會套用')
Assert-Meaning 'Vehicle will continue landing vertically until this height if target is not found. Below this height if landing target is not found, landing retry/failsafe might be attempted. This needs a rangefinder to work. Set to zero to disable this.' @('高於此高度','有效 rangefinder','0 只取消這個高度上限','不是停用精準降落')
Assert-Meaning 'Vehicle will continue landing vertically even if target is lost below this height. This needs a rangefinder to work. Set to zero to disable this.' @('低於此高度','有效 rangefinder','0 只取消這個低高度例外','不是停用精準降落')
foreach ($verb in @('attemp','attempt')) {
    Assert-Meaning "Time for which vehicle continues descend even if target is lost. After this time period, vehicle will $verb a landing retry depending on PLND_STRICT parameter." @('最後一次有效目標輸出','PLND_STRICT','不保證一定重試','0 不是停用重試')
}
Assert-Meaning 'PrecLand Maximum number of retires for a failed landing. Set to zero to disable landing retry.' @('0 只停用降落重試','不是停用精準降落','failsafe','垂直降落或懸停')
Assert-Meaning 'This parameter sets the slew rate for the throttle during auto takeoff. When this is zero the THR_SLEWRATE parameter is used during takeoff. For rolling takeoffs it can be a good idea to set a lower slewrate for takeoff to give a slower acceleration which can improve ground steering control. The value is a percentage throttle change per second, so a value of 20 means to advance the throttle over 5 seconds on takeoff. Values below 20 are not recommended as they may cause the plane to try to climb out with too little throttle. A value of -1 means no limit on slew rate in takeoff.' @('每秒油門百分點','0%','100%','THR_SLEWRATE','-1')
Assert-Meaning 'This parameter sets the time delay (in 1/10ths of a second) that the ground speed check is delayed after the forward acceleration check controlled by TKOFF_THR_MINACC has passed. For hand launches with pusher propellers it is essential that this is set to a value of no less than 2 (0.2 seconds) to ensure that the aircraft is safely clear of the throwers arm before the motor can start. For bungee launches a larger value can be used (such as 30) to give time for the bungee to release from the aircraft before the motor is started.' @('30 為 3 秒','不是 30 秒','不是時間一到就必定啟動馬達','TKOFF_THR_MINACC')
Assert-Meaning 'This is the timeout for an automatic takeoff. If this is non-zero and the aircraft does not reach a ground speed of at least 4 m/s within this number of seconds then the takeoff is aborted and the vehicle disarmed. If the value is zero then no timeout applies.' @('4 m/s GPS 地速','清除此回合','0 只停用這項起飛逾時檢查')
Assert-Meaning 'This is the number of acceleration events to require for arming with TKOFF_THR_MINACC. The default is 1, which means a single forward acceleration above TKOFF_THR_MINACC will arm. By setting this higher than 1 you can require more forward/backward movements to arm.' @('前向／後向加速度交替','0.5 秒','不是將飛控解鎖','TKOFF_THR_MINACC')
Assert-Meaning 'This param controls how much headwind compensation is used when landing. Headwind speed component multiplied by this parameter is added to TECS_LAND_ARSPD command. Set to Zero to disable. Note: The target landing airspeed command is still limited to AIRSPEED_MAX.' @('此值 ÷ 100','不是 50 倍','AIRSPEED_MAX')
Assert-Meaning 'This param controls how much headwind compensation is used when landing. Headwind speed component multiplied by this parameter is added to TECS_LAND_ARSPD command. Set to Zero to disable. Note: The target landing airspeed command is still limited to ARSPD_FBW_MAX.' @('此值 ÷ 100','不是 50 倍','ARSPD_FBW_MAX')
Assert-Meaning 'Desired airspeed during pre-flare flight stage. This is useful to reduce airspeed just before the flare. Use 0 to disable.' @('0 只停用 pre-flare 階段','不是要求零空速','m/s')
Assert-Meaning 'Altitude in autoland at which to lock heading and flare to the LAND_PITCH_CD pitch. Note that this option is secondary to LAND_FLARE_SEC. For a good landing it preferable that the flare is triggered by LAND_FLARE_SEC.' @('LAND_PITCH_CD','Pitch 下限','不必同時滿足時間條件')
Assert-Meaning 'Altitude in autoland at which to lock heading and flare to the LAND_PITCH_DEG pitch. Note that this option is secondary to LAND_FLARE_SEC. For a good landing it preferable that the flare is triggered by LAND_FLARE_SEC.' @('LAND_PITCH_DEG','Pitch 下限','不必同時滿足時間條件')
Assert-Meaning 'When enabled, after an autoland and auto-disarm via LAND_DISARMDELAY happens then set all servos to neutral. This is helpful when an aircraft has a rough landing upside down or a crazy angle causing the servos to strain.' @('控制舵面 servo','不是所有輸出通道','LAND_DISARMDELAY','不代表切斷 servo 電源')
Assert-Meaning 'After a landing has completed using a LAND waypoint, automatically disarm after this many seconds have passed. Use 0 to not disarm.' @('不再飛行','不是一進入 flare','不禁止手動上鎖')
Assert-Meaning 'Allow a landing abort to trigger with an input throttle >= 90%. This works with or without stick-mixing enabled.' @('遙控器油門輸入 >= 90%','不是以馬達實際油門輸出判斷','stick-mixing','條件')
# Parse only selected entries: Windows PowerShell treats full-catalog keys as case-insensitive.
$batterySources = @(Get-Content "$PSScriptRoot/Localization/Parameters.zh-TW.reviewed.json" -Encoding UTF8 | Where-Object { $_ -match '^  "Battery capacity at which' } | ForEach-Object {
    $entryObject = ('{' + $_.Trim().TrimEnd(',') + '}') | ConvertFrom-Json
    $entryObject.PSObject.Properties
})
if ($batterySources.Count -ne 48) { throw "Expected 48 reviewed battery capacity source variants" }
foreach ($entry in $batterySources) {
    $action = [regex]::Match($entry.Name, 'specified by the (\S+) parameter').Groups[1].Value
    Assert-Meaning $entry.Name @('剩餘容量門檻','單位 mAh','不是已消耗容量或百分比','具備電流監測','不等待低電壓持續時間','0 僅停用此剩餘容量門檻','不停用電壓門檻',$action)
}
Assert-Meaning 'This is the timeout in seconds before a low voltage event will be triggered. For aircraft with low C batteries it may be necessary to raise this in order to cope with low voltage on long takeoffs. A value of zero disables low voltage errors.' @('連續低於門檻','電壓恢復後重新計時','各自計時','不是立即觸發','不會停用剩餘容量 failsafe 或解鎖前電壓檢查')
$armingSources = @(Get-Content "$PSScriptRoot/Localization/Parameters.zh-TW.reviewed.json" -Encoding UTF8 | Where-Object { $_ -match '^  "Battery capacity remaining which is required to arm' } | ForEach-Object {
    $entryObject = ('{' + $_.Trim().TrimEnd(',') + '}') | ConvertFrom-Json
    $entryObject.PSObject.Properties
})
if ($armingSources.Count -ne 32) { throw 'Expected 32 arming capacity source variants' }
foreach ($entry in $armingSources) {
    $voltage = [regex]::Match($entry.Name, 'with the (\S+) parameter').Groups[1].Value
    Assert-Meaning $entry.Name @('最低剩餘電池容量','單位 mAh','不是已消耗容量或百分比','0 僅停用此解鎖容量門檻','其他電池檢查或飛行中 failsafe','除智慧電池外','並不代表電池已充滿',$voltage)
}
Assert-Meaning 'Battery voltage level which is required to arm the aircraft. Set to 0 to allow arming at any voltage.' @('單位 V','不是壓降補償後','0 僅停用此解鎖電壓門檻','不保證可以解鎖','飛行中 failsafe')
Assert-Meaning 'Number of amps that a 1V reading on the current sensor corresponds to. With a Pixhawk using the 3DR Power brick this should be set to 17. For the Pixhawk with the 3DR 4in1 ESC this should be 17.' @('單位 A/V','不是電流限制值')
Assert-Meaning 'Voltage offset at zero current on the current sensor.' @('單位 V','先從感測器電壓減去','不是直接扣除安培數')
Assert-Meaning 'Voltage offset at zero current on current sensor' @('單位 V','先從感測器電壓減去','不是直接扣除安培數')
Assert-Meaning 'Voltage offset on voltage pin. This allows for an offset due to a diode. This voltage is subtracted before the scaling is applied' @('單位 V','先減去 offset 再乘倍率','不是在換算後')
Assert-Meaning 'Number of amps that a 1V reading on the current sensor corresponds to.' @('單位 A/V','不是電流限制值')
Assert-Meaning 'Voltage offset on voltage pin. This allows for an offset due to a diode. This voltage is subtracted before the scaling is applied.' @('單位 V','先減去 offset 再乘倍率','不是在換算後')
Assert-Meaning 'Number of amps that a 1V reading on the current sensor corresponds to. With a Pixhawk using the 3DR Power brick this should be set to 17. For the Pixhawk with the 3DR 4in1 ESC this should be 17. For Synthetic Current sensor monitors, this is the maximum, full throttle current draw.' @('單位 A/V','不是電流限制值','估算係數（A）','不是實測電流或硬性上限')
Assert-Meaning 'Voltage offset at zero current on current sensor for Analog Sensors. For Synthetic Current sensor, this offset is the zero throttle system current and is added to the calculated throttle base current.' @('單位 V','先從感測器電壓減去','不是直接扣除安培數','零油門系統電流（A）','會加到')
Assert-Meaning 'Maximum voltage of battery. Provides scaling of current versus voltage' @('Synthetic Current','單位 V','目前電池電壓除以此值','不是過電壓保護門檻')
'PASS: 156 semantic source cases (including battery source variants) retained'
