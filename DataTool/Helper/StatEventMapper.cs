using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TankLib.Replay.State;

namespace DataTool.Helper
{
    /// <summary>
    /// Maps STUStatEvent hash values to human-readable event names.
    /// Supports both static mappings from CSV and runtime discovery.
    /// </summary>
    public static class StatEventMapper
    {
        private static Dictionary<uint, EventMapping> _hashToEvent;
        private static Dictionary<string, uint> _nameToHash;
        private static bool _initialized;
        
        /// <summary>
        /// Initialize the mapper with default mappings
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;
            
            _hashToEvent = new Dictionary<uint, EventMapping>();
            _nameToHash = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            
            // Load from embedded CSV
            LoadFromEmbeddedResource();
            
            // Add programmatic mappings from known STUStatEvent enum
            AddKnownEnumMappings();
            
            _initialized = true;
        }
        
        private static void LoadFromEmbeddedResource()
        {
            try
            {
                // Try to load from file path relative to assembly
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyDir = Path.GetDirectoryName(assembly.Location);
                var csvPath = Path.Combine(assemblyDir, "Static", "StatEventHashMapping.csv");
                
                if (File.Exists(csvPath))
                {
                    LoadFromCsv(csvPath);
                    return;
                }
                
                // Fallback: try current directory
                csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Static", "StatEventHashMapping.csv");
                if (File.Exists(csvPath))
                {
                    LoadFromCsv(csvPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load StatEventHashMapping.csv: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load mappings from a CSV file
        /// </summary>
        public static void LoadFromCsv(string filePath)
        {
            if (!File.Exists(filePath))
                return;
            
            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;
                
                var parts = line.Split(',');
                if (parts.Length < 2)
                    continue;
                
                if (!uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out var hash))
                    continue;
                
                var mapping = new EventMapping
                {
                    Hash = hash,
                    Name = parts[1].Trim(),
                    Category = parts.Length > 2 ? parts[2].Trim() : "Unknown",
                    Description = parts.Length > 3 ? parts[3].Trim() : ""
                };
                
                _hashToEvent[hash] = mapping;
                _nameToHash[mapping.Name] = hash;
            }
        }
        
        private static void AddKnownEnumMappings()
        {
            // Add mappings from STUPlayerStatEvent (these have known names)
            AddMapping(0x3A6D687B, "MatchEnter", "Match", "Player entered match");
            AddMapping(0xF669002F, "MatchWon", "Match", "Match won");
            AddMapping(0x69F07E27, "LeftMatch", "Match", "Player left match");
            AddMapping(0xF54EBBAB, "Disconnect", "Match", "Player disconnected");
            AddMapping(0xF428C120, "Reconnect", "Match", "Player reconnected");
            AddMapping(0x21B679C7, "OffenseMatchEnter", "Match", "Entered match on offense");
            AddMapping(0x1FB5608D, "OffenseMatchWon", "Match", "Won match on offense");
            AddMapping(0x25B1D62F, "DefenseMatchEnter", "Match", "Entered match on defense");
            AddMapping(0x75CB2306, "DefenseMatchWon", "Match", "Won match on defense");
            AddMapping(0xD631CF0E, "MatchTie", "Match", "Match ended in tie");
        }
        
        private static void AddMapping(uint hash, string name, string category, string description)
        {
            if (_hashToEvent.ContainsKey(hash))
                return;
            
            var mapping = new EventMapping
            {
                Hash = hash,
                Name = name,
                Category = category,
                Description = description
            };
            
            _hashToEvent[hash] = mapping;
            _nameToHash[name] = hash;
        }
        
        /// <summary>
        /// Get event name from hash
        /// </summary>
        public static string GetEventName(uint hash)
        {
            EnsureInitialized();
            return _hashToEvent.TryGetValue(hash, out var mapping) ? mapping.Name : $"Unknown_0x{hash:X8}";
        }
        
        /// <summary>
        /// Get full event mapping from hash
        /// </summary>
        public static EventMapping GetEventMapping(uint hash)
        {
            EnsureInitialized();
            return _hashToEvent.TryGetValue(hash, out var mapping) ? mapping : null;
        }
        
        /// <summary>
        /// Get hash from event name
        /// </summary>
        public static uint? GetEventHash(string name)
        {
            EnsureInitialized();
            return _nameToHash.TryGetValue(name, out var hash) ? hash : (uint?)null;
        }
        
        /// <summary>
        /// Convert hash to GameEventType enum
        /// </summary>
        public static GameEventType GetGameEventType(uint hash)
        {
            var name = GetEventName(hash);
            
            // Map known event names to enum
            return name switch
            {
                "Damage" => GameEventType.Damage,
                "Healing" or "SelfHealing" or "HealingReceived" => GameEventType.Healing,
                "Elimination" or "SoloKill" or "EnvironmentalKill" => GameEventType.Elimination,
                "Death" => GameEventType.Death,
                "Assist" => GameEventType.Assist,
                "Resurrect" => GameEventType.Resurrect,
                "AbilityUsed" => GameEventType.AbilityUsed,
                "AbilityEnded" => GameEventType.AbilityEnded,
                "UltimateUsed" => GameEventType.UltimateUsed,
                "UltimateEnded" => GameEventType.UltimateEnded,
                "UltimateCharged" or "UltimateProgress" => GameEventType.UltimateCharged,
                "StatusApplied" or "Stunned" or "Frozen" or "Slept" or "Hacked" or "Discorded" or "Nanoboosted" => GameEventType.StatusApplied,
                "StatusRemoved" => GameEventType.StatusRemoved,
                "ObjectiveCapturing" => GameEventType.ObjectiveCapturing,
                "ObjectiveCaptured" => GameEventType.ObjectiveCaptured,
                "ObjectiveLost" => GameEventType.ObjectiveLost,
                "PayloadProgress" => GameEventType.PayloadMoving,
                "CheckpointReached" => GameEventType.CheckpointReached,
                "MatchEnter" or "OffenseMatchEnter" or "DefenseMatchEnter" => GameEventType.RoundStart,
                "MatchWon" or "OffenseMatchWon" or "DefenseMatchWon" or "MatchTie" => GameEventType.MatchEnd,
                _ => GameEventType.Unknown
            };
        }
        
        /// <summary>
        /// Get all known mappings
        /// </summary>
        public static IEnumerable<EventMapping> GetAllMappings()
        {
            EnsureInitialized();
            return _hashToEvent.Values;
        }
        
        /// <summary>
        /// Get all unmapped hashes encountered
        /// </summary>
        public static HashSet<uint> UnmappedHashes { get; } = new HashSet<uint>();
        
        /// <summary>
        /// Record an unmapped hash for later analysis
        /// </summary>
        public static void RecordUnmappedHash(uint hash)
        {
            EnsureInitialized();
            if (!_hashToEvent.ContainsKey(hash))
            {
                UnmappedHashes.Add(hash);
            }
        }
        
        /// <summary>
        /// Export unmapped hashes for analysis
        /// </summary>
        public static void ExportUnmappedHashes(string filePath)
        {
            File.WriteAllLines(filePath, UnmappedHashes.Select(h => $"0x{h:X8}"));
        }
        
        private static void EnsureInitialized()
        {
            if (!_initialized)
                Initialize();
        }
    }
    
    /// <summary>
    /// Represents a mapping from hash to event information
    /// </summary>
    public class EventMapping
    {
        public uint Hash { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        
        public override string ToString() => $"{Name} (0x{Hash:X8}) [{Category}]";
    }
}
