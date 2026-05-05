# Overwatch Replay Analyzer

A comprehensive tool for parsing, analyzing, and visualizing Overwatch replay/highlight data. Extracts tick-by-tick game state for machine learning and game analysis.

## Features

- 🎮 **Binary Parsing**: Decodes Overwatch highlight files (`.phl`) and replay data (`.prp`)
- 📊 **Frame Analysis**: Extracts ~60fps tick data with entity states
- 🎬 **Timeline Visualization**: Interactive HTML viewer with scrubbing and charts
- 📦 **ML-Ready Export**: Standardized JSON/CSV formats for machine learning
- 🔍 **Hex Debugging**: Raw data inspection and bit-level analysis

## Quick Start

```bash
# Build the project
dotnet build ReplayAnalyzer/ReplayAnalyzer.csproj

# Analyze highlights with timeline generation
dotnet run --project ReplayAnalyzer --timeline "./path/to/Highlights"

# Full analysis with JSON export
dotnet run --project ReplayAnalyzer -t -j -v "./path/to/file"
```

## Command Line Options

```
Usage: ReplayAnalyzer [options] <path>

Options:
  -t, --timeline    Generate interactive HTML timeline viewer
  -j, --json        Export full tick data as JSON
  -v, --verbose     Show detailed parsing information

Examples:
  ReplayAnalyzer ./Highlights
  ReplayAnalyzer --timeline "C:\Users\...\Overwatch\123456\Highlights"
  ReplayAnalyzer -t -j ./highlight-file
```

## Output Files

For each analyzed highlight, the tool generates:

| File | Description |
|------|-------------|
| `*.frames.csv` | Tick data in CSV format (tick#, deltaTime, entities, etc.) |
| `*.summary.json` | Metadata (map, gamemode, players, duration) |
| `*.ticks.json` | Full tick data with entity details (if `--json`) |
| `*.timeline.html` | Interactive visualization (if `--timeline`) |
| `*.raw` | Decompressed binary data for debugging |

## Timeline Viewer

The interactive HTML timeline provides:

- **Scrubber Control**: Navigate frame-by-frame with slider or arrow keys
- **Entity Graph**: Visualize entity count changes over time
- **Delta Time Chart**: Monitor tick timing stability
- **Hex Viewer**: Inspect raw frame data
- **Statistics**: Duration, tick count, FPS, entity ranges

![Timeline Example](https://via.placeholder.com/800x400?text=Timeline+Viewer+Screenshot)

## Data Format

### Standardized Tick Data
```json
{
  "TickNumber": 10387,
  "DeltaTime": 0.0168,
  "CumulativeTime": 5.234,
  "Entities": [
    {
      "Index": 0,
      "Type": "Player",
      "Team": 1,
      "Position": { "X": 123.45, "Y": 67.89, "Z": 10.12 },
      "Health": 0.75
    }
  ],
  "Events": [
    { "Type": "Kill", "SourceEntityIndex": 2, "TargetEntityIndex": 5 }
  ]
}
```

See [DATA_STRUCTURES.md](DATA_STRUCTURES.md) for complete documentation.

## Current Limitations

⚠️ **Highlight files are short segments** (10-18 seconds) from matches, not full games
⚠️ **Entity data is bit-packed** - full decoding is ongoing
⚠️ **No full match data** - limits ML training capabilities for round-based analysis

## Project Structure

```
ReplayAnalyzer/
├── Program.cs              # CLI entry point
├── FrameParser.cs          # Binary frame parsing
├── TimelineVisualizer.cs   # HTML visualization
├── Models/
│   └── TickData.cs         # Data models for ML
└── DATA_STRUCTURES.md      # Detailed format documentation
```

## Dependencies

- **.NET 10.0**
- **TankLib**: Binary parsing for Overwatch file formats
- **ZstdSharp**: Zstandard decompression

## Use Cases

### Currently Supported
✅ Action recognition and movement analysis
✅ Team composition analysis
✅ Micro-level gameplay study (16ms granularity)
✅ Replay visualization and debugging

### Requires Full Match Data
❌ Round-based learning
❌ Objective tracking
❌ Match outcome prediction
❌ Full player statistics

## Contributing

1. Fork the repository
2. Implement your feature/fix
3. Update `DATA_STRUCTURES.md` with new findings
4. Submit a pull request

## Roadmap

- [ ] **Phase 1**: Improve entity bit-unpacking
- [ ] **Phase 2**: Event detection (kills, ults, abilities)
- [ ] **Phase 3**: 2D map overlay visualization
- [ ] **Phase 4**: Acquire full match replay data
- [ ] **Phase 5**: ML model training pipeline

## License

See LICENSE file in repository root.

## Acknowledgments

- Built on [OWLib/TankLib](https://github.com/overtools/OWLib) by overtools
- Zstandard compression by [ZstdSharp](https://github.com/oleg-st/ZstdSharp)

---

**Status**: Active Development 🚧
**Last Updated**: 2026-01-07
