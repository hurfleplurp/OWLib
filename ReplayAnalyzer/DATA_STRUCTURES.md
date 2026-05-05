# Overwatch Replay Data Structures

## Overview
This document describes the data structures we've decoded from Overwatch highlight/replay files and our standardized format for machine learning.

## File Format

### Player Highlight File (`.phl`)
- **Magic**: `phl` (0x70686C)
- **Format Version**: Byte at offset 3
- **Contains**: Compressed replay segment + metadata

### Replay Data (`.prp`)
- **Magic**: `prp` (0x707270)
- **Format Version**: Byte at offset 3
- **Build Number**: uint32 at offset 4
- **Compression**: Zstandard (magic: 0xFD2FB528)
- **Contains**: Frame-by-frame game state

---

## Decompressed Frame Structure

### Frame Header (12 bytes)
```
Offset | Type   | Description
-------|--------|----------------------------
0x00   | uint32 | Frame Number (tick number)
0x04   | uint32 | Payload Length (bytes)
0x08   | uint32 | Entity Count
0x0C   | ...    | Frame Payload (variable)
```

### Frame Payload
```
Offset | Type   | Description
-------|--------|----------------------------
0x00   | float  | Delta Time (seconds since last frame)
0x04   | uint32 | Marker (always 0x00077A84)
0x08   | ...    | Entity Data (bit-packed, variable)
```

### Inter-Frame Structure
- **Padding**: 8 bytes between consecutive frames
- **Tick Rate**: ~60 fps (16.67ms average delta time)
- **Entity Data**: Bit-packed, not byte-aligned

---

## Standardized Tick Data Format

Our parser converts raw binary frames into a standardized JSON/CSV format for ML:

### TickData Model
```csharp
{
    "TickNumber": 10387,           // Absolute tick number
    "DeltaTime": 0.0168,            // Time since last tick (seconds)
    "CumulativeTime": 5.234,        // Total time from segment start
    "Entities": [                   // Array of entity states
        {
            "Index": 0,             // Entity index in this tick
            "Type": "Player",       // Entity type (if identifiable)
            "Team": 1,              // Team (0=spectator, 1/2=teams)
            "Position": {           // World position (if decoded)
                "X": 123.45,
                "Y": 67.89,
                "Z": 10.12
            },
            "Health": 0.75,         // Health percentage (0-1)
            "UltCharge": 0.42,      // Ultimate charge (0-1)
            // ... more fields as we decode them
        }
    ],
    "Events": [                     // Game events this tick
        {
            "Type": "Kill",
            "SourceEntityIndex": 2,
            "TargetEntityIndex": 5,
            "Detail": "Headshot"
        }
    ]
}
```

### CSV Export Format
For efficient analysis, we export:
```csv
TickNumber,DeltaTime,CumulativeTime,EntityCount,RawDataSize,RawDataPreview
10387,0.016800,0.168000,22,342,60 8F 89 3C 84 7A 07 00 00 29 07 00...
10388,0.015234,0.183234,24,451,0798793C847A070000CAC12720008905...
```

---

## Observations & Findings

### What We Know
✅ **Frame Timing**: Consistent ~60fps tick rate (16.67ms ±2ms variance)
✅ **Frame Structure**: 12-byte header + variable payload with 0x00077A84 marker
✅ **Entity Counts**: Range from 15-30+ entities per tick
✅ **Compression**: Zstandard compression on full replay data
✅ **Segments**: Each highlight file is a ~10-18 second segment from a match

### What We're Working On
🔄 **Entity Bit-Packing**: The entity data uses custom bit-packing (not byte-aligned)
🔄 **Position Encoding**: Some float triplets look like positions but need validation
🔄 **Event Detection**: Identifying kills, ults, objectives from state changes
🔄 **Entity Tracking**: Matching entities across frames to track individual players/objects

### Known Limitations
⚠️ **No Full Matches**: Highlight files are fragments, not complete matches
⚠️ **No Timestamps**: We don't know where in the match each segment occurs
⚠️ **Limited Metadata**: Player names/heroes often missing from header
⚠️ **Bit Format Unknown**: Entity data bit-packing scheme is proprietary

---

## Data Quality Assessment

### Segment Statistics
From our corpus analysis:
- **Total Segments**: 50+ unique highlight files
- **Average Duration**: 14-18 seconds per segment
- **Average Tick Count**: 900-1100 ticks per segment
- **Build Versions**: 98702 - 144835 (multiple game patches)

### Use Cases

#### ✅ Can Do Now
- Train models on **entity positioning patterns**
- Analyze **team compositions** (entity count distribution)
- Study **micro-level gameplay** (16ms granularity)
- Build **action detection** from state changes
- Create **replay visualizers** (timeline view)

#### ❌ Need Full Match Data For
- **Round-based learning** (round start/end markers)
- **Objective tracking** (payload progress, capture points)
- **Match outcome prediction** (win/loss labels)
- **Player statistics** (full match K/D/A, damage, healing)
- **Map strategy** (spawn locations, ultimate economy)

---

## Next Steps

### 1. Improve Entity Decoding
- **Bit-pattern analysis**: Study entity data across multiple frames
- **Coordinate validation**: Verify position data with map bounds
- **Type identification**: Distinguish players from projectiles/objects

### 2. Event Extraction
- **State diff analysis**: Compare consecutive ticks to detect events
- **Kill detection**: Entity disappearance + count changes
- **Ultimate usage**: Look for large state flag changes
- **Ability detection**: Pattern recognition on bit flags

### 3. Visualization Enhancements
- **2D map overlay**: Plot positions on actual map layouts
- **Entity tracking**: Color-code entities by team/type
- **Event markers**: Visual indicators for detected events
- **Playback controls**: Play/pause/scrub through tick data

### 4. Finding Full Matches
Priority actions to get complete match replay data:
- **Blizzard API**: Check if official replay API exists (competitive matches)
- **Local game files**: Scan for full replay storage locations
- **OWL/Contenders**: Official league replay files (if publicly available)
- **Community tools**: Research existing replay parsers/databases

---

## ML Training Considerations

### Current Capabilities
With our segment data, we can train models for:
- **Action recognition** (player movements, ability usage)
- **Position prediction** (where players will move next)
- **Engagement detection** (teamfight start/end)
- **Anomaly detection** (unusual plays, highlights)

### What We Need Full Matches For
- **Win probability** estimation
- **Round outcome** prediction
- **Ultimate economy** optimization
- **Map strategy** learning
- **Role performance** evaluation

### Recommended Approach
1. **Phase 1** (Current): Build robust entity decoder and visualization tools
2. **Phase 2**: Train preliminary models on segment data (action recognition)
3. **Phase 3**: Acquire full match data source
4. **Phase 4**: Transfer learning from segment models to full match models

---

## Tools & Outputs

### ReplayAnalyzer
**Usage:**
```bash
# Basic analysis
dotnet run --project ReplayAnalyzer ./Highlights

# With timeline visualization
dotnet run --project ReplayAnalyzer --timeline ./Highlights

# Full JSON export
dotnet run --project ReplayAnalyzer --timeline --json ./Highlights

# Verbose output
dotnet run --project ReplayAnalyzer -t -j -v ./path/to/file
```

**Outputs:**
- `*.frames.csv`: Tick data in CSV format
- `*.summary.json`: Segment metadata (map, players, duration)
- `*.ticks.json`: Full tick data with entities (if `--json`)
- `*.timeline.html`: Interactive visualization (if `--timeline`)
- `*.raw`: Decompressed binary data (for debugging)

### Timeline Viewer
Interactive HTML visualization featuring:
- **Timeline scrubber** with keyboard navigation (arrow keys)
- **Entity count graph** over time
- **Delta time variance** chart (tick stability)
- **Hex viewer** for raw frame data inspection
- **Metadata display** (map, mode, build, players)

---

## References

### Repository Structure
```
OWLib/
├── ReplayAnalyzer/          # Main analysis tool
│   ├── Program.cs           # Entry point & legacy analysis
│   ├── FrameParser.cs       # Binary frame → TickData conversion
│   ├── TimelineVisualizer.cs # HTML generation
│   └── Models/
│       └── TickData.cs      # Standardized data models
├── TankLib/                 # Binary parsing library
│   └── Replay/
│       └── tePlayerHighlight.cs
└── DataTool/                # Asset extraction tool
```

### Key Files
- `TickData.cs`: ML-ready data model definitions
- `FrameParser.cs`: Binary parsing logic
- `TimelineVisualizer.cs`: Visualization generation
- `Program.cs`: CLI interface and file handling

---

## Contributing

When adding new decodings or features:
1. Update `TickData.cs` model with new fields
2. Implement parsing in `FrameParser.cs`
3. Add visualization to `TimelineVisualizer.cs`
4. Document findings in this file
5. Add unit tests for new decodings

---

**Last Updated**: 2026-01-07
**Decoded By**: Replay Analysis Team
**Status**: Active Development 🚧
