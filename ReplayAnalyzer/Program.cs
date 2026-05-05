using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TankLib.Replay;
using ReplayAnalyzerTool.Models;
using ReplayAnalyzerTool.GameData;

namespace ReplayAnalyzerTool;

class Program
{
    static readonly FrameParser _frameParser = new();
    static readonly TimelineVisualizer _visualizer = new();
    static readonly EnhancedReplayAnalyzer _enhancedAnalyzer = new();

    static void Main(string[] args)
    {
        // Parse command line arguments
        bool generateTimeline = args.Contains("--timeline") || args.Contains("-t");
        bool exportJson = args.Contains("--json") || args.Contains("-j");
        bool verbose = args.Contains("--verbose") || args.Contains("-v");
        bool enhanced = args.Contains("--enhanced") || args.Contains("-e");

        string? highlightsPath = args.FirstOrDefault(a => !a.StartsWith("-"));

        if (string.IsNullOrEmpty(highlightsPath))
            highlightsPath = FindHighlightsPath();

        if (string.IsNullOrEmpty(highlightsPath))
        {
            PrintUsage();
            return;
        }

        Console.WriteLine($"🎮 Overwatch Replay Analyzer");
        Console.WriteLine($"═══════════════════════════════════════════════════════════════════════");
        Console.WriteLine($"  Source: {highlightsPath}");
        Console.WriteLine($"  Timeline: {(generateTimeline ? "Yes" : "No")}");
        Console.WriteLine($"  JSON Export: {(exportJson ? "Yes" : "No")}");
        Console.WriteLine($"  Enhanced Analysis: {(enhanced ? "Yes" : "No")}");
        Console.WriteLine($"═══════════════════════════════════════════════════════════════════════");
        Console.WriteLine();

        string outputDir = Path.Combine(Path.GetDirectoryName(highlightsPath)!, "Analysis");
        Directory.CreateDirectory(outputDir);

        var files = Directory.Exists(highlightsPath)
            ? Directory.GetFiles(highlightsPath, "*", SearchOption.TopDirectoryOnly)
            : new[] { highlightsPath };

        int processed = 0;
        int failed = 0;

        foreach (var file in files)
        {
            try
            {
                AnalyzeFile(file, outputDir, generateTimeline, exportJson, verbose, enhanced);
                processed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Failed: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"═══════════════════════════════════════════════════════════════════════");
        Console.WriteLine($"  ✅ Processed: {processed} files");
        if (failed > 0) Console.WriteLine($"  ❌ Failed: {failed} files");
        Console.WriteLine($"  📁 Output: {outputDir}");
        Console.WriteLine($"═══════════════════════════════════════════════════════════════════════");
    }

    static void PrintUsage()
    {
        Console.WriteLine("Overwatch Replay Analyzer");
        Console.WriteLine();
        Console.WriteLine("Usage: ReplayAnalyzer [options] <highlights-path>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -t, --timeline    Generate interactive HTML timeline viewer");
        Console.WriteLine("  -j, --json        Export standardized tick data as JSON");
        Console.WriteLine("  -e, --enhanced    Enable enhanced game-aware analysis (heroes, abilities, kills)");
        Console.WriteLine("  -v, --verbose     Show detailed parsing information");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ReplayAnalyzer ./Highlights");
        Console.WriteLine("  ReplayAnalyzer --timeline --enhanced ./Highlights");
        Console.WriteLine("  ReplayAnalyzer -t -e -j ./path/to/highlight-file");
    }

    static string? FindHighlightsPath()
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
                return Path.GetFullPath(path);
        }
        return null;
    }

    static void AnalyzeFile(string filePath, string outputDir, bool generateTimeline, bool exportJson, bool verbose, bool enhanced)
    {
        var fileName = Path.GetFileName(filePath);
        Console.WriteLine($"📄 {fileName}");

        // Read file into memory for analysis
        var fileData = File.ReadAllBytes(filePath);

        // Check for 'phl' magic (Player HighLight)
        if (fileData.Length < 4 || fileData[0] != 'p' || fileData[1] != 'h' || fileData[2] != 'l')
        {
            Console.WriteLine($"   ⚠️ Not a highlight file (invalid magic)");
            return;
        }

        // Parse highlight using memory stream
        using var ms = new MemoryStream(fileData);
        tePlayerHighlight highlight;
        try
        {
            highlight = new tePlayerHighlight(ms, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️ Failed to parse highlight header: {ex.Message}");
            return;
        }

        // Create segment info
        var segmentInfo = new ReplaySegmentInfo
        {
            SourceFile = filePath,
            MapGuid = highlight.Map.ToString(),
            GameModeGuid = highlight.GameMode.ToString(),
            PlayerId = (uint)highlight.PlayerId
        };

        // Debug: show what we got from the header
        if (verbose)
        {
            Console.WriteLine($"   Highlight Info array: {(highlight.Info != null ? highlight.Info.Length.ToString() : "null")}");
            Console.WriteLine($"   Heroes array: {(highlight.Heroes != null ? highlight.Heroes.Length.ToString() : "null")}");
        }

        // Extract player info if available
        if (highlight.Info != null && highlight.Info.Length > 0)
        {
            if (verbose)
            {
                Console.WriteLine($"   📋 Found {highlight.Info.Length} players in header");
            }
            foreach (var info in highlight.Info)
            {
                var playerInfo = new PlayerInfo
                {
                    Name = info.PlayerName ?? "",
                    HeroGuid = info.Hero.ToString(),
                    TeamIndex = (int)info.TeamIndex
                };
                segmentInfo.Players.Add(playerInfo);

                if (verbose)
                {
                    Console.WriteLine($"      Player: {playerInfo.Name} | Hero: {playerInfo.HeroGuid} | Team: {playerInfo.TeamIndex}");
                }
            }
        }

        // Find and decompress replay data
        byte[]? decompressed = null;
        for (int i = 0; i < fileData.Length - 3; i++)
        {
            if (fileData[i] == 'p' && fileData[i+1] == 'r' && fileData[i+2] == 'p')
            {
                segmentInfo.FormatVersion = fileData[i + 3];
                segmentInfo.BuildNumber = BitConverter.ToUInt32(fileData, i + 4);

                // Find Zstd magic
                for (int j = i; j < fileData.Length - 4; j++)
                {
                    uint zstdMagic = BitConverter.ToUInt32(fileData, j);
                    if (zstdMagic == 0xFD2FB528)
                    {
                        try
                        {
                            int compressedLength = fileData.Length - j;
                            byte[] compressed = new byte[compressedLength];
                            Array.Copy(fileData, j, compressed, 0, compressedLength);

                            using var compressedStream = new MemoryStream(compressed);
                            using var decompressor = new ZstdSharp.DecompressionStream(compressedStream);
                            using var decompressedStream = new MemoryStream();
                            decompressor.CopyTo(decompressedStream);
                            decompressed = decompressedStream.ToArray();
                        }
                        catch { }
                        break;
                    }
                }
                break;
            }
        }

        if (decompressed == null)
        {
            Console.WriteLine($"   ⚠️ Could not decompress replay data");
            return;
        }

        // Parse ticks using new parser
        var ticks = _frameParser.ParseTicks(decompressed, segmentInfo);

        Console.WriteLine($"   📊 {ticks.Count} ticks | {segmentInfo.TotalDuration:F2}s | Entities: {segmentInfo.MinEntityCount}-{segmentInfo.MaxEntityCount}");

        // Run enhanced analysis if requested
        EnhancedAnalysisResult? enhancedResult = null;
        if (enhanced && ticks.Count > 0)
        {
            var enhancedAnalyzer = new EnhancedReplayAnalyzer();
            enhancedResult = enhancedAnalyzer.Analyze(ticks, segmentInfo);
            Console.WriteLine($"   🎯 Enhanced: {enhancedResult.GameState.Players.Count} players | {enhancedResult.GameState.Interactions.Count} interactions");

            // Export narrative summary
            var narrativePath = Path.Combine(outputDir, $"{fileName}.narrative.txt");
            File.WriteAllText(narrativePath, enhancedResult.NarrativeSummary);

            // Export analysis log for debugging
            if (verbose)
            {
                var logPath = Path.Combine(outputDir, $"{fileName}.analysis.log");
                File.WriteAllLines(logPath, enhancedAnalyzer.AnalysisLog);
            }
        }

        // Export CSV (always)
        var csvPath = Path.Combine(outputDir, $"{fileName}.frames.csv");
        ExportTicksToCsv(ticks, csvPath, segmentInfo);

        // Export JSON summary (always)
        var summaryPath = Path.Combine(outputDir, $"{fileName}.summary.json");
        ExportSummaryJson(segmentInfo, summaryPath);

        // Export raw data for debugging
        var rawPath = Path.Combine(outputDir, $"{fileName}.raw");
        File.WriteAllBytes(rawPath, decompressed);

        // Generate timeline visualization if requested
        if (generateTimeline && ticks.Count > 0)
        {
            var timelinePath = Path.Combine(outputDir, $"{fileName}.timeline.html");
            _visualizer.GenerateTimeline(ticks, segmentInfo, timelinePath, enhancedResult);
            Console.WriteLine($"   🎬 Timeline: {timelinePath}");
        }

        // Export full tick data as JSON if requested
        if (exportJson && ticks.Count > 0)
        {
            var jsonPath = Path.Combine(outputDir, $"{fileName}.ticks.json");
            ExportTicksJson(ticks, segmentInfo, jsonPath, enhancedResult);
            Console.WriteLine($"   📦 JSON: {jsonPath}");
        }

        // Verbose output
        if (verbose)
        {
            Console.WriteLine($"   Map: {segmentInfo.MapGuid}");
            Console.WriteLine($"   Mode: {segmentInfo.GameModeGuid}");
            Console.WriteLine($"   Build: {segmentInfo.BuildNumber} (v{segmentInfo.FormatVersion})");
            if (segmentInfo.Players.Count > 0)
            {
                Console.WriteLine($"   Players:");
                foreach (var p in segmentInfo.Players)
                    Console.WriteLine($"      - {p.Name} (Team {p.TeamIndex})");
            }
        }
    }

    static void ExportTicksToCsv(List<TickData> ticks, string path, ReplaySegmentInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TickNumber,DeltaTime,CumulativeTime,EntityCount,RawDataSize,RawDataPreview");

        foreach (var t in ticks)
        {
            sb.AppendLine($"{t.TickNumber},{t.DeltaTime:F6},{t.CumulativeTime:F6},{t.Entities.Count},{t.RawData.Length},{t.RawDataPreview}");
        }

        File.WriteAllText(path, sb.ToString());
    }

    static void ExportSummaryJson(ReplaySegmentInfo info, string path)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(info, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }

    static void ExportTicksJson(List<TickData> ticks, ReplaySegmentInfo info, string path, EnhancedAnalysisResult? enhancedResult = null)
    {
        object export;

        if (enhancedResult != null)
        {
            export = new
            {
                Segment = info,
                Enhanced = new
                {
                    Players = enhancedResult.PlayerSummaries,
                    Interactions = enhancedResult.GameState.Interactions.Select(i => new
                    {
                        Tick = i.Tick,
                        Time = i.Time,
                        Type = i.Type.ToString(),
                        Source = i.SourcePlayer?.DisplayName,
                        Target = i.TargetPlayer?.DisplayName,
                        Ability = i.AbilityName,
                        Description = i.Description
                    }),
                    Killfeed = enhancedResult.GameState.Killfeed
                },
                Ticks = ticks.Select(t => new
                {
                    t.TickNumber,
                    t.DeltaTime,
                    t.CumulativeTime,
                    EntityCount = t.Entities.Count,
                    Events = t.Events.Select(e => new { Type = e.Type.ToString(), e.Detail }),
                    t.RawDataPreview
                })
            };
        }
        else
        {
            export = new
            {
                Segment = info,
                Ticks = ticks.Select(t => new
                {
                    t.TickNumber,
                    t.DeltaTime,
                    t.CumulativeTime,
                    EntityCount = t.Entities.Count,
                    Events = t.Events.Select(e => new { Type = e.Type.ToString(), e.Detail }),
                    t.RawDataPreview
                })
            };
        }

        var json = System.Text.Json.JsonSerializer.Serialize(export, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }

    // Legacy method for backwards compatibility - retained for detailed analysis output
    static void AnalyzeFileLegacy(string filePath, string outputDir)
    {
        var fileName = Path.GetFileName(filePath);
        Console.WriteLine(new string('═', 70));
        Console.WriteLine($"Analyzing: {fileName}");
        Console.WriteLine(new string('═', 70));

        try
        {
            // Read file into memory for analysis
            var fileData = File.ReadAllBytes(filePath);
            Console.WriteLine($"  File size: {fileData.Length:N0} bytes");

            // Check for 'phl' magic (Player HighLight)
            var magic = (fileData[0] << 16) | (fileData[1] << 8) | fileData[2];
            Console.WriteLine($"  Magic bytes: {(char)fileData[0]}{(char)fileData[1]}{(char)fileData[2]} (0x{magic:X6})");

            // Parse highlight using memory stream
            using var ms = new MemoryStream(fileData);
            var highlight = new tePlayerHighlight(ms, true);

            Console.WriteLine($"  Highlight.Replay is null: {highlight.Replay == null}");
            if (highlight.Replay != null)
            {
                Console.WriteLine($"  Highlight.Replay.Length: {highlight.Replay.Length:N0} bytes");

                // Check for 'prp' magic in replay
                if (highlight.Replay.Length >= 4)
                {
                    highlight.Replay.Position = 0;
                    var replayMagicBytes = new byte[4];
                    highlight.Replay.Read(replayMagicBytes, 0, 4);
                    var replayMagic = (replayMagicBytes[0] << 16) | (replayMagicBytes[1] << 8) | replayMagicBytes[2];
                    Console.WriteLine($"  Replay magic: {(char)replayMagicBytes[0]}{(char)replayMagicBytes[1]}{(char)replayMagicBytes[2]} (0x{replayMagic:X6})");
                }
            }

            // Check fields that affect parsing
            Console.WriteLine($"  Heroes count: {highlight.Heroes?.Length ?? 0}");
            Console.WriteLine($"  FillerStructs count: {highlight.FillerStructs?.Length ?? 0}");
            Console.WriteLine($"  Info count: {highlight.Info?.Length ?? 0}");

            // Look for 'prp' magic manually in the file
            Console.WriteLine();
            Console.WriteLine("  Searching for 'prp' (replay) marker...");
            for (int i = 0; i < fileData.Length - 3; i++)
            {
                if (fileData[i] == 'p' && fileData[i+1] == 'r' && fileData[i+2] == 'p')
                {
                    Console.WriteLine($"    Found 'prp' at offset 0x{i:X} ({i})");

                    // Manually parse replay header to find decompressed data
                    int offset = i;
                    byte formatVersion = fileData[offset + 3];
                    uint buildNumber = BitConverter.ToUInt32(fileData, offset + 4);

                    Console.WriteLine($"    Replay format version: {formatVersion}");
                    Console.WriteLine($"    Replay build: {buildNumber}");

                    // Try parsing remaining header fields
                    // Skip: magic(3) + formatVersion(1) + buildNumber(4) + Map(8) + GameMode(8) + Unknown1(1) + Unknown2(4) + Unknown3(4) + MapChecksum(20) + ParamsBlockLength(4)
                    // = 57 bytes to params block length
                    int paramsLenOffset = offset + 3 + 1 + 4 + 8 + 8 + 1 + 4 + 4 + 20;
                    int paramsBlockLength = BitConverter.ToInt32(fileData, paramsLenOffset);
                    Console.WriteLine($"    Params block length: {paramsBlockLength}");
                    Console.WriteLine($"    Params block length offset: 0x{paramsLenOffset:X}");

                    // Find where compressed data starts (after all header)
                    // Dump hex around the prp marker for manual inspection
                    int dumpStart = System.Math.Max(0, i - 16);
                    int dumpLen = System.Math.Min(256, fileData.Length - dumpStart);
                    var headerHex = GenerateHexDump(fileData, i, 128);
                    var headerHexPath = Path.Combine(outputDir, $"{fileName}.header.hex.txt");
                    File.WriteAllText(headerHexPath, $"Header at offset 0x{i:X}\n\n" + headerHex);
                    Console.WriteLine($"    Header hex: {headerHexPath}");

                    // Look for Zstd magic (0x28B52FFD) after the header
                    Console.WriteLine("    Searching for Zstd magic in entire file...");
                    bool foundZstd = false;
                    for (int j = i; j < fileData.Length - 4; j++)
                    {
                        uint zstdMagic = BitConverter.ToUInt32(fileData, j);
                        if (zstdMagic == 0xFD2FB528) // Zstd magic (little endian)
                        {
                            Console.WriteLine($"    Found Zstd magic at offset 0x{j:X} ({j})");
                            Console.WriteLine($"    Bytes before header: {j - i}");

                            // Extract and decompress from this point
                            int compressedLength = fileData.Length - j;
                            byte[] compressed = new byte[compressedLength];
                            Array.Copy(fileData, j, compressed, 0, compressedLength);

                            try
                            {
                                using var compressedStream = new MemoryStream(compressed);
                                using var decompressor = new ZstdSharp.DecompressionStream(compressedStream);
                                using var decompressedStream = new MemoryStream();
                                decompressor.CopyTo(decompressedStream);
                                var decompressed = decompressedStream.ToArray();

                                Console.WriteLine($"    Decompressed size: {decompressed.Length:N0} bytes");

                                // Save the decompressed data
                                var rawPath = Path.Combine(outputDir, $"{fileName}.raw");
                                File.WriteAllBytes(rawPath, decompressed);
                                Console.WriteLine($"    Saved raw data to: {rawPath}");

                                // Generate hex dump
                                var hexDump = GenerateHexDump(decompressed, 0, System.Math.Min(4096, decompressed.Length));
                                var hexPath = Path.Combine(outputDir, $"{fileName}.hex.txt");
                                File.WriteAllText(hexPath, hexDump);
                                Console.WriteLine($"    Hex dump: {hexPath}");

                                // Generate analysis
                                var analysisPath = Path.Combine(outputDir, $"{fileName}.analysis.txt");
                                File.WriteAllText(analysisPath, GenerateDeepAnalysisRaw(decompressed, formatVersion, buildNumber));
                                Console.WriteLine($"    Analysis: {analysisPath}");

                                // Try to identify frame structure
                                var frameAnalysis = AnalyzeFrameStructure(decompressed);
                                var frameAnalysisPath = Path.Combine(outputDir, $"{fileName}.frames.txt");
                                File.WriteAllText(frameAnalysisPath, frameAnalysis);
                                Console.WriteLine($"    Frame analysis: {frameAnalysisPath}");

                                // Parse and export frames to JSON for ML
                                var parsedFrames = ParseFrames(decompressed);
                                if (parsedFrames != null && parsedFrames.Count > 0)
                                {
                                    // Export to CSV for easy analysis
                                    var csvPath = Path.Combine(outputDir, $"{fileName}.frames.csv");
                                    ExportFramesToCsv(parsedFrames, csvPath, highlight);
                                    Console.WriteLine($"    Frame CSV: {csvPath} ({parsedFrames.Count} frames)");

                                    // Export first frame's hex for debugging
                                    var firstFrame = parsedFrames[0];
                                    Console.WriteLine($"    First frame: #{firstFrame.FrameNum}, dt={firstFrame.DeltaTime:F4}s, entities={firstFrame.EntityCount}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"    Decompression failed: {ex.Message}");
                            }
                            foundZstd = true;
                            break;
                        }
                    }

                    if (!foundZstd)
                    {
                        Console.WriteLine("    No Zstd magic found - data may not be compressed or uses different format");

                        // Try to find any compressed data pattern
                        // Export raw replay data (everything after 'prp' header)
                        var rawReplayPath = Path.Combine(outputDir, $"{fileName}.replay.bin");
                        var replayData = new byte[fileData.Length - i];
                        Array.Copy(fileData, i, replayData, 0, replayData.Length);
                        File.WriteAllBytes(rawReplayPath, replayData);
                        Console.WriteLine($"    Raw replay data saved: {rawReplayPath} ({replayData.Length:N0} bytes)");
                    }
                    break;
                }
            }

            // Print highlight header info
            Console.WriteLine();
            Console.WriteLine("  === Highlight Info ===");
            Console.WriteLine($"  Format: {highlight.FormatVersion}");
            Console.WriteLine($"  Map: {highlight.Map}");
            Console.WriteLine($"  GameMode: {highlight.GameMode}");
            Console.WriteLine($"  Player ID: {highlight.PlayerId}");
            Console.WriteLine($"  Flags: {highlight.Flags}");

            if (highlight.Info != null && highlight.Info.Length > 0)
            {
                Console.WriteLine($"  Players ({highlight.Info.Length}):");
                foreach (var info in highlight.Info)
                {
                    Console.WriteLine($"    - {info.PlayerName} (Hero: {info.Hero}, Team: {info.TeamIndex})");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine();
    }

    static string GenerateHexDump(byte[] data, int offset, int length)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Hex Dump: Offset 0x{offset:X8}, Length {length} bytes ===");
        sb.AppendLine();

        for (int i = 0; i < length; i += 16)
        {
            int lineOffset = offset + i;
            sb.Append($"{lineOffset:X8}  ");

            // Hex bytes
            for (int j = 0; j < 16; j++)
            {
                if (i + j < length)
                    sb.Append($"{data[lineOffset + j]:X2} ");
                else
                    sb.Append("   ");

                if (j == 7) sb.Append(" ");
            }

            sb.Append(" |");

            // ASCII representation
            for (int j = 0; j < 16 && i + j < length; j++)
            {
                byte b = data[lineOffset + j];
                sb.Append(b >= 32 && b < 127 ? (char)b : '.');
            }

            sb.AppendLine("|");
        }

        return sb.ToString();
    }

    static string GenerateDeepAnalysis(byte[] data, tePlayerReplay replay)
    {
        return GenerateDeepAnalysisRaw(data, replay.FormatVersion, replay.BuildNumber);
    }

    static string GenerateDeepAnalysisRaw(byte[] data, byte formatVersion, uint buildNumber)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== DEEP STRUCTURE ANALYSIS ===");
        sb.AppendLine($"Total size: {data.Length:N0} bytes");
        sb.AppendLine($"Format version: {formatVersion}");
        sb.AppendLine($"Build number: {buildNumber}");
        sb.AppendLine();

        // Analyze byte frequency distribution
        var frequency = new int[256];
        foreach (byte b in data)
            frequency[b]++;

        sb.AppendLine("=== BYTE FREQUENCY (top 20) ===");
        var sorted = frequency.Select((count, val) => (val, count))
            .OrderByDescending(x => x.count)
            .Take(20);
        foreach (var (val, count) in sorted)
        {
            double pct = 100.0 * count / data.Length;
            sb.AppendLine($"  0x{val:X2}: {count,8:N0} ({pct:F2}%)");
        }
        sb.AppendLine();

        // Look for potential delimiters/markers by finding repeating patterns
        sb.AppendLine("=== POTENTIAL FRAME MARKERS ===");
        var markers = FindPotentialMarkers(data);
        foreach (var (marker, offsets) in markers.Take(10))
        {
            if (offsets.Count >= 3)
            {
                // Calculate intervals
                var intervals = new List<int>();
                for (int i = 1; i < System.Math.Min(offsets.Count, 20); i++)
                    intervals.Add(offsets[i] - offsets[i - 1]);

                double avgInterval = intervals.Average();
                double stdDev = System.Math.Sqrt(intervals.Average(x => (x - avgInterval) * (x - avgInterval)));

                sb.AppendLine($"  Marker 0x{marker:X8}: {offsets.Count} occurrences");
                sb.AppendLine($"    First occurrences: {string.Join(", ", offsets.Take(5).Select(o => $"0x{o:X}"))}");
                sb.AppendLine($"    Avg interval: {avgInterval:F1} bytes, StdDev: {stdDev:F1}");
            }
        }
        sb.AppendLine();

        // Look for tick/frame numbers (incrementing sequences)
        sb.AppendLine("=== TICK NUMBER CANDIDATES ===");
        var tickCandidates = FindTickNumberCandidatesRaw(data);
        foreach (var (offset, type, startVal, count) in tickCandidates.Take(5))
        {
            sb.AppendLine($"  Offset 0x{offset:X}: {type} starting at {startVal}, found {count} incrementing values");
        }
        sb.AppendLine();

        // Look for float patterns (position data)
        sb.AppendLine("=== POTENTIAL POSITION DATA ===");
        var positionCandidates = FindPositionCandidates(data);
        foreach (var (offset, x, y, z) in positionCandidates.Take(10))
        {
            sb.AppendLine($"  Offset 0x{offset:X}: ({x:F2}, {y:F2}, {z:F2})");
        }

        return sb.ToString();
    }

    static List<(uint marker, List<int> offsets)> FindPotentialMarkers(byte[] data)
    {
        var markerOffsets = new Dictionary<uint, List<int>>();

        for (int i = 0; i < data.Length - 4; i++)
        {
            uint val = BitConverter.ToUInt32(data, i);
            if (!markerOffsets.ContainsKey(val))
                markerOffsets[val] = new List<int>();
            markerOffsets[val].Add(i);
        }

        return markerOffsets
            .Where(kvp => kvp.Value.Count >= 3 && kvp.Value.Count < data.Length / 100) // Not too rare, not too common
            .OrderByDescending(kvp => kvp.Value.Count)
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }

    static List<(int offset, string type, uint startVal, int count)> FindTickNumberCandidates(byte[] data, tePlayerReplay replay)
    {
        uint startFrame = replay.Params?.StartFrame ?? 0;
        return FindTickNumberCandidatesRaw(data, startFrame);
    }

    static List<(int offset, string type, uint startVal, int count)> FindTickNumberCandidatesRaw(byte[] data, uint startFrame = 0)
    {
        var results = new List<(int offset, string type, uint startVal, int count)>();

        // Look for uint16 incrementing sequences
        for (int i = 0; i < data.Length - 100; i += 2)
        {
            ushort val = BitConverter.ToUInt16(data, i);
            if (val >= startFrame && val < startFrame + 10000)
            {
                // Check if we have incrementing values at regular intervals
                int consecutiveFound = 1;
                for (int stride = 8; stride <= 256; stride += 4)
                {
                    int localConsec = 1;
                    for (int j = i + stride; j < data.Length - 2 && localConsec < 20; j += stride)
                    {
                        ushort nextVal = BitConverter.ToUInt16(data, j);
                        if (nextVal == val + localConsec)
                            localConsec++;
                        else
                            break;
                    }
                    if (localConsec > consecutiveFound)
                        consecutiveFound = localConsec;
                }

                if (consecutiveFound >= 5)
                {
                    results.Add((i, "uint16", val, consecutiveFound));
                }
            }
        }

        // Look for uint32 incrementing sequences
        for (int i = 0; i < data.Length - 100; i += 4)
        {
            uint val = BitConverter.ToUInt32(data, i);
            if (val >= startFrame && val < startFrame + 100000)
            {
                int consecutiveFound = 1;
                for (int stride = 16; stride <= 512; stride += 8)
                {
                    int localConsec = 1;
                    for (int j = i + stride; j < data.Length - 4 && localConsec < 20; j += stride)
                    {
                        uint nextVal = BitConverter.ToUInt32(data, j);
                        if (nextVal == val + localConsec)
                            localConsec++;
                        else
                            break;
                    }
                    if (localConsec > consecutiveFound)
                        consecutiveFound = localConsec;
                }

                if (consecutiveFound >= 5)
                {
                    results.Add((i, "uint32", val, consecutiveFound));
                }
            }
        }

        return results.OrderByDescending(x => x.count).ToList();
    }

    static List<(int offset, float x, float y, float z)> FindPositionCandidates(byte[] data)
    {
        var results = new List<(int offset, float x, float y, float z)>();

        for (int i = 0; i < data.Length - 12; i += 4)
        {
            float x = BitConverter.ToSingle(data, i);
            float y = BitConverter.ToSingle(data, i + 4);
            float z = BitConverter.ToSingle(data, i + 8);

            // Reasonable Overwatch map coordinates
            if (System.Math.Abs(x) < 500 && System.Math.Abs(y) < 200 && System.Math.Abs(z) < 500 &&
                !float.IsNaN(x) && !float.IsNaN(y) && !float.IsNaN(z) &&
                !float.IsInfinity(x) && !float.IsInfinity(y) && !float.IsInfinity(z) &&
                (System.Math.Abs(x) > 1 || System.Math.Abs(y) > 1 || System.Math.Abs(z) > 1)) // Not all near-zero
            {
                results.Add((i, x, y, z));
            }
        }

        return results;
    }

    static string AnalyzeFrameStructure(byte[] data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== FRAME STRUCTURE ANALYSIS ===");
        sb.AppendLine($"Total data size: {data.Length:N0} bytes");
        sb.AppendLine();

        // Look for pattern: Frame# (2 bytes) followed by length (2 bytes)
        // The frame marker 0x00077A84 appears at consistent intervals
        // Let's search for the constant 0x00077A84 and work backwards
        var marker84Offsets = new List<int>();
        for (int i = 0; i < data.Length - 4; i++)
        {
            if (data[i] == 0x84 && data[i+1] == 0x7A && data[i+2] == 0x07 && data[i+3] == 0x00)
            {
                marker84Offsets.Add(i);
            }
        }

        sb.AppendLine($"Marker 0x00077A84 occurrences: {marker84Offsets.Count}");
        sb.AppendLine();

        if (marker84Offsets.Count >= 2)
        {
            // Analyze what comes before the marker
            sb.AppendLine("=== ANALYZING STRUCTURE AROUND MARKER 0x00077A84 ===");
            sb.AppendLine();

            // Look at first few occurrences
            for (int m = 0; m < System.Math.Min(10, marker84Offsets.Count); m++)
            {
                int markerOffset = marker84Offsets[m];

                // Read data before and after marker
                int start = System.Math.Max(0, markerOffset - 16);
                int end = System.Math.Min(data.Length, markerOffset + 20);

                sb.AppendLine($"Occurrence {m+1} at offset 0x{markerOffset:X}:");
                sb.Append("  Before: ");
                for (int i = start; i < markerOffset; i++)
                    sb.Append($"{data[i]:X2} ");
                sb.AppendLine();
                sb.Append("  Marker+After: ");
                for (int i = markerOffset; i < end; i++)
                    sb.Append($"{data[i]:X2} ");
                sb.AppendLine();

                // Try to interpret the 4 bytes before the marker as potential frame info
                if (markerOffset >= 4)
                {
                    ushort val1 = BitConverter.ToUInt16(data, markerOffset - 4);
                    ushort val2 = BitConverter.ToUInt16(data, markerOffset - 2);
                    sb.AppendLine($"  4 bytes before: {val1} (0x{val1:X4}), {val2} (0x{val2:X4})");
                }

                // Look for frame number after marker
                if (markerOffset + 8 <= data.Length)
                {
                    uint after = BitConverter.ToUInt32(data, markerOffset + 4);
                    sb.AppendLine($"  4 bytes after marker: 0x{after:X8}");
                }
                sb.AppendLine();
            }

            // Calculate intervals between markers
            var intervals = new List<int>();
            for (int i = 1; i < marker84Offsets.Count; i++)
                intervals.Add(marker84Offsets[i] - marker84Offsets[i-1]);

            sb.AppendLine("=== MARKER INTERVALS ===");
            sb.AppendLine($"Min interval: {intervals.Min()} bytes");
            sb.AppendLine($"Max interval: {intervals.Max()} bytes");
            sb.AppendLine($"Average interval: {intervals.Average():F1} bytes");
            sb.AppendLine();
        }

        // Now parse the actual frame structure:
        // Frame header format (12 bytes):
        //   Bytes 0-3: Frame number (u32 little-endian)
        //   Bytes 4-7: Frame data length (u32 little-endian, NOT including header)
        //   Bytes 8-11: Player/entity count or type (u32)
        //   Then: frame data bytes
        //
        // The marker 0x00077A84 appears at offset 4 within the frame data
        // So frame header starts at (marker offset - 16)
        //
        // There might be inter-frame padding or a different header size!

        sb.AppendLine("=== PARSING FRAME STRUCTURE ===");
        sb.AppendLine("Header: [FrameNum:u32][DataLen:u32][EntityCount:u32][Data...]");
        sb.AppendLine();

        // First, let's carefully look at the bytes between consecutive markers
        if (marker84Offsets.Count >= 2)
        {
            sb.AppendLine("=== INVESTIGATING INTER-FRAME STRUCTURE ===");

            // Look at gap between first two markers
            int m1 = marker84Offsets[0]; // 0x7299
            int m2 = marker84Offsets[1]; // 0x72F8

            // Frame 1 header is 16 bytes before m1
            int f1_header = m1 - 16;  // 0x7289
            uint f1_frameNum = BitConverter.ToUInt32(data, f1_header);
            uint f1_dataLen = BitConverter.ToUInt32(data, f1_header + 4);
            uint f1_entityCount = BitConverter.ToUInt32(data, f1_header + 8);

            int f1_end = f1_header + 12 + (int)f1_dataLen;

            // Frame 2 header is 16 bytes before m2
            int f2_header = m2 - 16;  // 0x72E8

            sb.AppendLine($"Frame 1 header at 0x{f1_header:X}: frameNum={f1_frameNum}, dataLen={f1_dataLen}, entityCount={f1_entityCount}");
            sb.AppendLine($"Frame 1 ends at 0x{f1_end:X} (header + 12 + dataLen)");
            sb.AppendLine($"Frame 2 header at 0x{f2_header:X}");
            sb.AppendLine($"Gap between frames: {f2_header - f1_end} bytes");

            // Hex dump the gap
            if (f2_header > f1_end)
            {
                sb.Append("Gap bytes: ");
                for (int i = f1_end; i < f2_header; i++)
                    sb.Append($"{data[i]:X2} ");
                sb.AppendLine();
            }
            else if (f1_end > f2_header)
            {
                sb.AppendLine($"OVERLAP! Frame 1 extends {f1_end - f2_header} bytes past frame 2 start");
                // Maybe the dataLen field doesn't include all frame data?
                // Or maybe the marker isn't always at the same offset in data
            }

            sb.AppendLine();

            // Let's check if marker position varies within frame data
            sb.AppendLine("=== MARKER OFFSET WITHIN FRAME DATA ===");

            // Using marker offsets, calculate frame boundaries differently
            // If marker is at offset M in data, and frame header is 12 bytes, then:
            // frameHeader = markerOffset - 12 - M
            // We need to find M (marker offset within data)

            // From marker 1: frame header at m1-16, so marker offset in data = m1 - (m1-16+12) = 4
            // From marker 2: frame header at m2-16, so marker offset in data = m2 - (m2-16+12) = 4
            // Both have marker at data offset 4... but that would mean consistent frame structure

            // Let's verify frame 2
            uint f2_frameNum = BitConverter.ToUInt32(data, f2_header);
            uint f2_dataLen = BitConverter.ToUInt32(data, f2_header + 4);
            uint f2_entityCount = BitConverter.ToUInt32(data, f2_header + 8);

            sb.AppendLine($"Frame 2: frameNum={f2_frameNum}, dataLen={f2_dataLen}, entityCount={f2_entityCount}");

            // Calc where marker should be
            int expectedMarkerInF2 = f2_header + 12 + 4; // header + 4 bytes into data
            sb.AppendLine($"Expected marker position: 0x{expectedMarkerInF2:X}, actual: 0x{m2:X}");

            sb.AppendLine();
        }

        // Use the marker locations to find frames - but account for inter-frame gap
        if (marker84Offsets.Count > 0)
        {
            int firstMarker = marker84Offsets[0];
            int frameStart = firstMarker - 16;

            sb.AppendLine($"Starting frame parsing at 0x{frameStart:X}");

            // Parse frames, but check each frame header starts with sequential frame number
            var parsedFrames = new List<(int offset, uint frameNum, uint dataLen, uint entityCount, byte[] data)>();
            int currentOffset = frameStart;
            uint expectedFrameNum = BitConverter.ToUInt32(data, frameStart);

            while (currentOffset + 12 <= data.Length)
            {
                uint fn = BitConverter.ToUInt32(data, currentOffset);
                uint dl = BitConverter.ToUInt32(data, currentOffset + 4);
                uint ec = BitConverter.ToUInt32(data, currentOffset + 8);

                // Validate
                if (fn < 1 || fn > 999999 || dl < 4 || dl > 10000 || ec < 1 || ec > 100)
                {
                    // Try to find next frame by scanning for expected frame number
                    sb.AppendLine($"Frame validation failed at 0x{currentOffset:X}: fn={fn}, dl={dl}, ec={ec}");
                    sb.AppendLine($"  Expected frameNum: {expectedFrameNum}");

                    // Scan for next expected frame number
                    bool found = false;
                    for (int scan = currentOffset; scan < Math.Min(currentOffset + 100, data.Length - 12); scan++)
                    {
                        uint scanFn = BitConverter.ToUInt32(data, scan);
                        if (scanFn == expectedFrameNum)
                        {
                            uint scanDl = BitConverter.ToUInt32(data, scan + 4);
                            uint scanEc = BitConverter.ToUInt32(data, scan + 8);
                            if (scanDl >= 4 && scanDl <= 10000 && scanEc >= 1 && scanEc <= 100)
                            {
                                sb.AppendLine($"  Found next frame at 0x{scan:X} (scanned {scan - currentOffset} bytes)");
                                currentOffset = scan;
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        sb.AppendLine($"  Could not find next frame, stopping parse");
                        break;
                    }

                    // Re-read at new position
                    fn = BitConverter.ToUInt32(data, currentOffset);
                    dl = BitConverter.ToUInt32(data, currentOffset + 4);
                    ec = BitConverter.ToUInt32(data, currentOffset + 8);
                }

                if (currentOffset + 12 + (int)dl > data.Length)
                {
                    sb.AppendLine($"Frame data extends past end of file at offset 0x{currentOffset:X}");
                    break;
                }

                var frameData = new byte[dl];
                Array.Copy(data, currentOffset + 12, frameData, 0, (int)dl);
                parsedFrames.Add((currentOffset, fn, dl, ec, frameData));

                // Move to next frame - there might be padding, so scan for next frame number
                int nextExpectedOffset = currentOffset + 12 + (int)dl;
                expectedFrameNum = fn + 1;
                currentOffset = nextExpectedOffset;

                if (parsedFrames.Count > 5000) break; // Safety limit
            }

            sb.AppendLine();
            sb.AppendLine($"Total parsed frames: {parsedFrames.Count}");
            if (parsedFrames.Count > 0)
            {
                sb.AppendLine($"Frame number range: {parsedFrames.First().frameNum} - {parsedFrames.Last().frameNum}");
                sb.AppendLine($"Data sizes: {parsedFrames.Min(f => f.dataLen)} - {parsedFrames.Max(f => f.dataLen)} bytes");
                sb.AppendLine($"Entity counts: {parsedFrames.Min(f => f.entityCount)} - {parsedFrames.Max(f => f.entityCount)}");
            }
            sb.AppendLine();

            // Show first 30 frames in detail
            sb.AppendLine("First 30 frames:");
            foreach (var (offset, frameNum, dataLen, entityCount, frameData) in parsedFrames.Take(30))
            {
                sb.AppendLine($"  Frame {frameNum} at 0x{offset:X4}: {dataLen} bytes, {entityCount} entities");
                sb.Append("    Data: ");
                for (int j = 0; j < Math.Min(40, frameData.Length); j++)
                    sb.Append($"{frameData[j]:X2} ");
                if (frameData.Length > 40) sb.Append("...");
                sb.AppendLine();
            }

            // Analyze per-entity data structure
            sb.AppendLine();
            sb.AppendLine("=== ENTITY DATA ANALYSIS ===");

            var byEntityCount = parsedFrames.GroupBy(f => f.entityCount)
                .Select(g => new { Count = g.Key, AvgSize = g.Average(f => f.dataLen), Samples = g.Count() });

            foreach (var group in byEntityCount.OrderBy(g => g.Count))
            {
                sb.AppendLine($"  {group.Count} entities: avg {group.AvgSize:F1} bytes, {group.Samples} samples");
                var bytesPerEntity = group.AvgSize / group.Count;
                sb.AppendLine($"    ~{bytesPerEntity:F1} bytes per entity");
            }

            // Analyze frame data structure in detail
            sb.AppendLine();
            sb.AppendLine("=== FRAME DATA STRUCTURE ANALYSIS ===");

            // Look at first 10 frames with 9 entities to understand consistent structure
            var framesWithNine = parsedFrames.Where(f => f.entityCount == 9).Take(20).ToList();

            sb.AppendLine($"Analyzing first 20 frames with 9 entities:");
            sb.AppendLine();

            sb.AppendLine("Bytes 0-3 (float - delta time?):");
            foreach (var f in framesWithNine.Take(10))
            {
                float dt = BitConverter.ToSingle(f.data, 0);
                sb.AppendLine($"  Frame {f.frameNum}: {dt:F6}");
            }

            sb.AppendLine();
            sb.AppendLine("Bytes 4-7 (marker 0x00077A84):");
            foreach (var f in framesWithNine.Take(5))
            {
                uint marker = BitConverter.ToUInt32(f.data, 4);
                sb.AppendLine($"  Frame {f.frameNum}: 0x{marker:X8}");
            }

            sb.AppendLine();
            sb.AppendLine("Bytes 8-11:");
            foreach (var f in framesWithNine.Take(10))
            {
                if (f.data.Length >= 12)
                {
                    uint val = BitConverter.ToUInt32(f.data, 8);
                    sb.AppendLine($"  Frame {f.frameNum}: 0x{val:X8} ({val})");
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== LOOKING FOR POSITION DATA (floats in reasonable range) ===");

            // Scan frame data for float triplets that could be positions
            foreach (var f in framesWithNine.Take(5))
            {
                sb.AppendLine($"\nFrame {f.frameNum} ({f.data.Length} bytes):");

                // Look for float sequences
                for (int i = 0; i < f.data.Length - 12; i += 4)
                {
                    float v1 = BitConverter.ToSingle(f.data, i);
                    float v2 = BitConverter.ToSingle(f.data, i + 4);
                    float v3 = BitConverter.ToSingle(f.data, i + 8);

                    // Check if all three are in a reasonable position range
                    bool valid1 = !float.IsNaN(v1) && !float.IsInfinity(v1) && Math.Abs(v1) < 200;
                    bool valid2 = !float.IsNaN(v2) && !float.IsInfinity(v2) && Math.Abs(v2) < 200;
                    bool valid3 = !float.IsNaN(v3) && !float.IsInfinity(v3) && Math.Abs(v3) < 200;

                    if (valid1 && valid2 && valid3 && (Math.Abs(v1) > 0.1 || Math.Abs(v2) > 0.1 || Math.Abs(v3) > 0.1))
                    {
                        sb.AppendLine($"  Offset {i}: ({v1:F2}, {v2:F2}, {v3:F2})");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== BIT-LEVEL ANALYSIS ===");
            sb.AppendLine("Frame data appears to be bit-packed. Analyzing bit patterns...");

            // Look at how data changes between consecutive frames
            if (framesWithNine.Count >= 2)
            {
                var f1 = framesWithNine[0];
                var f2 = framesWithNine[1];

                sb.AppendLine($"\nComparing Frame {f1.frameNum} and {f2.frameNum}:");
                sb.AppendLine("Bytes that differ:");

                int minLen = Math.Min(f1.data.Length, f2.data.Length);
                int diffCount = 0;
                for (int i = 0; i < minLen; i++)
                {
                    if (f1.data[i] != f2.data[i])
                    {
                        diffCount++;
                        if (diffCount <= 20)
                            sb.AppendLine($"  Byte {i}: 0x{f1.data[i]:X2} -> 0x{f2.data[i]:X2}");
                    }
                }
                sb.AppendLine($"Total differing bytes: {diffCount} of {minLen}");
            }
        }
        else
        {
            sb.AppendLine("No marker 0x00077A84 found - cannot determine frame start");
        }

        return sb.ToString();
    }

    // Data class for parsed frame
    class ParsedFrame
    {
        public uint FrameNum { get; set; }
        public uint DataLen { get; set; }
        public uint EntityCount { get; set; }
        public float DeltaTime { get; set; }
        public uint Marker { get; set; }
        public byte[] RawData { get; set; } = Array.Empty<byte>();
    }

    static List<ParsedFrame>? ParseFrames(byte[] data)
    {
        // Find marker 0x00077A84
        var marker84Offsets = new List<int>();
        for (int i = 0; i < data.Length - 4; i++)
        {
            if (data[i] == 0x84 && data[i+1] == 0x7A && data[i+2] == 0x07 && data[i+3] == 0x00)
            {
                marker84Offsets.Add(i);
            }
        }

        if (marker84Offsets.Count == 0) return null;

        // Frame header is 16 bytes before marker (12-byte header + 4 bytes data before marker)
        int firstMarker = marker84Offsets[0];
        int frameStart = firstMarker - 16;

        var parsedFrames = new List<ParsedFrame>();
        int currentOffset = frameStart;
        uint expectedFrameNum = BitConverter.ToUInt32(data, frameStart);

        while (currentOffset + 12 <= data.Length)
        {
            uint fn = BitConverter.ToUInt32(data, currentOffset);
            uint dl = BitConverter.ToUInt32(data, currentOffset + 4);
            uint ec = BitConverter.ToUInt32(data, currentOffset + 8);

            // Validate
            if (fn < 1 || fn > 999999 || dl < 4 || dl > 10000 || ec < 1 || ec > 100)
            {
                // Scan for next expected frame number
                bool found = false;
                for (int scan = currentOffset; scan < Math.Min(currentOffset + 100, data.Length - 12); scan++)
                {
                    uint scanFn = BitConverter.ToUInt32(data, scan);
                    if (scanFn == expectedFrameNum)
                    {
                        uint scanDl = BitConverter.ToUInt32(data, scan + 4);
                        uint scanEc = BitConverter.ToUInt32(data, scan + 8);
                        if (scanDl >= 4 && scanDl <= 10000 && scanEc >= 1 && scanEc <= 100)
                        {
                            currentOffset = scan;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found) break;

                fn = BitConverter.ToUInt32(data, currentOffset);
                dl = BitConverter.ToUInt32(data, currentOffset + 4);
                ec = BitConverter.ToUInt32(data, currentOffset + 8);
            }

            if (currentOffset + 12 + (int)dl > data.Length) break;

            var frameData = new byte[dl];
            Array.Copy(data, currentOffset + 12, frameData, 0, (int)dl);

            float deltaTime = frameData.Length >= 4 ? BitConverter.ToSingle(frameData, 0) : 0;
            uint marker = frameData.Length >= 8 ? BitConverter.ToUInt32(frameData, 4) : 0;

            parsedFrames.Add(new ParsedFrame
            {
                FrameNum = fn,
                DataLen = dl,
                EntityCount = ec,
                DeltaTime = deltaTime,
                Marker = marker,
                RawData = frameData
            });

            expectedFrameNum = fn + 1;
            currentOffset = currentOffset + 12 + (int)dl;

            if (parsedFrames.Count > 5000) break;
        }

        return parsedFrames;
    }

    static void ExportFramesToCsv(List<ParsedFrame> frames, string path, tePlayerHighlight highlight)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("FrameNum,DeltaTime,EntityCount,DataLen,Marker,DataHexFirst32");

        foreach (var f in frames)
        {
            var hexFirst32 = string.Join("", f.RawData.Take(32).Select(b => b.ToString("X2")));
            sb.AppendLine($"{f.FrameNum},{f.DeltaTime:F6},{f.EntityCount},{f.DataLen},0x{f.Marker:X8},{hexFirst32}");
        }

        File.WriteAllText(path, sb.ToString());

        // Also export a summary JSON
        var jsonPath = Path.ChangeExtension(path, ".summary.json");
        var summary = new
        {
            FileName = Path.GetFileName(path),
            Map = highlight.Map.ToString(),
            GameMode = highlight.GameMode.ToString(),
            PlayerId = highlight.PlayerId,
            TotalFrames = frames.Count,
            FrameRange = new { Start = frames.First().FrameNum, End = frames.Last().FrameNum },
            EntityCountRange = new { Min = frames.Min(f => f.EntityCount), Max = frames.Max(f => f.EntityCount) },
            AverageDeltaTime = frames.Average(f => f.DeltaTime),
            TotalDuration = frames.Sum(f => f.DeltaTime),
            Players = highlight.Info?.Select(p => new { p.PlayerName, Hero = p.Hero.ToString(), p.TeamIndex }).ToArray()
        };

        var json = System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);
    }
}
