using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TankLib.Replay
{
    /// <summary>
    /// Analyzes replay/highlight binary data to identify frame structures and patterns.
    /// Used for reverse engineering the decompressed replay payload format.
    /// </summary>
    public class ReplayAnalyzer
    {
        public byte[] DecompressedData { get; private set; }
        public tePlayerReplay ReplayHeader { get; private set; }
        public tePlayerHighlight HighlightHeader { get; private set; }

        /// <summary>
        /// Load a highlight file for analysis
        /// </summary>
        public static ReplayAnalyzer LoadHighlight(string filePath)
        {
            var analyzer = new ReplayAnalyzer();

            using (var stream = File.OpenRead(filePath))
            {
                analyzer.HighlightHeader = new tePlayerHighlight(stream);

                // The replay data is embedded in the highlight
                if (analyzer.HighlightHeader.Replay != null && analyzer.HighlightHeader.Replay.Length > 0)
                {
                    analyzer.HighlightHeader.Replay.Position = 0;
                    var replay = new tePlayerReplay(analyzer.HighlightHeader.Replay);
                    analyzer.ReplayHeader = replay;
                    analyzer.DecompressedData = replay.DecompressedBuffer;
                }
            }

            return analyzer;
        }

        /// <summary>
        /// Load a standalone replay file for analysis
        /// </summary>
        public static ReplayAnalyzer LoadReplay(string filePath)
        {
            var analyzer = new ReplayAnalyzer();

            using (var stream = File.OpenRead(filePath))
            {
                analyzer.ReplayHeader = new tePlayerReplay(stream);
                analyzer.DecompressedData = analyzer.ReplayHeader?.DecompressedBuffer;
            }

            return analyzer;
        }

        /// <summary>
        /// Generate a hex dump of the decompressed data with ASCII representation
        /// </summary>
        public string GenerateHexDump(int offset = 0, int length = -1)
        {
            if (DecompressedData == null || DecompressedData.Length == 0)
                return "No decompressed data available";

            if (length == -1 || offset + length > DecompressedData.Length)
                length = DecompressedData.Length - offset;

            var sb = new StringBuilder();
            sb.AppendLine($"=== Hex Dump: Offset {offset:X8}, Length {length} bytes ===");
            sb.AppendLine();

            for (int i = 0; i < length; i += 16)
            {
                int lineOffset = offset + i;
                sb.Append($"{lineOffset:X8}  ");

                // Hex bytes
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < length)
                        sb.Append($"{DecompressedData[lineOffset + j]:X2} ");
                    else
                        sb.Append("   ");

                    if (j == 7) sb.Append(" ");
                }

                sb.Append(" |");

                // ASCII representation
                for (int j = 0; j < 16 && i + j < length; j++)
                {
                    byte b = DecompressedData[lineOffset + j];
                    sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                }

                sb.AppendLine("|");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Search for potential frame headers by looking for repeating patterns
        /// </summary>
        public List<PatternMatch> FindRepeatingPatterns(int minPatternLength = 4, int maxPatternLength = 16, int minOccurrences = 3)
        {
            if (DecompressedData == null || DecompressedData.Length == 0)
                return new List<PatternMatch>();

            var matches = new List<PatternMatch>();
            var seenPatterns = new Dictionary<string, List<int>>();

            for (int patternLen = minPatternLength; patternLen <= maxPatternLength; patternLen++)
            {
                for (int i = 0; i <= DecompressedData.Length - patternLen; i++)
                {
                    var pattern = new byte[patternLen];
                    Array.Copy(DecompressedData, i, pattern, 0, patternLen);
                    string key = BitConverter.ToString(pattern);

                    if (!seenPatterns.ContainsKey(key))
                        seenPatterns[key] = new List<int>();
                    seenPatterns[key].Add(i);
                }
            }

            foreach (var kvp in seenPatterns.Where(x => x.Value.Count >= minOccurrences))
            {
                var patternBytes = kvp.Key.Split('-').Select(x => Convert.ToByte(x, 16)).ToArray();
                matches.Add(new PatternMatch
                {
                    Pattern = patternBytes,
                    Offsets = kvp.Value,
                    Occurrences = kvp.Value.Count
                });
            }

            return matches.OrderByDescending(x => x.Pattern.Length * x.Occurrences).Take(50).ToList();
        }

        /// <summary>
        /// Analyze potential frame boundaries by looking for length-prefixed structures
        /// </summary>
        public List<PotentialFrame> FindPotentialFrames()
        {
            if (DecompressedData == null || DecompressedData.Length < 8)
                return new List<PotentialFrame>();

            var frames = new List<PotentialFrame>();
            int offset = 0;

            using (var ms = new MemoryStream(DecompressedData))
            using (var reader = new BinaryReader(ms))
            {
                while (offset < DecompressedData.Length - 8)
                {
                    ms.Position = offset;

                    // Try to interpret as length-prefixed frame
                    uint potentialLength = reader.ReadUInt32();

                    // Sanity check: length should be reasonable (1 byte to 64KB)
                    if (potentialLength > 0 && potentialLength < 65536 && offset + potentialLength + 4 <= DecompressedData.Length)
                    {
                        // Check if next potential frame also looks valid
                        int nextOffset = offset + 4 + (int)potentialLength;
                        if (nextOffset < DecompressedData.Length - 4)
                        {
                            ms.Position = nextOffset;
                            uint nextLength = reader.ReadUInt32();

                            if (nextLength > 0 && nextLength < 65536)
                            {
                                frames.Add(new PotentialFrame
                                {
                                    Offset = offset,
                                    Length = (int)potentialLength,
                                    HeaderBytes = DecompressedData.Skip(offset).Take(System.Math.Min(16, (int)potentialLength + 4)).ToArray()
                                });
                            }
                        }
                    }

                    offset++;
                }
            }

            // Filter to likely frame boundaries (consistent spacing)
            return AnalyzeFrameSpacing(frames);
        }

        private List<PotentialFrame> AnalyzeFrameSpacing(List<PotentialFrame> candidates)
        {
            if (candidates.Count < 2)
                return candidates;

            // Group by similar frame lengths
            var byLength = candidates.GroupBy(f => f.Length / 10 * 10) // Group by tens
                                      .Where(g => g.Count() >= 3)
                                      .OrderByDescending(g => g.Count())
                                      .FirstOrDefault();

            return byLength?.ToList() ?? candidates.Take(20).ToList();
        }

        /// <summary>
        /// Search for 4-byte magic numbers that could indicate chunk/packet types
        /// </summary>
        public List<MagicCandidate> FindPotentialMagicNumbers()
        {
            if (DecompressedData == null || DecompressedData.Length < 4)
                return new List<MagicCandidate>();

            var candidates = new Dictionary<uint, List<int>>();

            for (int i = 0; i < DecompressedData.Length - 4; i++)
            {
                uint value = BitConverter.ToUInt32(DecompressedData, i);

                // Look for values that could be magic numbers (not likely to be floats/lengths)
                // Common patterns: ASCII-like (0x41-0x7A range), or specific hex patterns
                if (IsPotentialMagic(value))
                {
                    if (!candidates.ContainsKey(value))
                        candidates[value] = new List<int>();
                    candidates[value].Add(i);
                }
            }

            return candidates
                .Where(x => x.Value.Count >= 2 && x.Value.Count < 1000) // Multiple occurrences but not too common
                .Select(x => new MagicCandidate
                {
                    Value = x.Key,
                    Offsets = x.Value,
                    AsString = TryDecodeAsString(x.Key)
                })
                .OrderByDescending(x => x.Offsets.Count)
                .Take(30)
                .ToList();
        }

        private bool IsPotentialMagic(uint value)
        {
            // Check if all bytes are printable ASCII
            byte[] bytes = BitConverter.GetBytes(value);
            bool allAscii = bytes.All(b => b >= 0x20 && b < 0x7F);
            if (allAscii) return true;

            // Check for common magic patterns (e.g., 0xF123456F from teChunkedData)
            if ((value & 0xF0000000) == 0xF0000000) return true;
            if ((value & 0x0000000F) == 0x0000000F && value > 0x10000000) return true;

            return false;
        }

        private string TryDecodeAsString(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (bytes.All(b => b >= 0x20 && b < 0x7F))
                return Encoding.ASCII.GetString(bytes);
            return $"0x{value:X8}";
        }

        /// <summary>
        /// Generate a structural analysis report
        /// </summary>
        public string GenerateAnalysisReport()
        {
            var sb = new StringBuilder();

            sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║           REPLAY/HIGHLIGHT STRUCTURAL ANALYSIS               ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            // Header info
            if (HighlightHeader != null)
            {
                sb.AppendLine("=== HIGHLIGHT HEADER ===");
                sb.AppendLine($"Format Version: {HighlightHeader.FormatVersion}");
                sb.AppendLine($"Map GUID: {HighlightHeader.Map}");
                sb.AppendLine($"GameMode GUID: {HighlightHeader.GameMode}");
                sb.AppendLine($"Player ID: {HighlightHeader.PlayerId}");
                sb.AppendLine($"Flags: {HighlightHeader.Flags}");

                if (HighlightHeader.Info != null)
                {
                    sb.AppendLine($"Highlight Info Count: {HighlightHeader.Info.Length}");
                    foreach (var info in HighlightHeader.Info)
                    {
                        sb.AppendLine($"  - Player: {info.PlayerName}, Hero: {info.Hero}, Team: {info.TeamIndex}");
                        sb.AppendLine($"    Position: ({info.Position.X:F2}, {info.Position.Y:F2}, {info.Position.Z:F2})");
                    }
                }

                if (HighlightHeader.Heroes != null)
                {
                    sb.AppendLine($"Hero Data Count: {HighlightHeader.Heroes.Length}");
                    foreach (var hero in HighlightHeader.Heroes)
                    {
                        sb.AppendLine($"  - Hero GUID: {hero.Hero}, Skin: {hero.SkinId}");
                    }
                }
                sb.AppendLine();
            }

            if (ReplayHeader != null)
            {
                sb.AppendLine("=== REPLAY HEADER ===");
                sb.AppendLine($"Format Version: {ReplayHeader.FormatVersion}");
                sb.AppendLine($"Build Number: {ReplayHeader.BuildNumber}");
                sb.AppendLine($"Map GUID: {ReplayHeader.Map}");
                sb.AppendLine($"GameMode GUID: {ReplayHeader.GameMode}");

                if (ReplayHeader.Params != null)
                {
                    sb.AppendLine($"Start Frame: {ReplayHeader.Params.StartFrame}");
                    sb.AppendLine($"End Frame: {ReplayHeader.Params.EndFrame}");
                    sb.AppendLine($"Frame Count: {ReplayHeader.Params.EndFrame - ReplayHeader.Params.StartFrame}");
                    sb.AppendLine($"Duration: {ReplayHeader.Params.ExpectedDurationMS}ms ({ReplayHeader.Params.ExpectedDurationMS / 1000.0:F2}s)");
                    sb.AppendLine($"Start Time: {ReplayHeader.Params.StartMS}ms");
                    sb.AppendLine($"End Time: {ReplayHeader.Params.EndMS}ms");

                    if (ReplayHeader.Params.Heroes != null)
                    {
                        sb.AppendLine($"Heroes in Replay: {ReplayHeader.Params.Heroes.Length}");
                    }
                }
                sb.AppendLine();
            }

            // Decompressed data info
            if (DecompressedData != null)
            {
                sb.AppendLine("=== DECOMPRESSED PAYLOAD ===");
                sb.AppendLine($"Total Size: {DecompressedData.Length} bytes ({DecompressedData.Length / 1024.0:F2} KB)");
                sb.AppendLine();

                // First 256 bytes hex dump
                sb.AppendLine("=== FIRST 256 BYTES ===");
                sb.AppendLine(GenerateHexDump(0, 256));

                // Magic number analysis
                sb.AppendLine("=== POTENTIAL MAGIC NUMBERS ===");
                var magics = FindPotentialMagicNumbers();
                foreach (var magic in magics.Take(15))
                {
                    sb.AppendLine($"  {magic.AsString,-12} - {magic.Offsets.Count} occurrences at offsets: {string.Join(", ", magic.Offsets.Take(5).Select(o => $"0x{o:X}"))}...");
                }
                sb.AppendLine();

                // Frame analysis
                sb.AppendLine("=== POTENTIAL FRAME BOUNDARIES ===");
                var frames = FindPotentialFrames();
                sb.AppendLine($"Found {frames.Count} potential frames");
                foreach (var frame in frames.Take(10))
                {
                    sb.AppendLine($"  Offset 0x{frame.Offset:X8}, Length {frame.Length}, Header: {BitConverter.ToString(frame.HeaderBytes.Take(8).ToArray())}");
                }
            }
            else
            {
                sb.AppendLine("No decompressed data available!");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Export decompressed data to file for external analysis
        /// </summary>
        public void ExportDecompressedData(string outputPath)
        {
            if (DecompressedData == null)
                throw new InvalidOperationException("No decompressed data available");

            File.WriteAllBytes(outputPath, DecompressedData);
        }

        public class PatternMatch
        {
            public byte[] Pattern { get; set; }
            public List<int> Offsets { get; set; }
            public int Occurrences { get; set; }
        }

        public class PotentialFrame
        {
            public int Offset { get; set; }
            public int Length { get; set; }
            public byte[] HeaderBytes { get; set; }
        }

        public class MagicCandidate
        {
            public uint Value { get; set; }
            public List<int> Offsets { get; set; }
            public string AsString { get; set; }
        }
    }
}
