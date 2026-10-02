$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$source = Get-Content "$PSScriptRoot/../GCSViews/FlightData.cs" -Raw -Encoding UTF8
$start = $source.IndexOf('private void customizeToolStripMenuItem_Click(')
$end = $source.IndexOf('private void deleteToolStripMenuItem_Click(', $start)
$handler = $source.Substring($start, $end - $start)
foreach ($required in @('left.DisplayMember = nameof(TabPage.Text)', 'left.Items.Add(tabPage, true)', 'left.Items.Add(tabPage, false)', 'foreach (TabPage tabPage in left.CheckedItems)', 'answer += tabPage.Name + ";"', '"Visible Tabs"')) {
    if (!$handler.Contains($required)) { throw "Missing label/persistence behavior: $required" }
}
$list = New-Object Windows.Forms.CheckedListBox
$pages = @()
try {
    $list.DisplayMember = 'Text'
    $names = @('tabQuick','tabActions','tabActionsSimple','tabPagemessages','tabPagePreFlight','tabGauges','tabTransponder','tabStatus','tabServo','tabAuxFunction','tabScripts','tabPayload','tabTLogs','tablogbrowse')
    foreach ($name in $names) {
        $match = [regex]::Match($source, [regex]::Escape($name) + '\.Text = "([^"\r\n]+)";')
        if (!$match.Success -or $match.Groups[1].Value -notmatch '[\u3400-\u9fff]') { throw "Missing Chinese caption: $name" }
        $page = New-Object Windows.Forms.TabPage
        $page.Name = $name
        $page.Text = $match.Groups[1].Value
        $pages += $page
        [void]$list.Items.Add($page, ($pages.Count % 2 -eq 1))
        if ($list.GetItemText($page) -cne $page.Text) { throw "Caption not used: $name" }
    }
    $saved = ($list.CheckedItems | ForEach-Object { $_.Name }) -join ';'
    $expected = ($names | Where-Object { [Array]::IndexOf($names,$_) % 2 -eq 0 }) -join ';'
    if ($saved -cne $expected) { throw 'Checked states or stored IDs changed' }
    'PASS: 14 Chinese captions, display binding and checked-state persistence use stable IDs; no user settings written'
} finally {
    $list.Dispose()
    foreach ($page in $pages) { $page.Dispose() }
}
