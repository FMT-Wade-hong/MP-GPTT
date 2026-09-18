param([string]$Directory = 'bin/RelayCommandLayoutFix/net461')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/FMTPlanner.exe"))
$method = $asm.GetType('MissionPlanner.FMT.FmtRelayControlPanel').GetMethod('LayoutControlCommands', [Reflection.BindingFlags]'Static,NonPublic')
$bar = New-Object Windows.Forms.Panel
$bar.Visible = $false
$bar.AutoScroll = $true
$take = New-Object Windows.Forms.Button
$revoke = New-Object Windows.Forms.Button
$stop = New-Object Windows.Forms.Button
$take.Size = [Drawing.Size]::new(126,30)
$revoke.Size = [Drawing.Size]::new(112,30)
$stop.Size = [Drawing.Size]::new(104,30)
$bar.Controls.AddRange([Windows.Forms.Control[]]@($take,$revoke,$stop))
try {
    foreach ($width in @(1200,600,330,1600)) {
        $bar.Width = $width
        foreach ($main in @($true,$false,$true)) {
            $revoke.Visible = $stop.Visible = $main
            $take.Width = $(if ($main) {126} else {142})
            $argsForLayout = New-Object 'System.Object[]' 5
            $argsForLayout[0] = $bar.PSObject.BaseObject
            $argsForLayout[1] = $take.PSObject.BaseObject
            $argsForLayout[2] = $revoke.PSObject.BaseObject
            $argsForLayout[3] = $stop.PSObject.BaseObject
            $argsForLayout[4] = $main
            $method.Invoke($null,$argsForLayout) | Out-Null
            if ($main -and ($take.Bounds.IntersectsWith($revoke.Bounds) -or $revoke.Bounds.IntersectsWith($stop.Bounds))) { throw 'Buttons overlap' }
            if ($take.Top -lt 58 -and $take.Left -lt 520) { throw 'Buttons overlap status text' }
            if ($take.Bottom -gt $bar.ClientSize.Height) { throw 'Buttons clipped vertically' }
        }
    }
    'PASS: hidden parent, main/secondary/main switching and widths 330/600/1200/1600 retain non-overlapping buttons'
} finally { $bar.Dispose() }
