param([string]$Directory = 'bin/P400FrequencyTest/net461')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$outputDirectory = (Resolve-Path $Directory).Path
[Reflection.Assembly]::LoadFrom((Join-Path $outputDirectory 'FMTPlanner.exe')) | Out-Null
$plan = New-Object MissionPlanner.FMT.P400FrequencyPlan
$plan.Name = 'Test combination'
$plan.Frequencies[0] = '420.3'
$plan.Frequencies[49] = '469.300001'
$csv = $plan.ToCsv()
$roundtrip = [MissionPlanner.FMT.P400FrequencyPlan]::Parse($csv)
if ($roundtrip.Frequencies.Length -ne 50 -or $roundtrip.Frequencies[49] -ne '469.300001' -or $roundtrip.Frequencies[1] -ne '') { throw 'CSV roundtrip' }
'PASS: CSV roundtrip, six decimals, gaps, 50 entries'
$single = [MissionPlanner.FMT.P400FrequencyPlan]::Parse("420.3`n421.3")
$at = [MissionPlanner.FMT.P400FrequencyPlan]::Parse("Ch Freq(MHz)`n1 420.300000`n2 421.300000")
if ($single.Frequencies[1] -ne $at.Frequencies[1]) { throw 'AT and single-column parsing' }
'PASS: AT table and single-column paste'
foreach ($bad in @(('1,420.3' + "`n" + '1,421.3'), '51,420.3', '1,0', '1,420.1234567', '1,420MHz', '1,NaN')) {
    $rejected = $false
    try { [MissionPlanner.FMT.P400FrequencyPlan]::Parse($bad) | Out-Null } catch { $rejected = $true }
    if (!$rejected) { throw "Accepted invalid data: $bad" }
}
'PASS: duplicates, overflow, zero, excess precision, units and NaN rejected'
$testDirectory = Join-Path $outputDirectory ('frequency-tests-' + [Guid]::NewGuid().ToString('N'))
$path = Join-Path $testDirectory 'slot-01.json'
$plan.Save($path)
$plan.Name = 'Updated combination'
$plan.Save($path)
$saved = [MissionPlanner.FMT.P400FrequencyPlan]::Load($path)
if ($saved.Name -ne 'Updated combination' -or $saved.Frequencies[49] -ne '469.300001' -or !(Test-Path ($path+'.bak'))) { throw 'Slot save/load/backup' }
'PASS: named slot, exact values, atomic replace with backup'
$page = New-Object MissionPlanner.GCSViews.ConfigurationView.ConfigP400FrequencyTable
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$slots = $page.GetType().GetField('slots',$flags).GetValue($page)
$grid = $page.GetType().GetField('grid',$flags).GetValue($page)
if ($slots.Items.Count -ne 10 -or $grid.Rows.Count -ne 50) { throw 'UI counts' }
$page.Size = New-Object Drawing.Size 1400,850
$page.CreateControl(); $page.PerformLayout()
$image = New-Object Drawing.Bitmap 1400,850
$page.DrawToBitmap($image, (New-Object Drawing.Rectangle 0,0,1400,850))
$image.Save((Join-Path $outputDirectory 'P400-frequency-preview.png'))
$image.Dispose(); $page.Dispose()
'PASS: UI has ten slots and fifty rows; no serial connection'
