param([string]$Directory = "$PSScriptRoot\..\bin\RelayTestButtons\net461")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path (Resolve-Path $Directory) 'FMTPlanner.exe'))
$type = $assembly.GetType('MissionPlanner.Radio.Sikradio', $false)
if (!$type) { $type = $assembly.GetTypes() | Where-Object { $_.Name -eq 'Sikradio' } | Select-Object -First 1 }
$instance = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($type)
$method = $type.GetMethod('UpdateControlWithValue', [Reflection.BindingFlags]'Instance,NonPublic')
$settings = [Activator]::CreateInstance($method.GetParameters()[1].ParameterType)
$settingType = $assembly.GetType('RFD.RFD900.TShortSetting', $true)
foreach ($remote in @($false, $true)) {
    foreach ($pair in @(@('MIN_FREQ', 433050), @('MAX_FREQ', 434790), @('TXPOWER', 11))) {
        $name = $pair[0]
        $value = [int]$pair[1]
        $settings[$name] = [Activator]::CreateInstance($settingType, [object[]]@('S8', $name, $value))
        $panel = New-Object Windows.Forms.Panel
        $panel.BindingContext = New-Object Windows.Forms.BindingContext
        $combo = New-Object Windows.Forms.ComboBox
        $combo.DropDownStyle = 'DropDownList'
        $combo.Name = $(if ($remote) { 'R' }) + $name
        $panel.Controls.Add($combo)
        $combo.DataSource = [int[]](414000..460000 | Where-Object { $_ % 50 -eq 0 })
        $failed = $method.Invoke($instance, [object[]]@($combo.PSObject.BaseObject, $settings.PSObject.BaseObject, $name, "$value", $remote))
        if ($failed -or $combo.Text -ne "$value") { throw "Frequency mismatch: $name = $($combo.Text)" }
        if ($name -eq 'TXPOWER' -and (($combo.Items | ForEach-Object { "$_" }) -join ',') -ne '1,2,5,8,11,14,17,20') {
            throw 'Unexpected TXPOWER options'
        }
        Write-Output "PASS remote=$remote $name=$($combo.Text) (legacy TShortSetting)"
        $panel.Dispose()
    }
}
