param([string]$Directory='bin/Release124/net461', [string]$ReportDirectory='tmp/release124-tests')
$ErrorActionPreference='Stop'
[void][IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($ReportDirectory))
$results=@()
$tests=@('Local124','CrashGuard124','DualGpsToolbar','MotorTestGuard','AdsbHttp','IceSetup','HudCompact',
    'ScreenSizeMatrix','TranslationAudit124','AllParameterTranslation','TranslationSemantics',
    'ReviewedParameterTranslation','ParameterLocalization')
foreach($name in $tests) {
    $timer=[Diagnostics.Stopwatch]::StartNew()
    $output=Join-Path $ReportDirectory ($name+'.out')
    $errors=Join-Path $ReportDirectory ($name+'.err')
    $test=Join-Path $PSScriptRoot ('Test-'+$name+'.ps1')
    $process=Start-Process powershell.exe -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File',('"'+$test+'"'),'-Directory',('"'+$Directory+'"') -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $output -RedirectStandardError $errors
    $results+=[pscustomobject]@{Test=$name;ExitCode=$process.ExitCode;Seconds=[Math]::Round($timer.Elapsed.TotalSeconds,1)}
    $results | ConvertTo-Json | Set-Content (Join-Path $ReportDirectory 'results.json') -Encoding UTF8
    Write-Output "$name exit=$($process.ExitCode) seconds=$([Math]::Round($timer.Elapsed.TotalSeconds,1))"
}
if(@($results | Where-Object ExitCode -ne 0).Count){throw 'Release regression failed; inspect results.json and per-test logs'}
'PASS: baseline 50 regression scripts plus 12 additional suites; no real vehicle connection.'
