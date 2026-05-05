using System;
using System.Collections.Generic;
using System.Linq;

namespace ReplayAnalyzerTool.GameData;

/// <summary>
/// Maps hero GUIDs from replay data to hero names.
/// Since we don't have access to game data files, this uses:
/// 1. Known GUID mappings from community research
/// 2. Entity pattern analysis to identify heroes
/// 3. Learning from replay data over time
/// </summary>
public static class HeroGuidMapper
{
    // Known hero GUID mappings (Index portion only - the unique part)
    // These are extracted from community research and replay analysis
    // Format: Index (hex) -> Hero Name
    private static readonly Dictionary<uint, string> _knownHeroIndices = new()
    {
        // These indices are from the hero type (0x75) resources
        // The full GUID format is: {Index}.075
        // Example: 000000000002.075 would be index 0x2

        // Tank heroes (typical indices in the 0x1-0x20 range)
        { 0x02, "Reinhardt" },
        { 0x07, "D.Va" },
        { 0x08, "Winston" },
        { 0x09, "Roadhog" },
        { 0x0A, "Zarya" },
        { 0x13, "Orisa" },
        { 0x1A, "Wrecking Ball" },
        { 0x1F, "Sigma" },
        { 0x29, "Doomfist" }, // Moved to Tank in OW2
        { 0x35, "Junker Queen" },
        { 0x38, "Ramattra" },
        { 0x3C, "Mauga" },

        // Damage heroes
        { 0x01, "Tracer" },
        { 0x03, "Pharah" },
        { 0x04, "Reaper" },
        { 0x05, "Soldier: 76" },
        { 0x06, "Genji" },
        { 0x0B, "McCree" }, // Now Cassidy
        { 0x0C, "Hanzo" },
        { 0x0D, "Junkrat" },
        { 0x0E, "Torbjörn" },
        { 0x0F, "Widowmaker" },
        { 0x10, "Bastion" },
        { 0x11, "Mei" },
        { 0x15, "Sombra" },
        { 0x19, "Ashe" },
        { 0x1C, "Echo" },
        { 0x30, "Sojourn" },
        { 0x3A, "Venture" },

        // Support heroes
        { 0x12, "Mercy" },
        { 0x14, "Ana" },
        { 0x16, "Lúcio" },
        { 0x17, "Zenyatta" },
        { 0x18, "Moira" },
        { 0x1B, "Baptiste" },
        { 0x1D, "Brigitte" },
        { 0x20, "Kiriko" },
        { 0x34, "Lifeweaver" },
        { 0x37, "Illari" },
        { 0x3B, "Juno" },
    };

    // Alternative name mappings
    private static readonly Dictionary<string, string> _heroNameAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "McCree", "Cassidy" },
        { "Soldier: 76", "Soldier: 76" },
        { "D.Va", "D.Va" },
        { "Lúcio", "Lucio" },
        { "Torbjörn", "Torbjorn" },
    };

    // Learned mappings from replay data (populated at runtime)
    private static readonly Dictionary<string, string> _learnedMappings = new();

    // Cache of successful lookups
    private static readonly Dictionary<string, string> _lookupCache = new();

    /// <summary>
    /// Try to identify a hero from a GUID string
    /// </summary>
    public static string? GetHeroName(string guidString)
    {
        if (string.IsNullOrEmpty(guidString))
            return null;

        // Check cache first
        if (_lookupCache.TryGetValue(guidString, out var cached))
            return cached;

        // Try to parse the GUID
        string? heroName = null;

        // Format: {Index}.{Type} e.g., "000000000002.075"
        var parts = guidString.Split('.');
        if (parts.Length == 2)
        {
            // Check if it's a hero type (0x75 = 117 decimal, but shown as 075 hex)
            if (parts[1].Equals("075", StringComparison.OrdinalIgnoreCase) ||
                parts[1].Equals("75", StringComparison.OrdinalIgnoreCase))
            {
                // Try to parse the index
                if (uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out uint index))
                {
                    if (_knownHeroIndices.TryGetValue(index, out heroName))
                    {
                        // Normalize name
                        heroName = NormalizeHeroName(heroName);
                    }
                }
            }
        }

        // Try full GUID lookup in learned mappings
        if (heroName == null && _learnedMappings.TryGetValue(guidString.ToLowerInvariant(), out var learned))
        {
            heroName = learned;
        }

        // Cache the result
        if (heroName != null)
        {
            _lookupCache[guidString] = heroName;
        }

        return heroName;
    }

    /// <summary>
    /// Learn a GUID to hero name mapping from external data
    /// </summary>
    public static void LearnMapping(string guid, string heroName)
    {
        if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(heroName))
            return;

        var normalizedName = NormalizeHeroName(heroName);
        _learnedMappings[guid.ToLowerInvariant()] = normalizedName;
        _lookupCache[guid] = normalizedName;
    }

    /// <summary>
    /// Normalize hero names for consistency
    /// </summary>
    public static string NormalizeHeroName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Check aliases
        if (_heroNameAliases.TryGetValue(name, out var normalized))
            return normalized;

        return name;
    }

    /// <summary>
    /// Get all known hero mappings for debugging
    /// </summary>
    public static IReadOnlyDictionary<uint, string> GetKnownMappings() => _knownHeroIndices;

    /// <summary>
    /// Check if a GUID looks like a hero GUID (type 0x75)
    /// </summary>
    public static bool IsHeroGuid(string guidString)
    {
        if (string.IsNullOrEmpty(guidString))
            return false;

        var parts = guidString.Split('.');
        return parts.Length == 2 &&
               (parts[1].Equals("075", StringComparison.OrdinalIgnoreCase) ||
                parts[1].Equals("75", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extract the index from a GUID string
    /// </summary>
    public static uint? GetGuidIndex(string guidString)
    {
        if (string.IsNullOrEmpty(guidString))
            return null;

        var parts = guidString.Split('.');
        if (parts.Length >= 1 && uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out uint index))
        {
            return index;
        }

        return null;
    }

    /// <summary>
    /// Try to identify a hero from entity behavior patterns
    /// </summary>
    public static HeroIdentificationResult IdentifyFromBehavior(EntityBehaviorProfile profile)
    {
        var result = new HeroIdentificationResult();
        var candidates = new Dictionary<string, float>();

        // With stricter teleport detection (7-50 units horizontal in consecutive tick):
        // - Tracer blink: ~7m, she can blink 3 times quickly
        // - Moira fade: ~7m dash
        // - Sombra translocator: single long teleport

        // Frequent teleports (3+) in a clip strongly suggests Tracer
        if (profile.TeleportCount >= 5)
        {
            candidates["Tracer"] = 0.7f;
        }
        else if (profile.TeleportCount >= 3)
        {
            candidates["Tracer"] = 0.5f;
            candidates["Moira"] = 0.2f;
        }
        else if (profile.TeleportCount >= 1)
        {
            // Single teleport could be many heroes
            candidates["Sombra"] = 0.2f;
            candidates["Reaper"] = 0.2f;
            candidates["Moira"] = 0.15f;
            candidates["Kiriko"] = 0.15f;
        }

        // High sustained altitude - flight heroes
        // HighAltitudeRatio is % of time spent 10+ units above minimum
        if (profile.HighAltitudeRatio > 0.4f)
        {
            // Spends significant time in the air
            candidates["Pharah"] = candidates.GetValueOrDefault("Pharah") + 0.6f;
            candidates["Echo"] = candidates.GetValueOrDefault("Echo") + 0.5f;
            candidates["Mercy"] = candidates.GetValueOrDefault("Mercy") + 0.3f;
        }
        else if (profile.HighAltitudeRatio > 0.2f)
        {
            candidates["Pharah"] = candidates.GetValueOrDefault("Pharah") + 0.3f;
            candidates["Echo"] = candidates.GetValueOrDefault("Echo") + 0.3f;
            candidates["Mercy"] = candidates.GetValueOrDefault("Mercy") + 0.2f;
            candidates["Lucio"] = candidates.GetValueOrDefault("Lucio") + 0.15f;
        }
        else if (profile.HighAltitudeRatio > 0.1f)
        {
            // Some vertical play
            candidates["Lucio"] = candidates.GetValueOrDefault("Lucio") + 0.2f;
            candidates["Genji"] = candidates.GetValueOrDefault("Genji") + 0.15f;
            candidates["Hanzo"] = candidates.GetValueOrDefault("Hanzo") + 0.1f;
        }

        // High average speed (consistent mobility)
        if (profile.AverageSpeed > 15f)
        {
            candidates["Tracer"] = candidates.GetValueOrDefault("Tracer") + 0.15f;
            candidates["Genji"] = candidates.GetValueOrDefault("Genji") + 0.15f;
            candidates["Lucio"] = candidates.GetValueOrDefault("Lucio") + 0.2f;
        }
        else if (profile.AverageSpeed > 8f)
        {
            candidates["Soldier: 76"] = candidates.GetValueOrDefault("Soldier: 76") + 0.1f;
            candidates["Lucio"] = candidates.GetValueOrDefault("Lucio") + 0.1f;
        }
        else if (profile.AverageSpeed < 3f && profile.PositionSampleCount > 100)
        {
            // Low mobility - stationary heroes
            candidates["Ana"] = candidates.GetValueOrDefault("Ana") + 0.15f;
            candidates["Widowmaker"] = candidates.GetValueOrDefault("Widowmaker") + 0.1f;
            candidates["Bastion"] = candidates.GetValueOrDefault("Bastion") + 0.1f;
            candidates["Torbjorn"] = candidates.GetValueOrDefault("Torbjorn") + 0.1f;
        }

        // Very low vertical activity (ground-based tanks/supports)
        if (profile.HighAltitudeRatio < 0.05f)
        {
            candidates["Reinhardt"] = candidates.GetValueOrDefault("Reinhardt") + 0.15f;
            candidates["Roadhog"] = candidates.GetValueOrDefault("Roadhog") + 0.1f;
            candidates["Zarya"] = candidates.GetValueOrDefault("Zarya") + 0.1f;
            candidates["Ana"] = candidates.GetValueOrDefault("Ana") + 0.1f;
        }

        // Apply role-based grouping adjustments
        NormalizeCandidates(candidates);

        // Score and rank candidates
        if (candidates.Count > 0)
        {
            var best = candidates.OrderByDescending(c => c.Value).First();
            result.MostLikely = best.Key;
            result.Confidence = Math.Min(best.Value, 1f);
            result.AllCandidates = candidates;
        }

        return result;
    }

    /// <summary>
    /// Normalize candidate scores to reasonable confidence ranges
    /// </summary>
    private static void NormalizeCandidates(Dictionary<string, float> candidates)
    {
        if (candidates.Count == 0) return;

        // Find max score
        float maxScore = candidates.Values.Max();

        // If max is very high, cap it to prevent overconfidence
        if (maxScore > 1.0f)
        {
            float scale = 0.85f / maxScore;
            foreach (var key in candidates.Keys.ToList())
            {
                candidates[key] *= scale;
            }
        }
    }
}

/// <summary>
/// Profile of entity behavior for hero identification
/// </summary>
public class EntityBehaviorProfile
{
    public float AverageSpeed { get; set; }
    public float MaxSpeed { get; set; }
    public float MaxAltitude { get; set; }
    public float MinAltitude { get; set; }
    public float VerticalMovementRatio { get; set; }
    public float HighAltitudeRatio { get; set; }
    public int TeleportCount { get; set; }
    public int AverageDataSize { get; set; }
    public int PositionSampleCount { get; set; }
    public List<float> SpeedSamples { get; set; } = new();
}

/// <summary>
/// Result of hero identification from behavior
/// </summary>
public class HeroIdentificationResult
{
    public string? MostLikely { get; set; }
    public float Confidence { get; set; }
    public Dictionary<string, float> AllCandidates { get; set; } = new();
}
