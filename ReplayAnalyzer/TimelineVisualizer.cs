using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ReplayAnalyzerTool.Models;
using ReplayAnalyzerTool.GameData;

namespace ReplayAnalyzerTool;

/// <summary>
/// Generates interactive HTML visualizations of replay tick data.
/// Includes timeline view, entity tracking, event markers, and analysis tools.
/// </summary>
public class TimelineVisualizer
{
    public void GenerateTimeline(List<TickData> ticks, ReplaySegmentInfo info, string outputPath, EnhancedAnalysisResult? enhanced = null)
    {
        var html = GenerateHtmlTimeline(ticks, info, enhanced);
        File.WriteAllText(outputPath, html);
    }

    private string GenerateHtmlTimeline(List<TickData> ticks, ReplaySegmentInfo info, EnhancedAnalysisResult? enhanced = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Replay Timeline - " + Path.GetFileNameWithoutExtension(info.SourceFile) + @"</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: 'Segoe UI', system-ui, sans-serif;
            background: #1a1a2e;
            color: #eee;
            padding: 20px;
            line-height: 1.5;
        }
        .header {
            background: linear-gradient(135deg, #16213e, #0f3460);
            padding: 20px;
            border-radius: 10px;
            margin-bottom: 20px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.3);
        }
        .header h1 { color: #e94560; margin-bottom: 10px; }
        .meta-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 15px;
            margin-top: 15px;
        }
        .meta-item {
            background: rgba(255,255,255,0.05);
            padding: 10px 15px;
            border-radius: 5px;
            border-left: 3px solid #e94560;
        }
        .meta-item label { color: #888; font-size: 0.8em; display: block; }
        .meta-item span { font-size: 1.1em; font-weight: 500; }

        /* Main layout */
        .main-grid {
            display: grid;
            grid-template-columns: 1fr 350px;
            gap: 20px;
        }
        @media (max-width: 1200px) {
            .main-grid { grid-template-columns: 1fr; }
        }

        .panel {
            background: #16213e;
            border-radius: 10px;
            padding: 20px;
            margin-bottom: 20px;
        }
        .panel h2 {
            color: #e94560;
            margin-bottom: 15px;
            padding-bottom: 10px;
            border-bottom: 1px solid #0f3460;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        /* Timeline with event markers */
        .timeline-container {
            position: relative;
            height: 120px;
            background: #0f3460;
            border-radius: 5px;
            margin-bottom: 15px;
            overflow: hidden;
        }
        .timeline-track {
            position: absolute;
            top: 50%;
            left: 0;
            right: 0;
            height: 4px;
            background: #e94560;
            transform: translateY(-50%);
        }
        .event-marker {
            position: absolute;
            width: 16px;
            height: 16px;
            transform: translate(-50%, -50%);
            top: 50%;
            cursor: pointer;
            z-index: 10;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 10px;
            transition: all 0.2s;
            border: 2px solid rgba(255,255,255,0.3);
        }
        .event-marker:hover {
            transform: translate(-50%, -50%) scale(1.5);
            z-index: 20;
        }
        .event-marker.selected {
            transform: translate(-50%, -50%) scale(1.3);
            box-shadow: 0 0 10px currentColor;
        }
        .playhead {
            position: absolute;
            top: 0;
            bottom: 0;
            width: 2px;
            background: #fff;
            transform: translateX(-50%);
            z-index: 15;
            pointer-events: none;
        }
        .playhead::after {
            content: '';
            position: absolute;
            top: -5px;
            left: 50%;
            transform: translateX(-50%);
            border-left: 6px solid transparent;
            border-right: 6px solid transparent;
            border-top: 8px solid #fff;
        }

        /* Entity count visualization layer */
        .entity-layer {
            position: absolute;
            bottom: 0;
            left: 0;
            right: 0;
            height: 40px;
            pointer-events: none;
        }
        .entity-layer svg { width: 100%; height: 100%; }
        .entity-fill { fill: rgba(0, 217, 255, 0.2); }
        .entity-line { fill: none; stroke: #00d9ff; stroke-width: 1.5; }

        /* Scrubber */
        .scrubber {
            background: #0f3460;
            padding: 15px;
            border-radius: 5px;
            margin-bottom: 15px;
        }
        .scrubber input[type=""range""] {
            width: 100%;
            height: 8px;
            -webkit-appearance: none;
            background: #16213e;
            border-radius: 4px;
            outline: none;
        }
        .scrubber input[type=""range""]::-webkit-slider-thumb {
            -webkit-appearance: none;
            width: 20px;
            height: 20px;
            background: #e94560;
            border-radius: 50%;
            cursor: pointer;
        }
        .scrubber-info {
            display: flex;
            justify-content: space-between;
            margin-top: 10px;
            color: #888;
            font-size: 0.9em;
            flex-wrap: wrap;
            gap: 10px;
        }
        .scrubber-info strong { color: #fff; }

        /* Stats */
        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(100px, 1fr));
            gap: 10px;
        }
        .stat-card {
            background: #0f3460;
            padding: 12px;
            border-radius: 5px;
            text-align: center;
        }
        .stat-card .value {
            font-size: 1.5em;
            font-weight: bold;
            color: #e94560;
        }
        .stat-card .label {
            font-size: 0.75em;
            color: #888;
            margin-top: 3px;
        }

        /* Event panel */
        .event-list {
            max-height: 400px;
            overflow-y: auto;
        }
        .event-item {
            display: flex;
            align-items: center;
            gap: 10px;
            padding: 8px 10px;
            background: #0f3460;
            border-radius: 5px;
            margin-bottom: 5px;
            cursor: pointer;
            transition: all 0.2s;
        }
        .event-item:hover {
            background: #1a3a6e;
            transform: translateX(5px);
        }
        .event-item.active {
            background: #1a3a6e;
            border-left: 3px solid #e94560;
        }
        .event-icon {
            width: 28px;
            height: 28px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 14px;
            flex-shrink: 0;
        }
        .event-info {
            flex: 1;
            min-width: 0;
        }
        .event-type {
            font-weight: 500;
            font-size: 0.85em;
        }
        .event-detail {
            font-size: 0.75em;
            color: #888;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }
        .event-time {
            font-size: 0.7em;
            color: #666;
            flex-shrink: 0;
        }
        .event-divider {
            display: flex;
            align-items: center;
            margin: 15px 0;
            color: #e94560;
        }
        .event-divider span {
            padding: 0 10px;
            background: #16213e;
            font-size: 0.8em;
        }
        .event-divider::before, .event-divider::after {
            content: '';
            flex: 1;
            height: 1px;
            background: #0f3460;
        }
        .event-item.interaction {
            border-left: 3px solid #f39c12;
        }

        /* Players panel */
        .players-panel {
            margin-bottom: 20px;
        }
        .player-list {
            display: flex;
            flex-direction: column;
            gap: 8px;
        }
        .player-card {
            background: #0f3460;
            padding: 10px;
            border-radius: 5px;
            border-left: 3px solid #666;
        }
        .player-card.team-1 {
            border-left-color: #3498db;
        }
        .player-card.team-2 {
            border-left-color: #e74c3c;
        }
        .player-hero {
            font-weight: 600;
            color: #fff;
        }
        .player-name {
            font-size: 0.85em;
            color: #888;
        }
        .player-stats {
            display: flex;
            gap: 10px;
            margin-top: 5px;
            font-size: 0.8em;
        }
        .player-stats .stat {
            padding: 2px 6px;
            border-radius: 3px;
            background: rgba(255,255,255,0.1);
        }
        .player-stats .elims { color: #2ecc71; }
        .player-stats .deaths { color: #e74c3c; }
        .player-stats .ults { color: #f39c12; }

        /* Killfeed panel */
        .killfeed-panel {
            margin-top: 20px;
        }
        .killfeed-list {
            display: flex;
            flex-direction: column;
            gap: 4px;
        }
        .killfeed-item {
            display: flex;
            align-items: center;
            gap: 5px;
            padding: 6px 10px;
            background: #0f3460;
            border-radius: 4px;
            font-size: 0.8em;
            cursor: pointer;
        }
        .killfeed-item:hover {
            background: #1a3a6e;
        }
        .killfeed-item.critical {
            border-left: 3px solid #f39c12;
        }
        .killfeed-item .killer { color: #3498db; font-weight: 500; }
        .killfeed-item .victim { color: #e74c3c; font-weight: 500; }
        .killfeed-item .weapon { color: #888; font-size: 0.9em; }
        .killfeed-item .arrow { color: #666; }
        .killfeed-item .hero-icon { font-size: 0.9em; opacity: 0.7; }

        /* Entity panel */
        .entity-list {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
            gap: 8px;
        }
        .entity-card {
            background: #0f3460;
            padding: 10px;
            border-radius: 5px;
            border-left: 3px solid #00d9ff;
            font-size: 0.85em;
        }
        .entity-card.pov { border-left-color: #00ff00; }
        .entity-card.player { border-left-color: #e94560; }
        .entity-name {
            font-weight: 500;
            margin-bottom: 4px;
            display: flex;
            align-items: center;
            gap: 5px;
        }
        .entity-badge {
            font-size: 0.7em;
            padding: 2px 5px;
            border-radius: 3px;
            background: rgba(255,255,255,0.1);
        }
        .entity-pos {
            font-size: 0.75em;
            color: #888;
            font-family: 'Consolas', monospace;
        }

        /* Tracked entities summary */
        .tracked-list {
            max-height: 300px;
            overflow-y: auto;
        }
        .tracked-item {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 8px 10px;
            background: #0f3460;
            border-radius: 5px;
            margin-bottom: 5px;
        }
        .tracked-name { font-weight: 500; }
        .tracked-stats {
            font-size: 0.75em;
            color: #888;
        }
        .presence-bar {
            width: 60px;
            height: 6px;
            background: #16213e;
            border-radius: 3px;
            overflow: hidden;
        }
        .presence-fill {
            height: 100%;
            background: #00d9ff;
            border-radius: 3px;
        }

        /* Charts */
        .chart {
            width: 100%;
            height: 150px;
            background: #0f3460;
            border-radius: 5px;
            position: relative;
        }
        .chart svg { width: 100%; height: 100%; }
        .chart-line { fill: none; stroke: #e94560; stroke-width: 2; }
        .chart-area { fill: rgba(233, 69, 96, 0.2); }

        /* Hex viewer */
        .hex-view {
            font-family: 'Consolas', monospace;
            font-size: 0.7em;
            line-height: 1.6;
            background: #0a0a15;
            padding: 15px;
            border-radius: 5px;
            overflow-x: auto;
            white-space: pre;
            max-height: 200px;
        }
        .hex-offset { color: #666; }
        .hex-byte { color: #e94560; }
        .hex-byte.marker { color: #00ff00; background: rgba(0,255,0,0.1); }
        .hex-ascii { color: #00d9ff; }

        /* Keyboard shortcut hints */
        .shortcuts {
            display: flex;
            gap: 15px;
            font-size: 0.75em;
            color: #666;
            margin-top: 10px;
        }
        .shortcuts kbd {
            background: #0f3460;
            padding: 2px 6px;
            border-radius: 3px;
            margin-right: 3px;
        }

        /* Tabs */
        .tabs {
            display: flex;
            gap: 5px;
            margin-bottom: 15px;
        }
        .tab {
            padding: 8px 16px;
            background: #0f3460;
            border: none;
            color: #888;
            cursor: pointer;
            border-radius: 5px 5px 0 0;
            transition: all 0.2s;
        }
        .tab.active {
            background: #1a3a6e;
            color: #fff;
        }
        .tab-content { display: none; }
        .tab-content.active { display: block; }

        /* Event type filter */
        .filter-row {
            display: flex;
            gap: 5px;
            flex-wrap: wrap;
            margin-bottom: 10px;
        }
        .filter-btn {
            padding: 4px 10px;
            font-size: 0.75em;
            background: #0f3460;
            border: none;
            color: #888;
            cursor: pointer;
            border-radius: 3px;
            transition: all 0.2s;
        }
        .filter-btn.active {
            color: #fff;
            background: #1a3a6e;
        }
    </style>
</head>
<body>
    <div class=""header"">
        <h1>🎮 Replay Timeline Viewer</h1>
        <p>Interactive visualization of Overwatch replay tick data</p>
        <div class=""meta-grid"">
            <div class=""meta-item"">
                <label>Source File</label>
                <span>" + Path.GetFileName(info.SourceFile) + @"</span>
            </div>
            <div class=""meta-item"">
                <label>Map GUID</label>
                <span>" + info.MapGuid + @"</span>
            </div>
            <div class=""meta-item"">
                <label>Game Mode</label>
                <span>" + info.GameModeGuid + @"</span>
            </div>
            <div class=""meta-item"">
                <label>Build</label>
                <span>" + info.BuildNumber + @" (v" + info.FormatVersion + @")</span>
            </div>
            <div class=""meta-item"">
                <label>Duration</label>
                <span>" + info.TotalDuration.ToString("F2") + @"s (" + ticks.Count + @" ticks)</span>
            </div>
            <div class=""meta-item"">
                <label>Events Detected</label>
                <span>" + info.TotalEvents + @"</span>
            </div>
        </div>
    </div>

    <div class=""main-grid"">
        <div class=""left-col"">
            <!-- Timeline with events -->
            <div class=""panel"">
                <h2>⏱️ Timeline</h2>
                <div class=""timeline-container"" id=""timeline"">
                    <div class=""entity-layer"">
                        " + GenerateEntitySvg(ticks) + @"
                    </div>
                    <div class=""timeline-track""></div>
                    <div class=""playhead"" id=""playhead""></div>
                    " + GenerateEventMarkers(ticks) + @"
                </div>
                <div class=""scrubber"">
                    <input type=""range"" id=""tickSlider"" min=""0"" max=""" + Math.Max(0, ticks.Count - 1) + @""" value=""0"">
                    <div class=""scrubber-info"">
                        <span>Tick: <strong id=""currentTick"">" + (ticks.Count > 0 ? ticks[0].TickNumber.ToString() : "0") + @"</strong></span>
                        <span>Time: <strong id=""currentTime"">0.00s</strong></span>
                        <span>Entities: <strong id=""currentEntities"">" + (ticks.Count > 0 ? ticks[0].Entities.Count.ToString() : "0") + @"</strong></span>
                        <span>Events: <strong id=""currentEvents"">0</strong></span>
                    </div>
                    <div class=""shortcuts"">
                        <span><kbd>←</kbd><kbd>→</kbd> Navigate</span>
                        <span><kbd>Space</kbd> Play/Pause</span>
                        <span><kbd>E</kbd> Next Event</span>
                    </div>
                </div>
            </div>

            <!-- Stats -->
            <div class=""panel"">
                <h2>📊 Statistics</h2>
                <div class=""stats-grid"">
                    <div class=""stat-card"">
                        <div class=""value"">" + ticks.Count + @"</div>
                        <div class=""label"">Total Ticks</div>
                    </div>
                    <div class=""stat-card"">
                        <div class=""value"">" + (ticks.Count > 0 ? (ticks.Average(t => t.DeltaTime) * 1000).ToString("F1") : "0") + @"</div>
                        <div class=""label"">Avg ms/tick</div>
                    </div>
                    <div class=""stat-card"">
                        <div class=""value"">" + info.MinEntityCount + @"-" + info.MaxEntityCount + @"</div>
                        <div class=""label"">Entity Range</div>
                    </div>
                    <div class=""stat-card"">
                        <div class=""value"">" + (ticks.Count > 0 ? (1.0f / Math.Max(0.001f, ticks.Average(t => t.DeltaTime))).ToString("F0") : "0") + @"</div>
                        <div class=""label"">~FPS</div>
                    </div>
                    <div class=""stat-card"">
                        <div class=""value"">" + info.TotalEvents + @"</div>
                        <div class=""label"">Events</div>
                    </div>
                    <div class=""stat-card"">
                        <div class=""value"">" + info.TrackedEntities.Count + @"</div>
                        <div class=""label"">Tracked</div>
                    </div>
                </div>
            </div>

            <!-- Tabbed content -->
            <div class=""panel"">
                <div class=""tabs"">
                    <button class=""tab active"" data-tab=""entities"">Entities</button>
                    <button class=""tab"" data-tab=""tracked"">Tracked Summary</button>
                    <button class=""tab"" data-tab=""hex"">Raw Data</button>
                    <button class=""tab"" data-tab=""charts"">Charts</button>
                </div>

                <div class=""tab-content active"" id=""tab-entities"">
                    <div id=""entityList"" class=""entity-list""></div>
                </div>

                <div class=""tab-content"" id=""tab-tracked"">
                    <div class=""tracked-list"">
                        " + GenerateTrackedEntitiesList(info) + @"
                    </div>
                </div>

                <div class=""tab-content"" id=""tab-hex"">
                    <div id=""hexView"" class=""hex-view""></div>
                </div>

                <div class=""tab-content"" id=""tab-charts"">
                    <h3 style=""color: #888; margin-bottom: 10px; font-size: 0.9em;"">Entity Count Over Time</h3>
                    <div class=""chart"">
                        <svg viewBox=""0 0 1000 150"" preserveAspectRatio=""none"">
                            " + GenerateChartPath(ticks) + @"
                        </svg>
                    </div>
                    <h3 style=""color: #888; margin: 15px 0 10px; font-size: 0.9em;"">Delta Time Variance</h3>
                    <div class=""chart"">
                        <svg viewBox=""0 0 1000 150"" preserveAspectRatio=""none"">
                            " + GenerateDeltaTimeChart(ticks) + @"
                        </svg>
                    </div>
                </div>
            </div>
        </div>

        <!-- Right sidebar: Events -->
        <div class=""right-col"">
            " + GeneratePlayersPanel(enhanced) + @"
            <div class=""panel"">
                <h2>⚡ Events (" + info.TotalEvents + @")</h2>
                <div class=""filter-row"">
                    <button class=""filter-btn active"" data-filter=""all"">All</button>
                    " + GenerateEventFilterButtons(ticks) + @"
                </div>
                <div class=""event-list"" id=""eventList"">
                    " + GenerateEventList(ticks, enhanced) + @"
                </div>
            </div>
            " + GenerateKillfeedPanel(enhanced) + @"
        </div>
    </div>

    <script>
        const tickData = " + GenerateTickDataJson(ticks, enhanced) + @";
        const events = " + GenerateEventsJson(ticks) + @";
        const enhanced = " + (enhanced != null ? "true" : "false") + @";
        const interactions = " + GenerateInteractionsJson(enhanced) + @";

        let currentIndex = 0;
        let isPlaying = false;
        let playInterval = null;
        let activeFilter = 'all';

        // Elements
        const slider = document.getElementById('tickSlider');
        const playhead = document.getElementById('playhead');
        const timeline = document.getElementById('timeline');
        const currentTickEl = document.getElementById('currentTick');
        const currentTimeEl = document.getElementById('currentTime');
        const currentEntitiesEl = document.getElementById('currentEntities');
        const currentEventsEl = document.getElementById('currentEvents');
        const entityListEl = document.getElementById('entityList');
        const hexViewEl = document.getElementById('hexView');
        const eventListEl = document.getElementById('eventList');

        function updateDisplay(index) {
            if (index < 0 || index >= tickData.length) return;
            currentIndex = index;
            const tick = tickData[index];

            // Update info
            currentTickEl.textContent = tick.tickNumber;
            currentTimeEl.textContent = tick.cumulativeTime.toFixed(3) + 's';
            currentEntitiesEl.textContent = tick.entityCount;
            currentEventsEl.textContent = tick.events.length;

            // Update playhead position
            const pct = index / Math.max(1, tickData.length - 1) * 100;
            playhead.style.left = pct + '%';
            slider.value = index;

            // Update entity list
            updateEntityList(tick);

            // Update hex view
            hexViewEl.textContent = formatHex(tick.rawPreview);

            // Highlight active events
            highlightEvents(tick.tickNumber);
        }

        function updateEntityList(tick) {
            entityListEl.innerHTML = '';
            for (const entity of tick.entities) {
                const card = document.createElement('div');
                card.className = 'entity-card';
                if (entity.type === 'PlayerPOV') card.className += ' pov';
                else if (entity.type === 'Player') card.className += ' player';

                let html = `<div class=""entity-name"">
                    ${entity.name}
                    <span class=""entity-badge"">${entity.type}</span>
                </div>`;
                if (entity.position) {
                    html += `<div class=""entity-pos"">(${entity.position.x.toFixed(1)}, ${entity.position.y.toFixed(1)}, ${entity.position.z.toFixed(1)})</div>`;
                }
                card.innerHTML = html;
                entityListEl.appendChild(card);
            }
        }

        function formatHex(hex) {
            if (!hex) return '';
            let result = '';
            const markerHex = '847A0700'; // 0x00077A84 little-endian
            for (let i = 0; i < hex.length; i += 32) {
                const offset = (i / 2).toString(16).padStart(4, '0');
                const chunk = hex.substring(i, i + 32);
                const bytes = chunk.match(/.{2}/g) || [];
                const ascii = bytes.map(b => {
                    const c = parseInt(b, 16);
                    return (c >= 32 && c < 127) ? String.fromCharCode(c) : '.';
                }).join('');

                // Highlight marker bytes
                let byteStr = '';
                for (let j = 0; j < bytes.length; j++) {
                    const pos = i + j * 2;
                    const isMarker = hex.indexOf(markerHex) === pos - (pos % 8) + (4*2);
                    byteStr += bytes[j] + ' ';
                }
                result += offset + '  ' + byteStr.padEnd(48) + '  ' + ascii + '\n';
            }
            return result;
        }

        function highlightEvents(tickNum) {
            document.querySelectorAll('.event-item').forEach(el => {
                el.classList.toggle('active', parseInt(el.dataset.tick) === tickNum);
            });
            document.querySelectorAll('.event-marker').forEach(el => {
                el.classList.toggle('selected', parseInt(el.dataset.tick) === tickNum);
            });
        }

        function jumpToEvent(tickNum) {
            const idx = tickData.findIndex(t => t.tickNumber === tickNum);
            if (idx >= 0) updateDisplay(idx);
        }

        function nextEvent() {
            const currentTick = tickData[currentIndex].tickNumber;
            const nextEvt = events.find(e => e.tickNumber > currentTick);
            if (nextEvt) jumpToEvent(nextEvt.tickNumber);
        }

        function prevEvent() {
            const currentTick = tickData[currentIndex].tickNumber;
            const prevEvts = events.filter(e => e.tickNumber < currentTick);
            if (prevEvts.length > 0) jumpToEvent(prevEvts[prevEvts.length - 1].tickNumber);
        }

        function togglePlay() {
            isPlaying = !isPlaying;
            if (isPlaying) {
                playInterval = setInterval(() => {
                    if (currentIndex < tickData.length - 1) {
                        updateDisplay(currentIndex + 1);
                    } else {
                        togglePlay();
                    }
                }, 16);
            } else {
                clearInterval(playInterval);
            }
        }

        function filterEvents(filter) {
            activeFilter = filter;
            document.querySelectorAll('.filter-btn').forEach(b => {
                b.classList.toggle('active', b.dataset.filter === filter);
            });
            document.querySelectorAll('.event-item').forEach(el => {
                if (filter === 'all' || el.dataset.type === filter) {
                    el.style.display = '';
                } else {
                    el.style.display = 'none';
                }
            });
            document.querySelectorAll('.event-marker').forEach(el => {
                if (filter === 'all' || el.dataset.type === filter) {
                    el.style.display = '';
                } else {
                    el.style.display = 'none';
                }
            });
        }

        // Event listeners
        slider.addEventListener('input', (e) => updateDisplay(parseInt(e.target.value)));

        timeline.addEventListener('click', (e) => {
            if (e.target.closest('.event-marker')) return;
            const rect = timeline.getBoundingClientRect();
            const pct = (e.clientX - rect.left) / rect.width;
            const idx = Math.round(pct * (tickData.length - 1));
            updateDisplay(idx);
        });

        document.querySelectorAll('.event-marker').forEach(el => {
            el.addEventListener('click', () => jumpToEvent(parseInt(el.dataset.tick)));
        });

        document.querySelectorAll('.event-item').forEach(el => {
            el.addEventListener('click', () => jumpToEvent(parseInt(el.dataset.tick)));
        });

        document.querySelectorAll('.filter-btn').forEach(btn => {
            btn.addEventListener('click', () => filterEvents(btn.dataset.filter));
        });

        // Tabs
        document.querySelectorAll('.tab').forEach(tab => {
            tab.addEventListener('click', () => {
                document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
                document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
                tab.classList.add('active');
                document.getElementById('tab-' + tab.dataset.tab).classList.add('active');
            });
        });

        // Keyboard controls
        document.addEventListener('keydown', (e) => {
            if (e.key === 'ArrowLeft') {
                updateDisplay(Math.max(0, currentIndex - 1));
            } else if (e.key === 'ArrowRight') {
                updateDisplay(Math.min(tickData.length - 1, currentIndex + 1));
            } else if (e.key === ' ') {
                e.preventDefault();
                togglePlay();
            } else if (e.key.toLowerCase() === 'e') {
                nextEvent();
            } else if (e.key.toLowerCase() === 'q') {
                prevEvent();
            }
        });

        // Initialize
        updateDisplay(0);
    </script>
</body>
</html>");

        return sb.ToString();
    }

    private string GenerateEventMarkers(List<TickData> ticks)
    {
        if (ticks.Count == 0) return "";

        var sb = new StringBuilder();
        var allEvents = ticks.SelectMany(t => t.Events.Select(e => new { Tick = t, Event = e })).ToList();

        foreach (var item in allEvents)
        {
            float pct = ticks.IndexOf(item.Tick) / (float)Math.Max(1, ticks.Count - 1) * 100;
            var evt = item.Event;

            sb.AppendLine($@"<div class=""event-marker"" style=""left: {pct:F2}%; background: {evt.Color}; color: #fff;""
                data-tick=""{item.Tick.TickNumber}"" data-type=""{evt.Type}""
                title=""{evt.Type}: {evt.Detail?.Replace("\"", "&quot;")}"">
                {evt.Icon}
            </div>");
        }

        return sb.ToString();
    }

    private string GenerateEventList(List<TickData> ticks, EnhancedAnalysisResult? enhanced = null)
    {
        var sb = new StringBuilder();
        var allEvents = ticks.SelectMany(t => t.Events.Select(e => new { Tick = t, Event = e })).ToList();

        foreach (var item in allEvents)
        {
            var evt = item.Event;
            sb.AppendLine($@"<div class=""event-item"" data-tick=""{item.Tick.TickNumber}"" data-type=""{evt.Type}"">
                <div class=""event-icon"" style=""background: {evt.Color}"">{evt.Icon}</div>
                <div class=""event-info"">
                    <div class=""event-type"">{evt.Type}</div>
                    <div class=""event-detail"">{evt.Detail ?? ""}</div>
                </div>
                <div class=""event-time"">{item.Tick.CumulativeTime:F2}s</div>
            </div>");
        }

        // Add enhanced interactions if available
        if (enhanced != null)
        {
            sb.AppendLine(@"<div class=""event-divider""><span>Game Interactions</span></div>");
            foreach (var interaction in enhanced.GameState.Interactions.Take(100))
            {
                sb.AppendLine($@"<div class=""event-item interaction"" data-tick=""{interaction.Tick}"" data-type=""{interaction.Type}"">
                    <div class=""event-icon"" style=""background: {interaction.Color}"">{interaction.Icon}</div>
                    <div class=""event-info"">
                        <div class=""event-type"">{interaction.Type}</div>
                        <div class=""event-detail"">{interaction.Description}</div>
                    </div>
                    <div class=""event-time"">{interaction.Time:F2}s</div>
                </div>");
            }
        }

        return sb.ToString();
    }

    private string GenerateEventFilterButtons(List<TickData> ticks)
    {
        var types = ticks.SelectMany(t => t.Events)
                         .Select(e => e.Type)
                         .Distinct()
                         .OrderBy(t => t.ToString())
                         .ToList();

        var sb = new StringBuilder();
        foreach (var type in types)
        {
            sb.AppendLine($@"<button class=""filter-btn"" data-filter=""{type}"">{type}</button>");
        }
        return sb.ToString();
    }

    private string GenerateEntitySvg(List<TickData> ticks)
    {
        if (ticks.Count == 0) return "<svg viewBox=\"0 0 100 40\"></svg>";

        int maxEntities = ticks.Max(t => t.Entities.Count);
        int minEntities = ticks.Min(t => t.Entities.Count);
        int range = Math.Max(1, maxEntities - minEntities);

        var points = new List<string>();
        for (int i = 0; i < ticks.Count; i++)
        {
            float x = (float)i / Math.Max(1, ticks.Count - 1) * 100;
            float y = 38 - ((float)(ticks[i].Entities.Count - minEntities) / range * 35);
            points.Add($"{x:F2},{y:F2}");
        }

        var areaPoints = new List<string>(points);
        areaPoints.Add("100,40");
        areaPoints.Add("0,40");

        return $@"<svg viewBox=""0 0 100 40"" preserveAspectRatio=""none"">
            <path class=""entity-fill"" d=""M {string.Join(" L ", areaPoints)} Z""/>
            <path class=""entity-line"" d=""M {string.Join(" L ", points)}""/>
        </svg>";
    }

    private string GenerateTrackedEntitiesList(ReplaySegmentInfo info)
    {
        var sb = new StringBuilder();
        foreach (var entity in info.TrackedEntities)
        {
            sb.AppendLine($@"<div class=""tracked-item"">
                <div>
                    <div class=""tracked-name"">{entity.Name}</div>
                    <div class=""tracked-stats"">Type: {entity.Type} | Ticks: {entity.TicksPresent} | Positions: {entity.PositionSamples}</div>
                </div>
                <div>
                    <div class=""presence-bar"">
                        <div class=""presence-fill"" style=""width: {Math.Min(100, entity.PresenceRatio):F0}%""></div>
                    </div>
                    <div class=""tracked-stats"">{entity.PresenceRatio:F0}%</div>
                </div>
            </div>");
        }
        return sb.ToString();
    }

    private string GenerateChartPath(List<TickData> ticks)
    {
        if (ticks.Count == 0) return "";

        int maxEntities = ticks.Max(t => t.Entities.Count);
        int minEntities = ticks.Min(t => t.Entities.Count);
        int range = Math.Max(1, maxEntities - minEntities);

        var points = new List<string>();
        var areaPoints = new List<string>();

        for (int i = 0; i < ticks.Count; i++)
        {
            float x = (float)i / Math.Max(1, ticks.Count - 1) * 1000;
            float y = 140 - ((float)(ticks[i].Entities.Count - minEntities) / range * 120);
            points.Add($"{x:F1},{y:F1}");
            areaPoints.Add($"{x:F1},{y:F1}");
        }

        areaPoints.Add("1000,140");
        areaPoints.Add("0,140");

        return $@"<defs>
            <linearGradient id=""areaGrad"" x1=""0"" y1=""0"" x2=""0"" y2=""1"">
                <stop offset=""0%"" stop-color=""#e94560"" stop-opacity=""0.3""/>
                <stop offset=""100%"" stop-color=""#e94560"" stop-opacity=""0""/>
            </linearGradient>
        </defs>
        <path class=""chart-area"" d=""M {string.Join(" L ", areaPoints)} Z"" fill=""url(#areaGrad)""/>
        <path class=""chart-line"" d=""M {string.Join(" L ", points)}""/>";
    }

    private string GenerateDeltaTimeChart(List<TickData> ticks)
    {
        if (ticks.Count == 0) return "";

        float maxDt = ticks.Max(t => t.DeltaTime);
        float minDt = ticks.Min(t => t.DeltaTime);
        float range = Math.Max(0.001f, maxDt - minDt);

        var points = new List<string>();

        for (int i = 0; i < ticks.Count; i++)
        {
            float x = (float)i / Math.Max(1, ticks.Count - 1) * 1000;
            float y = 140 - ((ticks[i].DeltaTime - minDt) / range * 120);
            points.Add($"{x:F1},{y:F1}");
        }

        float refY = 140 - ((0.01667f - minDt) / range * 120);

        return $@"<line x1=""0"" y1=""{refY:F1}"" x2=""1000"" y2=""{refY:F1}"" stroke=""#00d9ff"" stroke-width=""1"" stroke-dasharray=""5,5"" opacity=""0.5""/>
        <path class=""chart-line"" d=""M {string.Join(" L ", points)}"" stroke=""#00d9ff""/>";
    }

    private string GenerateTickDataJson(List<TickData> ticks, EnhancedAnalysisResult? enhanced = null)
    {
        var items = ticks.Select(t => new
        {
            tickNumber = t.TickNumber,
            deltaTime = t.DeltaTime,
            cumulativeTime = t.CumulativeTime,
            entityCount = t.Entities.Count,
            rawPreview = t.RawDataPreview,
            events = t.Events.Select(e => new { type = e.Type.ToString(), detail = e.Detail }).ToList(),
            entities = t.Entities.Select(e => new
            {
                index = e.Index,
                name = e.Name,
                type = e.Type.ToString(),
                position = e.Position.HasValue ? new { x = e.Position.Value.X, y = e.Position.Value.Y, z = e.Position.Value.Z } : null
            }).ToList()
        });

        return JsonSerializer.Serialize(items);
    }

    private string GenerateEventsJson(List<TickData> ticks)
    {
        var events = ticks.SelectMany(t => t.Events.Select(e => new
        {
            tickNumber = t.TickNumber,
            time = t.CumulativeTime,
            type = e.Type.ToString(),
            detail = e.Detail
        })).ToList();

        return JsonSerializer.Serialize(events);
    }

    private string GeneratePlayersPanel(EnhancedAnalysisResult? enhanced)
    {
        if (enhanced == null) return "";

        var sb = new StringBuilder();
        sb.AppendLine(@"<div class=""panel players-panel"">");
        sb.AppendLine($@"<h2>👥 Players ({enhanced.GameState.Players.Count})</h2>");
        sb.AppendLine(@"<div class=""player-list"">");

        foreach (var player in enhanced.GameState.Players.OrderBy(p => p.Team).ThenBy(p => p.PlayerId))
        {
            var teamClass = player.Team == 1 ? "team-1" : "team-2";
            var heroName = player.CurrentHero?.Name ?? "Unknown";
            var roleIcon = player.RoleIcon;

            sb.AppendLine($@"<div class=""player-card {teamClass}"">
                <div class=""player-hero"">{roleIcon} {heroName}</div>
                <div class=""player-name"">{player.PlayerName}</div>
                <div class=""player-stats"">
                    <span class=""stat elims"">{player.Eliminations} E</span>
                    <span class=""stat deaths"">{player.Deaths} D</span>
                    <span class=""stat ults"">{player.UltimatesUsed} Q</span>
                </div>
            </div>");
        }

        sb.AppendLine(@"</div></div>");
        return sb.ToString();
    }

    private string GenerateKillfeedPanel(EnhancedAnalysisResult? enhanced)
    {
        if (enhanced == null || enhanced.GameState.Killfeed.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine(@"<div class=""panel killfeed-panel"">");
        sb.AppendLine($@"<h2>💀 Killfeed ({enhanced.GameState.Killfeed.Count})</h2>");
        sb.AppendLine(@"<div class=""killfeed-list"">");

        foreach (var kill in enhanced.GameState.Killfeed.TakeLast(20).Reverse())
        {
            var critClass = kill.IsCritical ? "critical" : "";
            sb.AppendLine($@"<div class=""killfeed-item {critClass}"" data-tick=""{kill.Tick}"">
                <span class=""killer"">{kill.KillerName}</span>
                <span class=""hero-icon"">{kill.KillerHero ?? ""}</span>
                <span class=""weapon"">{kill.Weapon}</span>
                <span class=""arrow"">→</span>
                <span class=""victim"">{kill.VictimName}</span>
                <span class=""hero-icon"">{kill.VictimHero ?? ""}</span>
            </div>");
        }

        sb.AppendLine(@"</div></div>");
        return sb.ToString();
    }

    private string GenerateInteractionsJson(EnhancedAnalysisResult? enhanced)
    {
        if (enhanced == null) return "[]";

        var interactions = enhanced.GameState.Interactions.Select(i => new
        {
            tick = i.Tick,
            time = i.Time,
            type = i.Type.ToString(),
            source = i.SourcePlayer?.DisplayName,
            target = i.TargetPlayer?.DisplayName,
            ability = i.AbilityName,
            description = i.Description,
            icon = i.Icon,
            color = i.Color
        }).ToList();

        return JsonSerializer.Serialize(interactions);
    }
}
