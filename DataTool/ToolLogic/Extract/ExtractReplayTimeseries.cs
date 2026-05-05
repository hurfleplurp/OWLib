using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataTool.Flag;
using DataTool.Helper;
using DataTool.JSON;
using TankLib;
using TankLib.Replay;
using TankLib.Replay.Frames;
using TankLib.Replay.State;
using TankLib.Statescript;
using static DataTool.Helper.IO;

namespace DataTool.ToolLogic.Extract;

[Tool("extract-replay-timeseries", Description = "Extract replay data as ML-ready timeseries JSON", IsSensitive = true, CustomFlags = typeof(ExtractReplayTimeseriesFlags))]
public class ExtractReplayTimeseries : JSONTool, ITool
{
    // Tick rate: Overwatch runs at ~63 ticks/sec, we'll output at configurable rate
    private const float SERVER_TICK_RATE = 62.5f;
    private const float DEFAULT_OUTPUT_RATE = 20.0f;  // 20 Hz default

    private AbilityTracker _abilityTracker;
    private JsonSerializerOptions _jsonOptions;

    public void Parse(ICLIFlags toolFlags)
    {
        var flags = (ExtractReplayTimeseriesFlags)toolFlags;

        // Initialize components
        _abilityTracker = new AbilityTracker(new GraphLoader());
        StatEventMapper.Initialize();

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = !flags.Compact,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        string inputPath = flags.InputPath;

        // Default to ReplayDataSources/Highlights
        if (string.IsNullOrEmpty(inputPath))
        {
            var possiblePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "ReplayDataSources", "Highlights"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "ReplayDataSources", "Highlights"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "ReplayDataSources", "Highlights"),
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    inputPath = path;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(inputPath))
        {
            Log("Error: Could not find input path. Use --input to specify.");
            return;
        }

        string outputDir = flags.OutputPath ?? Path.Combine(Path.GetDirectoryName(inputPath), "Timeseries");
        Directory.CreateDirectory(outputDir);

        Log($"Input: {inputPath}");
        Log($"Output: {outputDir}");
        Log($"Output Rate: {flags.OutputRate} Hz");
        Log();

        // Process files
        var files = Directory.Exists(inputPath)
            ? Directory.GetFiles(inputPath, "*", SearchOption.TopDirectoryOnly)
            : new[] { inputPath };

        foreach (var file in files)
        {
            ProcessHighlightFile(file, outputDir, flags);
        }

        // Export unmapped event hashes for analysis
        if (StatEventMapper.UnmappedHashes.Count > 0)
        {
            var unmappedPath = Path.Combine(outputDir, "_unmapped_event_hashes.txt");
            StatEventMapper.ExportUnmappedHashes(unmappedPath);
            Log($"Exported {StatEventMapper.UnmappedHashes.Count} unmapped event hashes to: {unmappedPath}");
        }

        Log("Extraction complete.");
    }

    private void ProcessHighlightFile(string filePath, string outputDir, ExtractReplayTimeseriesFlags flags)
    {
        var fileName = Path.GetFileName(filePath);
        Log($"Processing: {fileName}");

        try
        {
            var analyzer = ReplayAnalyzer.LoadHighlight(filePath);

            if (analyzer.DecompressedData == null || analyzer.DecompressedData.Length == 0)
            {
                Log($"  Warning: No decompressed data available");
                return;
            }

            // Extract metadata from headers
            var metadata = ExtractMetadata(analyzer);

            // Build timeline
            var timeline = BuildTimeline(analyzer, flags);

            // Create output structure
            var output = new TimeseriesOutput
            {
                Metadata = metadata,
                Frames = timeline
            };

            // Write output
            var outputPath = Path.Combine(outputDir, $"{fileName}.jsonl");
            WriteTimeseriesOutput(output, outputPath, flags);

            Log($"  Output: {outputPath}");
            Log($"  Frames: {timeline.Count}");
            Log($"  Duration: {metadata.DurationSeconds:F2}s");
        }
        catch (Exception ex)
        {
            Log($"  Error: {ex.Message}");
            if (flags.Verbose)
            {
                Log($"  Stack: {ex.StackTrace}");
            }
        }
    }

    private ReplayMetadata ExtractMetadata(ReplayAnalyzer analyzer)
    {
        var metadata = new ReplayMetadata();

        if (analyzer.HighlightHeader != null)
        {
            var hl = analyzer.HighlightHeader;
            metadata.MapGuid = hl.Map.ToString();
            metadata.GameModeGuid = hl.GameMode.ToString();
            metadata.PlayerId = hl.PlayerId;

            // Extract player info from highlight info
            if (hl.Info != null && hl.Info.Length > 0)
            {
                foreach (var info in hl.Info)
                {
                    metadata.Players.Add(new PlayerMetadata
                    {
                        Name = info.PlayerName,
                        HeroGuid = info.Hero.ToString(),
                        Team = info.TeamIndex.ToString(),
                        HighlightType = info.Kind.ToString()
                    });
                }
            }

            // Extract hero data
            if (hl.Heroes != null)
            {
                for (int i = 0; i < hl.Heroes.Length; i++)
                {
                    var hero = hl.Heroes[i];
                    if (i < metadata.Players.Count)
                    {
                        metadata.Players[i].SkinId = hero.SkinId;
                    }
                }
            }
        }

        if (analyzer.ReplayHeader != null)
        {
            var rp = analyzer.ReplayHeader;
            metadata.FormatVersion = rp.FormatVersion;
            metadata.BuildNumber = rp.BuildNumber;
            metadata.MapGuid = rp.Map.ToString();
            metadata.GameModeGuid = rp.GameMode.ToString();

            if (rp.Params != null)
            {
                metadata.StartFrame = rp.Params.StartFrame;
                metadata.EndFrame = rp.Params.EndFrame;
                metadata.DurationMs = rp.Params.ExpectedDurationMS;
                metadata.DurationSeconds = metadata.DurationMs / 1000.0f;
            }
        }

        metadata.DecompressedSize = analyzer.DecompressedData?.Length ?? 0;

        return metadata;
    }

    private List<TimeseriesFrame> BuildTimeline(ReplayAnalyzer analyzer, ExtractReplayTimeseriesFlags flags)
    {
        var frames = new List<TimeseriesFrame>();

        if (analyzer.DecompressedData == null || analyzer.DecompressedData.Length == 0)
            return frames;

        using var frameReader = new ReplayFrameReader(analyzer.DecompressedData);

        // First, analyze structure to find frame boundaries
        frameReader.AnalyzeStructure();

        if (frameReader.DetectedFrames.Count == 0)
        {
            // Fallback: create synthetic frames based on data size and expected duration
            return BuildSyntheticTimeline(analyzer, flags);
        }

        // Calculate output interval
        float outputInterval = 1.0f / flags.OutputRate;
        float lastOutputTime = -outputInterval;

        uint startFrame = analyzer.ReplayHeader?.Params?.StartFrame ?? 0;
        uint endFrame = analyzer.ReplayHeader?.Params?.EndFrame ?? (uint)frameReader.DetectedFrames.Count;
        float durationMs = analyzer.ReplayHeader?.Params?.ExpectedDurationMS ?? 10000;
        float msPerTick = durationMs / Math.Max(1, endFrame - startFrame);

        foreach (var boundary in frameReader.DetectedFrames)
        {
            float tickTime = boundary.TickNumber > 0
                ? (boundary.TickNumber - startFrame) * msPerTick / 1000.0f
                : boundary.Offset * msPerTick / 1000.0f / 100; // Rough estimate

            // Output at configured rate
            if (tickTime - lastOutputTime >= outputInterval)
            {
                var frame = CreateTimeseriesFrame(boundary, tickTime, frameReader, analyzer);
                frames.Add(frame);
                lastOutputTime = tickTime;
            }
        }

        return frames;
    }

    private List<TimeseriesFrame> BuildSyntheticTimeline(ReplayAnalyzer analyzer, ExtractReplayTimeseriesFlags flags)
    {
        var frames = new List<TimeseriesFrame>();

        // Create timeline based on replay params if available
        float durationSeconds = analyzer.ReplayHeader?.Params?.ExpectedDurationMS / 1000.0f ?? 10.0f;
        float outputInterval = 1.0f / flags.OutputRate;

        for (float t = 0; t < durationSeconds; t += outputInterval)
        {
            frames.Add(new TimeseriesFrame
            {
                TickNumber = (uint)(t * SERVER_TICK_RATE),
                TimestampMs = (ulong)(t * 1000),
                GameTimeSeconds = t,
                Phase = "Unknown",
                Players = new List<PlayerStateOutput>(),
                Events = new List<GameEventOutput>()
            });
        }

        return frames;
    }

    private TimeseriesFrame CreateTimeseriesFrame(FrameBoundary boundary, float gameTime, ReplayFrameReader reader, ReplayAnalyzer analyzer)
    {
        var frame = new TimeseriesFrame
        {
            TickNumber = boundary.TickNumber > 0 ? boundary.TickNumber : (uint)boundary.Offset,
            TimestampMs = (ulong)(gameTime * 1000),
            GameTimeSeconds = gameTime,
            Phase = "InProgress", // Would be determined from frame data
            Players = new List<PlayerStateOutput>(),
            Events = new List<GameEventOutput>()
        };

        // Extract player states from highlight info if available
        if (analyzer.HighlightHeader?.Info != null)
        {
            foreach (var info in analyzer.HighlightHeader.Info)
            {
                frame.Players.Add(new PlayerStateOutput
                {
                    Slot = frame.Players.Count,
                    Team = info.TeamIndex.ToString(),
                    PlayerName = info.PlayerName,
                    HeroGuid = info.Hero.ToString(),
                    Position = new[] { info.Position.X, info.Position.Y, info.Position.Z },
                    Facing = new[] { info.Direction.X, info.Direction.Y, info.Direction.Z },
                    IsAlive = true,
                    Health = new HealthOutput { Current = 100, Max = 100, Percentage = 1.0f },
                    UltimateCharge = 0.0f,
                    Abilities = new Dictionary<string, AbilityOutput>(),
                    AvailableActions = new List<string> { "Move", "PrimaryFire", "SecondaryFire", "Melee" }
                });
            }
        }

        return frame;
    }

    private void WriteTimeseriesOutput(TimeseriesOutput output, string outputPath, ExtractReplayTimeseriesFlags flags)
    {
        if (flags.JsonLines)
        {
            // JSON Lines format: one JSON object per line
            using var writer = new StreamWriter(outputPath);

            // Write metadata as first line
            writer.WriteLine(JsonSerializer.Serialize(new { type = "metadata", data = output.Metadata }, _jsonOptions));

            // Write each frame as a line
            foreach (var frame in output.Frames)
            {
                writer.WriteLine(JsonSerializer.Serialize(new { type = "frame", data = frame }, _jsonOptions));
            }
        }
        else
        {
            // Single JSON file
            var json = JsonSerializer.Serialize(output, _jsonOptions);
            File.WriteAllText(outputPath, json);
        }
    }
}

#region Output Data Structures

public class TimeseriesOutput
{
    public ReplayMetadata Metadata { get; set; }
    public List<TimeseriesFrame> Frames { get; set; }
}

public class ReplayMetadata
{
    public byte FormatVersion { get; set; }
    public uint BuildNumber { get; set; }
    public string MapGuid { get; set; }
    public string MapName { get; set; }
    public string GameModeGuid { get; set; }
    public string GameModeName { get; set; }
    public uint StartFrame { get; set; }
    public uint EndFrame { get; set; }
    public ulong DurationMs { get; set; }
    public float DurationSeconds { get; set; }
    public long PlayerId { get; set; }
    public int DecompressedSize { get; set; }
    public List<PlayerMetadata> Players { get; set; } = new List<PlayerMetadata>();
}

public class PlayerMetadata
{
    public string Name { get; set; }
    public string HeroGuid { get; set; }
    public string HeroName { get; set; }
    public string Team { get; set; }
    public string HighlightType { get; set; }
    public uint SkinId { get; set; }
}

public class TimeseriesFrame
{
    public uint TickNumber { get; set; }
    public ulong TimestampMs { get; set; }
    public float GameTimeSeconds { get; set; }
    public string Phase { get; set; }
    public ObjectiveOutput Objective { get; set; }
    public List<PlayerStateOutput> Players { get; set; }
    public List<GameEventOutput> Events { get; set; }
}

public class ObjectiveOutput
{
    public string Type { get; set; }
    public float Progress { get; set; }
    public int Checkpoint { get; set; }
    public string ControllingTeam { get; set; }
    public bool IsContested { get; set; }
}

public class PlayerStateOutput
{
    public int Slot { get; set; }
    public string Team { get; set; }
    public string PlayerName { get; set; }
    public string HeroGuid { get; set; }
    public string HeroName { get; set; }
    public float[] Position { get; set; }
    public float[] Facing { get; set; }
    public float[] Velocity { get; set; }
    public bool IsAlive { get; set; }
    public float RespawnTimer { get; set; }
    public HealthOutput Health { get; set; }
    public float UltimateCharge { get; set; }
    public bool UltimateReady { get; set; }
    public float PrimaryResource { get; set; }
    public Dictionary<string, AbilityOutput> Abilities { get; set; }
    public List<string> StatusEffects { get; set; }
    public List<string> AvailableActions { get; set; }
}

public class HealthOutput
{
    public float Current { get; set; }
    public float Max { get; set; }
    public float Armor { get; set; }
    public float Shields { get; set; }
    public float OverHealth { get; set; }
    public float Percentage { get; set; }
}

public class AbilityOutput
{
    public bool Available { get; set; }
    public int Charges { get; set; }
    public int MaxCharges { get; set; }
    public float CooldownRemaining { get; set; }
    public float CooldownDuration { get; set; }
    public bool IsActive { get; set; }
    public float Resource { get; set; }
    public float MaxResource { get; set; }
}

public class GameEventOutput
{
    public string Type { get; set; }
    public uint EventHash { get; set; }
    public uint Tick { get; set; }
    public ulong TimestampMs { get; set; }
    public int? SourceSlot { get; set; }
    public int? TargetSlot { get; set; }
    public string AbilityName { get; set; }
    public float Value { get; set; }
    public bool WasCritical { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

#endregion

public class ExtractReplayTimeseriesFlags : ICLIFlags
{
    [CLIFlag(Flag = "input", Help = "Input path (file or directory)", NeedsValue = true)]
    public string InputPath;

    [CLIFlag(Flag = "output", Help = "Output directory", NeedsValue = true)]
    public string OutputPath;

    [CLIFlag(Flag = "rate", Help = "Output tick rate in Hz (default: 20)", NeedsValue = true, Default = 20.0f)]
    public float OutputRate = 20.0f;

    [CLIFlag(Flag = "jsonl", Help = "Output as JSON Lines format (one object per line)", Default = true)]
    public bool JsonLines = true;

    [CLIFlag(Flag = "compact", Help = "Compact JSON output (no indentation)")]
    public bool Compact;

    [CLIFlag(Flag = "verbose", Help = "Verbose output")]
    public bool Verbose;

    public override bool Validate() => true;
}
