using System;
using System.IO;
using DataTool.Flag;
using DataTool.JSON;
using TankLib.Replay;
using static DataTool.Helper.IO;

namespace DataTool.ToolLogic.List.Misc;

[Tool("analyze-highlights", Description = "Analyze highlight/replay binary structure for reverse engineering", IsSensitive = true, CustomFlags = typeof(AnalyzeHighlightsFlags))]
public class AnalyzeHighlights : JSONTool, ITool
{
    public void Parse(ICLIFlags toolFlags)
    {
        var flags = (AnalyzeHighlightsFlags)toolFlags;

        string highlightsPath = flags.HighlightsPath;

        // Default to ReplayDataSources/Highlights if not specified
        if (string.IsNullOrEmpty(highlightsPath))
        {
            // Try to find relative to current directory or workspace
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
                    highlightsPath = path;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(highlightsPath) || !Directory.Exists(highlightsPath))
        {
            Log("Error: Could not find highlights directory. Use --highlights-path to specify.");
            Log($"Searched: ReplayDataSources/Highlights");
            return;
        }

        Log($"Analyzing highlights from: {highlightsPath}");
        Log();

        var files = Directory.GetFiles(highlightsPath, "*", SearchOption.TopDirectoryOnly);
        Log($"Found {files.Length} highlight files");
        Log();

        string outputDir = flags.OutputPath ?? Path.Combine(highlightsPath, "..", "Analysis");
        Directory.CreateDirectory(outputDir);

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            Log($"═══════════════════════════════════════════════════════════════");
            Log($"Analyzing: {fileName}");
            Log($"═══════════════════════════════════════════════════════════════");

            try
            {
                var analyzer = ReplayAnalyzer.LoadHighlight(file);

                // Generate report
                var report = analyzer.GenerateAnalysisReport();
                Log(report);

                // Export decompressed data for external analysis
                if (flags.ExportRaw && analyzer.DecompressedData != null)
                {
                    string rawOutputPath = Path.Combine(outputDir, $"{fileName}.raw");
                    analyzer.ExportDecompressedData(rawOutputPath);
                    Log($"Exported raw data to: {rawOutputPath}");
                }

                // Export hex dump
                if (flags.ExportHexDump && analyzer.DecompressedData != null)
                {
                    string hexOutputPath = Path.Combine(outputDir, $"{fileName}.hex.txt");
                    File.WriteAllText(hexOutputPath, analyzer.GenerateHexDump(0, Math.Min(4096, analyzer.DecompressedData.Length)));
                    Log($"Exported hex dump to: {hexOutputPath}");
                }

                // Export full report
                string reportPath = Path.Combine(outputDir, $"{fileName}.analysis.txt");
                File.WriteAllText(reportPath, report);
                Log($"Exported analysis to: {reportPath}");
            }
            catch (Exception ex)
            {
                Log($"Error analyzing {fileName}: {ex.Message}");
                if (flags.Verbose)
                {
                    Log(ex.StackTrace);
                }
            }

            Log();
        }

        Log($"Analysis complete. Output written to: {outputDir}");
    }
}

public class AnalyzeHighlightsFlags : ICLIFlags
{
    [CLIFlag(Flag = "highlights-path", Help = "Path to highlights directory", NeedsValue = true)]
    public string HighlightsPath;

    [CLIFlag(Flag = "output", Help = "Output directory for analysis files", NeedsValue = true)]
    public string OutputPath;

    [CLIFlag(Flag = "export-raw", Help = "Export raw decompressed replay data", Default = true)]
    public bool ExportRaw = true;

    [CLIFlag(Flag = "export-hex", Help = "Export hex dump of replay data", Default = true)]
    public bool ExportHexDump = true;

    [CLIFlag(Flag = "verbose", Help = "Verbose output including stack traces")]
    public bool Verbose;

    public override bool Validate() => true;
}
