param(
    [string]$ExecutablePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Release\net461\FMTPlanner.exe'),
    [string]$LoginPassword = '1234',
    [int]$WarmupSeconds = 8,
    [int]$SampleSeconds = 10,
    [string]$PackagePath = '',
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "FMTPlanner executable was not found: $ExecutablePath"
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$resolvedExecutable = (Resolve-Path -LiteralPath $ExecutablePath).Path
$process = $null
$launchedProcess = $null
$gcCounters = @()

try {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $launchedProcess = Start-Process -FilePath $resolvedExecutable -WorkingDirectory (Split-Path $resolvedExecutable) -PassThru
    $process = $launchedProcess
    $processCondition = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::ProcessIdProperty,
        $process.Id)
    $loginVisibleMs = $null
    $mainWindowReadyMs = $null
    $mainWindowTitle = $null
    $loginSubmitted = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(90)

    while ([DateTime]::UtcNow -lt $deadline -and -not $process.HasExited) {
        Start-Sleep -Milliseconds 100
        $windows = [Windows.Automation.AutomationElement]::RootElement.FindAll(
            [Windows.Automation.TreeScope]::Children,
            $processCondition)

        foreach ($window in $windows) {
            $windowName = $window.Current.Name
            if ($windowName -like '* Login') {
                if ($null -eq $loginVisibleMs) {
                    $loginVisibleMs = $clock.ElapsedMilliseconds
                }

                if (-not $loginSubmitted) {
                    $controls = $window.FindAll(
                        [Windows.Automation.TreeScope]::Descendants,
                        [Windows.Automation.Condition]::TrueCondition)
                    $edits = @($controls | Where-Object { $_.Current.ClassName -like '*EDIT*' })
                    $signIn = @($controls | Where-Object { $_.Current.Name -eq 'Sign in' }) |
                        Select-Object -First 1

                    if ($edits.Count -ge 2 -and $null -ne $signIn) {
                        $valuePattern = $edits[1].GetCurrentPattern(
                            [Windows.Automation.ValuePattern]::Pattern)
                        $valuePattern.SetValue($LoginPassword)
                        $invokePattern = $signIn.GetCurrentPattern(
                            [Windows.Automation.InvokePattern]::Pattern)
                        $invokePattern.Invoke()
                        $loginSubmitted = $true
                    }
                }
            }
            elseif ($windowName -like 'FeiMaoTecPlanner V*') {
                $descendantCount = $window.FindAll(
                    [Windows.Automation.TreeScope]::Descendants,
                    [Windows.Automation.Condition]::TrueCondition).Count
                if ($descendantCount -gt 20) {
                    $mainWindowReadyMs = $clock.ElapsedMilliseconds
                    $mainWindowTitle = $windowName
                    $mainWindowProcessId = $window.Current.ProcessId
                    if ($mainWindowProcessId -ne $process.Id) {
                        $process = Get-Process -Id $mainWindowProcessId -ErrorAction Stop
                    }
                    break
                }
            }
        }

        if ($null -ne $mainWindowReadyMs) {
            break
        }
    }

    if ($null -eq $mainWindowReadyMs) {
        $visibleWindows = @($windows | ForEach-Object { $_.Current.Name }) -join '; '
        throw "FMTPlanner main window was not ready within 90 seconds. Visible windows: $visibleWindows"
    }

    Start-Sleep -Seconds $WarmupSeconds

    $generationCounters = @{}
    try {
        $category = New-Object Diagnostics.PerformanceCounterCategory('.NET CLR Memory')
        $instance = $category.GetInstanceNames() | Where-Object {
            $processIdCounter = New-Object Diagnostics.PerformanceCounter(
                '.NET CLR Memory', 'Process ID', $_, $true)
            try {
                [int]$processIdCounter.NextValue() -eq $process.Id
            }
            finally {
                $processIdCounter.Dispose()
            }
        } | Select-Object -First 1

        if ($instance) {
            foreach ($generation in 0..2) {
                $counter = New-Object Diagnostics.PerformanceCounter(
                    '.NET CLR Memory', "# Gen $generation Collections", $instance, $true)
                $generationCounters[$generation] = $counter
                $gcCounters += $counter
            }
        }
    }
    catch {
        Write-Warning "GC performance counters are unavailable: $($_.Exception.Message)"
    }

    $samples = @()
    for ($index = 0; $index -lt $SampleSeconds; $index++) {
        $process.Refresh()
        $cpuStart = $process.TotalProcessorTime.TotalMilliseconds
        Start-Sleep -Seconds 1
        $process.Refresh()
        $cpuEnd = $process.TotalProcessorTime.TotalMilliseconds

        $samples += [pscustomobject]@{
            WorkingSetBytes = $process.WorkingSet64
            PrivateBytes = $process.PrivateMemorySize64
            CpuPercent = [math]::Round(
                (($cpuEnd - $cpuStart) / 1000 / [Environment]::ProcessorCount) * 100,
                3)
            Threads = $process.Threads.Count
            Handles = $process.HandleCount
        }
    }

    $executable = Get-Item -LiteralPath $resolvedExecutable
    $package = if (-not [string]::IsNullOrWhiteSpace($PackagePath) -and
        (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
        Get-Item -LiteralPath $PackagePath
    }

    $result = [ordered]@{
        Timestamp = (Get-Date).ToString('o')
        Version = $process.MainModule.FileVersionInfo.FileVersion
        MainWindowTitle = $mainWindowTitle
        LoginVisibleMs = $loginVisibleMs
        MainWindowReadyMs = $mainWindowReadyMs
        WarmupSeconds = $WarmupSeconds
        SampleSeconds = $SampleSeconds
        WorkingSetAvgBytes = [long](($samples.WorkingSetBytes | Measure-Object -Average).Average)
        WorkingSetPeakBytes = [long](($samples.WorkingSetBytes | Measure-Object -Maximum).Maximum)
        PrivateBytesAvg = [long](($samples.PrivateBytes | Measure-Object -Average).Average)
        CpuAvgPercent = [math]::Round(($samples.CpuPercent | Measure-Object -Average).Average, 3)
        CpuMaxPercent = [math]::Round(($samples.CpuPercent | Measure-Object -Maximum).Maximum, 3)
        ThreadsAvg = [math]::Round(($samples.Threads | Measure-Object -Average).Average, 1)
        HandlesAvg = [math]::Round(($samples.Handles | Measure-Object -Average).Average, 1)
        Gen0Collections = if ($generationCounters.ContainsKey(0)) { [int]$generationCounters[0].NextValue() } else { $null }
        Gen1Collections = if ($generationCounters.ContainsKey(1)) { [int]$generationCounters[1].NextValue() } else { $null }
        Gen2Collections = if ($generationCounters.ContainsKey(2)) { [int]$generationCounters[2].NextValue() } else { $null }
        ExecutableBytes = $executable.Length
        PackageBytes = if ($package) { $package.Length } else { $null }
        PackageSHA256 = if ($package) {
            (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        } else { $null }
        LogicalProcessors = [Environment]::ProcessorCount
    }

    $json = $result | ConvertTo-Json
    $json

    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $outputFullPath = [IO.Path]::GetFullPath($OutputPath)
        $outputDirectory = Split-Path $outputFullPath -Parent
        if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
            New-Item -ItemType Directory -Path $outputDirectory | Out-Null
        }
        Set-Content -LiteralPath $outputFullPath -Value $json -Encoding UTF8
    }
}
finally {
    foreach ($counter in $gcCounters) {
        $counter.Dispose()
    }

    if ($null -ne $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(8000)) {
            Stop-Process -Id $process.Id
        }
    }

    if ($null -ne $launchedProcess -and $launchedProcess.Id -ne $process.Id -and -not $launchedProcess.HasExited) {
        $launchedProcess.CloseMainWindow() | Out-Null
        if (-not $launchedProcess.WaitForExit(3000)) {
            Stop-Process -Id $launchedProcess.Id
        }
    }
}
