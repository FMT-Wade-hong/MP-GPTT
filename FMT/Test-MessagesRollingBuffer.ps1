param([string]$Directory = 'bin/MessagesRefreshFix/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.Controls.MessagesList',$true)
$control = [Activator]::CreateInstance($type)
$method = $type.GetMethod('UpdateMessages')
$listType = $method.GetParameters()[0].ParameterType
$tupleType = $listType.GetGenericArguments()[0]
$source = [Activator]::CreateInstance($listType)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$bufferField = $type.GetField('messages',$flags)
$now = [DateTime]::UtcNow
function Add-Message($target, [string]$text, [byte]$severity = 6) {
    $item = [Activator]::CreateInstance($tupleType, [object[]]@($now,$text,$severity))
    $target.Add($item)
}
function Refresh-Messages($target) {
    $method.Invoke($control,[object[]]@(,$target)) | Out-Null
}
function Assert-Latest([string]$expected) {
    $actual = $bufferField.GetValue($control)
    if ($actual.Count -eq 0 -or $actual[$actual.Count-1].Item2 -ne $expected) {
        throw "Latest message not refreshed: $expected"
    }
}
try {
    # Identical timestamps deliberately exercise bursts within one second.
    for ($i = 0; $i -lt 2500; $i++) {
        Add-Message $source "Message $i"
        if ($source.Count -gt 1000) { $source.RemoveAt(0) }
        Refresh-Messages $source
        Assert-Latest "Message $i"
    }
    $before = $bufferField.GetValue($control)
    Refresh-Messages $source
    if (![object]::ReferenceEquals($before,$bufferField.GetValue($control))) { throw 'Unchanged buffer replaced' }
    # Equal-length buffers from different vehicles must also replace the view.
    $other = [Activator]::CreateInstance($listType)
    for ($i = 0; $i -lt 1000; $i++) { Add-Message $other "Vehicle B $i" }
    Refresh-Messages $other
    Assert-Latest 'Vehicle B 999'
    $other.Clear()
    Refresh-Messages $other
    if ($bufferField.GetValue($control).Count -ne 0) { throw 'Clear not reflected' }
    Add-Message $other 'After reconnect'
    Refresh-Messages $other
    Assert-Latest 'After reconnect'
    'PASS: 2500 same-timestamp messages; rolling 1000-row buffer; unchanged snapshot; vehicle switch; clear and refill'
} finally { $control.Dispose() }
