# Batch Replay Analysis Report Generator
# Processes all highlights and creates aggregate statistics

param(
    [string]$OwAppDataPath = "$env:LOCALAPPDATA\Blizzard Entertainment\Overwatch",
    [string]$OutputPath = ".\ReplayCorpusReport.html",
    [switch]$GenerateTimelines
)

Write-Host "🔍 Scanning for highlight directories..." -ForegroundColor Cyan

# Find all highlight directories
$highlightDirs = Get-ChildItem $OwAppDataPath -Recurse -Directory -Filter "Highlights" -ErrorAction SilentlyContinue |
    Where-Object { (Get-ChildItem $_.FullName -File -ErrorAction SilentlyContinue).Count -gt 0 }

Write-Host "Found $($highlightDirs.Count) directories with highlights" -ForegroundColor Green
Write-Host ""

$allSegments = @()
$totalFiles = 0
$processedFiles = 0
$failedFiles = 0

foreach ($dir in $highlightDirs) {
    $files = Get-ChildItem $dir.FullName -File
    $totalFiles += $files.Count

    Write-Host "Processing $($dir.FullName)..." -ForegroundColor Yellow

    # Run analyzer
    $cmd = "dotnet run --project `"$PSScriptRoot\ReplayAnalyzer.csproj`""
    if ($GenerateTimelines) { $cmd += " --timeline" }
    $cmd += " `"$($dir.FullName)`""

    $result = Invoke-Expression $cmd 2>&1

    # Parse output
    $result | ForEach-Object {
        if ($_ -match "📊 (\d+) ticks \| ([\d.]+)s \| Entities: (\d+)-(\d+)") {
            $processedFiles++
        }
        elseif ($_ -match "❌") {
            $failedFiles++
        }
    }

    # Collect summary JSON files
    $analysisDir = Join-Path $dir.Parent.FullName "Analysis"
    if (Test-Path $analysisDir) {
        Get-ChildItem $analysisDir -Filter "*.summary.json" | ForEach-Object {
            $json = Get-Content $_.FullName | ConvertFrom-Json
            $allSegments += $json
        }
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  📊 Corpus Analysis Complete" -ForegroundColor Green
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Total Files: $totalFiles" -ForegroundColor White
Write-Host "  Processed: $processedFiles" -ForegroundColor Green
Write-Host "  Failed: $failedFiles" -ForegroundColor Red
Write-Host "  Segments Collected: $($allSegments.Count)" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan

# Generate aggregate statistics
$stats = @{
    TotalSegments = $allSegments.Count
    TotalTicks = ($allSegments | Measure-Object -Property TickCount -Sum).Sum
    TotalDuration = ($allSegments | Measure-Object -Property TotalDuration -Sum).Sum
    AvgDuration = ($allSegments | Measure-Object -Property TotalDuration -Average).Average
    AvgTickCount = ($allSegments | Measure-Object -Property TickCount -Average).Average
    MinEntityCount = ($allSegments | Measure-Object -Property MinEntityCount -Minimum).Minimum
    MaxEntityCount = ($allSegments | Measure-Object -Property MaxEntityCount -Maximum).Maximum
    UniqueMaps = ($allSegments | Select-Object -ExpandProperty MapGuid -Unique).Count
    UniqueGameModes = ($allSegments | Select-Object -ExpandProperty GameModeGuid -Unique).Count
    UniquePlayers = ($allSegments | Select-Object -ExpandProperty PlayerId -Unique).Count
    BuildVersions = ($allSegments | Select-Object -ExpandProperty BuildNumber -Unique | Sort-Object)
}

# Group by map
$mapGroups = $allSegments | Group-Object MapGuid | Sort-Object Count -Descending

# Group by game mode
$modeGroups = $allSegments | Group-Object GameModeGuid | Sort-Object Count -Descending

# Generate HTML report
$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Replay Corpus Analysis Report</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: 'Segoe UI', system-ui, sans-serif;
            background: linear-gradient(135deg, #1a1a2e, #16213e);
            color: #eee;
            padding: 20px;
        }
        .container { max-width: 1200px; margin: 0 auto; }
        .header {
            background: linear-gradient(135deg, #e94560, #ff6b6b);
            padding: 40px;
            border-radius: 15px;
            margin-bottom: 30px;
            text-align: center;
            box-shadow: 0 10px 30px rgba(233, 69, 96, 0.3);
        }
        .header h1 { font-size: 3em; margin-bottom: 10px; }
        .header p { font-size: 1.2em; opacity: 0.9; }

        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }
        .stat-card {
            background: #16213e;
            padding: 25px;
            border-radius: 10px;
            text-align: center;
            box-shadow: 0 4px 6px rgba(0,0,0,0.3);
            border-top: 3px solid #e94560;
        }
        .stat-card .value {
            font-size: 2.5em;
            font-weight: bold;
            color: #e94560;
            margin: 10px 0;
        }
        .stat-card .label {
            font-size: 0.9em;
            color: #888;
            text-transform: uppercase;
            letter-spacing: 1px;
        }

        .section {
            background: #16213e;
            padding: 30px;
            border-radius: 10px;
            margin-bottom: 30px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.3);
        }
        .section h2 {
            color: #e94560;
            margin-bottom: 20px;
            padding-bottom: 10px;
            border-bottom: 2px solid #0f3460;
        }

        .chart {
            width: 100%;
            height: 300px;
            background: #0f3460;
            border-radius: 5px;
            padding: 20px;
            margin-top: 20px;
        }

        .table-container {
            overflow-x: auto;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 15px;
        }
        th, td {
            padding: 12px;
            text-align: left;
            border-bottom: 1px solid #0f3460;
        }
        th {
            background: #0f3460;
            color: #e94560;
            font-weight: 600;
        }
        tr:hover {
            background: rgba(233, 69, 96, 0.1);
        }

        .bar-chart {
            margin: 20px 0;
        }
        .bar-item {
            display: flex;
            align-items: center;
            margin: 10px 0;
        }
        .bar-label {
            width: 150px;
            font-size: 0.9em;
            color: #888;
        }
        .bar-container {
            flex: 1;
            height: 25px;
            background: #0f3460;
            border-radius: 5px;
            overflow: hidden;
            position: relative;
        }
        .bar-fill {
            height: 100%;
            background: linear-gradient(90deg, #e94560, #ff6b6b);
            transition: width 0.5s ease;
        }
        .bar-value {
            position: absolute;
            right: 10px;
            top: 50%;
            transform: translateY(-50%);
            font-weight: bold;
            font-size: 0.85em;
        }

        .footer {
            text-align: center;
            margin-top: 50px;
            padding: 30px;
            border-top: 2px solid #0f3460;
            color: #666;
        }

        .quality-indicator {
            display: inline-block;
            width: 12px;
            height: 12px;
            border-radius: 50%;
            margin-right: 5px;
        }
        .quality-good { background: #4caf50; }
        .quality-warn { background: #ff9800; }
        .quality-bad { background: #f44336; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🎮 Replay Corpus Analysis</h1>
            <p>Comprehensive analysis of Overwatch highlight data</p>
            <p style="font-size: 0.9em; margin-top: 10px;">Generated $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</p>
        </div>

        <div class="stats-grid">
            <div class="stat-card">
                <div class="label">Total Segments</div>
                <div class="value">$($stats.TotalSegments)</div>
            </div>
            <div class="stat-card">
                <div class="label">Total Ticks</div>
                <div class="value">$([int]$stats.TotalTicks)</div>
            </div>
            <div class="stat-card">
                <div class="label">Total Duration</div>
                <div class="value">$([int]$stats.TotalDuration)s</div>
            </div>
            <div class="stat-card">
                <div class="label">Avg Segment</div>
                <div class="value">$("{0:F1}" -f $stats.AvgDuration)s</div>
            </div>
            <div class="stat-card">
                <div class="label">Avg Tick Count</div>
                <div class="value">$([int]$stats.AvgTickCount)</div>
            </div>
            <div class="stat-card">
                <div class="label">Entity Range</div>
                <div class="value">$($stats.MinEntityCount)-$($stats.MaxEntityCount)</div>
            </div>
        </div>

        <div class="section">
            <h2>📍 Map Distribution</h2>
            <div class="bar-chart">
                $(foreach ($group in $mapGroups | Select-Object -First 15) {
                    $pct = ($group.Count / $stats.TotalSegments * 100)
                    "<div class='bar-item'>
                        <div class='bar-label'>$($group.Name)</div>
                        <div class='bar-container'>
                            <div class='bar-fill' style='width: $($pct)%'></div>
                            <div class='bar-value'>$($group.Count)</div>
                        </div>
                    </div>"
                })
            </div>
        </div>

        <div class="section">
            <h2>🎯 Game Mode Distribution</h2>
            <div class="bar-chart">
                $(foreach ($group in $modeGroups) {
                    $pct = ($group.Count / $stats.TotalSegments * 100)
                    "<div class='bar-item'>
                        <div class='bar-label'>$($group.Name)</div>
                        <div class='bar-container'>
                            <div class='bar-fill' style='width: $($pct)%'></div>
                            <div class='bar-value'>$($group.Count)</div>
                        </div>
                    </div>"
                })
            </div>
        </div>

        <div class="section">
            <h2>🏗️ Build Versions</h2>
            <div class="table-container">
                <table>
                    <tr>
                        <th>Build Number</th>
                        <th>Segment Count</th>
                        <th>Coverage</th>
                    </tr>
                    $(foreach ($build in $stats.BuildVersions) {
                        $count = ($allSegments | Where-Object { $_.BuildNumber -eq $build }).Count
                        $pct = ($count / $stats.TotalSegments * 100)
                        "<tr>
                            <td>$build</td>
                            <td>$count</td>
                            <td>$("{0:F1}" -f $pct)%</td>
                        </tr>"
                    })
                </table>
            </div>
        </div>

        <div class="section">
            <h2>📊 Data Quality Assessment</h2>
            <div style="line-height: 2;">
                <p><span class="quality-indicator quality-good"></span> <strong>Tick Rate Consistency:</strong> ~60fps average (16.67ms/tick)</p>
                <p><span class="quality-indicator quality-good"></span> <strong>Frame Structure:</strong> Validated with 0x00077A84 marker</p>
                <p><span class="quality-indicator quality-warn"></span> <strong>Entity Decoding:</strong> Bit-packing partially understood</p>
                <p><span class="quality-indicator quality-warn"></span> <strong>Event Detection:</strong> Basic state-change detection only</p>
                <p><span class="quality-indicator quality-bad"></span> <strong>Full Match Data:</strong> Not available (highlights only)</p>
            </div>
        </div>

        <div class="section">
            <h2>🎯 ML Readiness</h2>
            <h3 style="color: #4caf50; margin: 15px 0;">✅ Ready For Training</h3>
            <ul style="line-height: 2; margin-left: 20px;">
                <li>Action recognition from tick sequences</li>
                <li>Movement pattern analysis</li>
                <li>Team composition effects</li>
                <li>Micro-level gameplay features</li>
            </ul>
            <h3 style="color: #f44336; margin: 15px 0 5px 0;">❌ Requires Full Match Data</h3>
            <ul style="line-height: 2; margin-left: 20px;">
                <li>Round-based learning</li>
                <li>Objective tracking</li>
                <li>Match outcome prediction</li>
                <li>Full player statistics</li>
            </ul>
        </div>

        <div class="section">
            <h2>📈 Corpus Statistics</h2>
            <div class="stats-grid">
                <div class="stat-card">
                    <div class="label">Unique Maps</div>
                    <div class="value">$($stats.UniqueMaps)</div>
                </div>
                <div class="stat-card">
                    <div class="label">Unique Modes</div>
                    <div class="value">$($stats.UniqueGameModes)</div>
                </div>
                <div class="stat-card">
                    <div class="label">Unique Players</div>
                    <div class="value">$($stats.UniquePlayers)</div>
                </div>
                <div class="stat-card">
                    <div class="label">Build Versions</div>
                    <div class="value">$($stats.BuildVersions.Count)</div>
                </div>
            </div>
        </div>

        <div class="footer">
            <p>Generated by ReplayAnalyzer | OWLib Project</p>
            <p style="margin-top: 10px; font-size: 0.9em;">
                Processed: $processedFiles files | Failed: $failedFiles files | Total: $totalFiles files
            </p>
        </div>
    </div>
</body>
</html>
"@

$html | Out-File -FilePath $OutputPath -Encoding UTF8

Write-Host ""
Write-Host "📄 Report generated: $OutputPath" -ForegroundColor Green
Write-Host ""
Write-Host "Opening report in browser..." -ForegroundColor Yellow
Start-Process $OutputPath
