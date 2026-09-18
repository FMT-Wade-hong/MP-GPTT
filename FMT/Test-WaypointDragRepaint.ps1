$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '../GCSViews/FlightPlanner.cs') -Raw
$move = $source.Substring($source.IndexOf('private void MainMap_MouseMove('))
$move = $move.Substring(0, $move.IndexOf('else if (CurrentPOIMarker != null)'))
$drag = $move.Substring($move.IndexOf('else if (CurentRectMarker != null)'))
if ($drag.Contains('Environment.TickCount') -or $drag.Contains('fmtLastWaypointDragRenderTick')) { throw 'Mouse event dropping remains' }
if ($drag.Contains('redrawPolygonSurvey(')) { throw 'Survey layer rebuilt during drag' }
if ($drag.Contains('UpdateMarkerLocalPosition(')) { throw 'Duplicate marker position updates remain' }
if (!$drag.Contains('MainMap.HoldInvalidation = true;') -or
    $drag -notmatch 'finally\s*\{\s*MainMap.HoldInvalidation = wasHoldingInvalidation;') { throw 'Invalidation state not restored' }
if ($source -notmatch 'callMeDrag\(CurentRectMarker.InnerMarker.Tag.ToString\(\), MouseDownEnd.Lat,\s*MouseDownEnd.Lng, -2\)') { throw 'Release endpoint changed' }
'PASS: drag source guards: no dropped events, no layer rebuild, no duplicate marker updates, repaint restored, exact release endpoint'
'NOTE: structural regression checks, not a measured UI frame-rate benchmark.'
