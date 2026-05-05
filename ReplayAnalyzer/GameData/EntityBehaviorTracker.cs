using System;
using System.Collections.Generic;
using System.Linq;
using ReplayAnalyzerTool.Models;

namespace ReplayAnalyzerTool.GameData;

/// <summary>
/// Tracks entity behavior over time to build profiles for hero identification
/// and ability detection
/// </summary>
public class EntityBehaviorTracker
{
    private readonly Dictionary<uint, TrackedEntity> _trackedEntities = new();
    private readonly Dictionary<uint, List<EntityStateSnapshot>> _stateHistory = new();

    public void ProcessTick(TickData tick)
    {
        foreach (var entity in tick.Entities)
        {
            ProcessEntity(entity, (int)tick.TickNumber);
        }
    }

    private void ProcessEntity(EntityState entity, int tickNumber)
    {
        uint entityIndex = (uint)entity.Index;

        // Initialize tracking if needed
        if (!_trackedEntities.ContainsKey(entityIndex))
        {
            _trackedEntities[entityIndex] = new TrackedEntity
            {
                EntityIndex = entityIndex,
                FirstSeenTick = tickNumber
            };
            _stateHistory[entityIndex] = new List<EntityStateSnapshot>();
        }

        var tracked = _trackedEntities[entityIndex];
        tracked.LastSeenTick = tickNumber;
        tracked.TotalSamples++;

        // Create state snapshot
        var snapshot = new EntityStateSnapshot
        {
            Tick = tickNumber,
            HasPosition = entity.Position != null,
            Position = entity.Position.HasValue ? new Position { X = entity.Position.Value.X, Y = entity.Position.Value.Y, Z = entity.Position.Value.Z } : null,
            HasRotation = entity.Rotation != null,
            Rotation = entity.Rotation.HasValue ? new Rotation { Pitch = entity.Rotation.Value.X, Yaw = entity.Rotation.Value.Y, Roll = entity.Rotation.Value.Z } : null,
            DataSize = entity.RawData?.Length ?? 0,
            EntityType = (uint)entity.Type
        };

        // Calculate velocity if we have previous position
        var history = _stateHistory[entityIndex];
        if (history.Count > 0 && snapshot.HasPosition && history.Last().HasPosition)
        {
            var prev = history.Last();
            var dx = (snapshot.Position?.X ?? 0) - (prev.Position?.X ?? 0);
            var dy = (snapshot.Position?.Y ?? 0) - (prev.Position?.Y ?? 0);
            var dz = (snapshot.Position?.Z ?? 0) - (prev.Position?.Z ?? 0);
            var dt = snapshot.Tick - prev.Tick;

            if (dt > 0)
            {
                var distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                snapshot.Speed = distance / dt;
                snapshot.VerticalSpeed = MathF.Abs(dz) / dt;

                // Calculate horizontal distance only
                var horizontalDist = MathF.Sqrt(dx * dx + dy * dy);

                // Teleport detection is very strict:
                // Only count as teleport if it's a clear outlier compared to recent movement
                // This helps filter out gaps in position data being misread as teleports

                // Get average speed from recent history
                float recentAvgSpeed = 0;
                int recentSamples = 0;
                for (int i = Math.Max(0, history.Count - 10); i < history.Count; i++)
                {
                    if (history[i].HasPosition && history[i].Speed > 0 && history[i].Speed < 100)
                    {
                        recentAvgSpeed += history[i].Speed;
                        recentSamples++;
                    }
                }
                if (recentSamples > 0) recentAvgSpeed /= recentSamples;

                // Only count as teleport if:
                // 1. dt == 1 (truly consecutive ticks)
                // 2. Distance is in blink range (7-40 units)
                // 3. Speed is at least 3x recent average (clear outlier)
                // 4. Recent average speed exists (we have history)
                bool isConsecutive = dt == 1;
                bool isBlinkDistance = horizontalDist >= 7 && horizontalDist <= 40;
                bool isClearOutlier = recentSamples >= 5 && snapshot.Speed > recentAvgSpeed * 3 && recentAvgSpeed > 0.1f;
                bool isHorizontalMovement = horizontalDist > MathF.Abs(dz) * 2;

                if (isConsecutive && isBlinkDistance && isClearOutlier && isHorizontalMovement)
                {
                    snapshot.IsTeleport = true;
                    tracked.TeleportCount++;
                }

                // Track sustained flight/hover (high altitude for multiple ticks)
                if ((snapshot.Position?.Z ?? 0) > tracked.MinAltitude + 10)
                {
                    tracked.HighAltitudeSamples++;
                }
            }
        }

        history.Add(snapshot);

        // Keep history manageable
        if (history.Count > 1000)
        {
            history.RemoveRange(0, 100);
        }

        // Update tracked statistics
        if (snapshot.HasPosition)
        {
            tracked.SpeedSamples.Add(snapshot.Speed);
            if (tracked.SpeedSamples.Count > 500)
                tracked.SpeedSamples.RemoveRange(0, 100);

            var z = snapshot.Position?.Z ?? 0;
            if (z > tracked.MaxAltitude) tracked.MaxAltitude = z;
            if (tracked.MinAltitude == 0 || z < tracked.MinAltitude) tracked.MinAltitude = z;
        }

        tracked.DataSizeSamples.Add(snapshot.DataSize);
        if (tracked.DataSizeSamples.Count > 500)
            tracked.DataSizeSamples.RemoveRange(0, 100);
    }

    /// <summary>
    /// Get the behavior profile for an entity
    /// </summary>
    public EntityBehaviorProfile? GetProfile(uint entityIndex)
    {
        if (!_trackedEntities.TryGetValue(entityIndex, out var tracked))
            return null;

        var profile = new EntityBehaviorProfile
        {
            MaxAltitude = tracked.MaxAltitude,
            MinAltitude = tracked.MinAltitude,
            TeleportCount = tracked.TeleportCount,
            PositionSampleCount = tracked.SpeedSamples.Count
        };

        if (tracked.SpeedSamples.Count > 0)
        {
            profile.AverageSpeed = tracked.SpeedSamples.Average();
            profile.MaxSpeed = tracked.SpeedSamples.Max();
            profile.SpeedSamples = tracked.SpeedSamples.ToList();

            // Calculate high altitude ratio (% of time spent elevated)
            if (tracked.TotalSamples > 0)
            {
                profile.HighAltitudeRatio = (float)tracked.HighAltitudeSamples / tracked.TotalSamples;
            }

            // Calculate vertical movement ratio
            var verticalRange = tracked.MaxAltitude - tracked.MinAltitude;
            if (verticalRange > 5)
            {
                profile.VerticalMovementRatio = verticalRange / Math.Max(1, profile.AverageSpeed * 10);
            }
        }

        if (tracked.DataSizeSamples.Count > 0)
        {
            profile.AverageDataSize = (int)tracked.DataSizeSamples.Average();
        }

        return profile;
    }

    /// <summary>
    /// Get all tracked entity indices
    /// </summary>
    public IEnumerable<uint> GetTrackedEntities() => _trackedEntities.Keys;

    /// <summary>
    /// Get state history for an entity
    /// </summary>
    public IReadOnlyList<EntityStateSnapshot>? GetHistory(uint entityIndex)
    {
        return _stateHistory.TryGetValue(entityIndex, out var history) ? history : null;
    }

    /// <summary>
    /// Analyze entity for potential ability usage
    /// </summary>
    public List<DetectedAbility> DetectAbilities(uint entityIndex, string? heroName = null)
    {
        var abilities = new List<DetectedAbility>();

        if (!_stateHistory.TryGetValue(entityIndex, out var history) || history.Count < 2)
            return abilities;

        for (int i = 1; i < history.Count; i++)
        {
            var prev = history[i - 1];
            var curr = history[i];

            // Teleport ability detection
            if (curr.IsTeleport)
            {
                var ability = new DetectedAbility
                {
                    Tick = curr.Tick,
                    Type = AbilityType.Movement,
                    Name = GuessAbilityName(heroName, "teleport"),
                    Confidence = 0.7f
                };
                abilities.Add(ability);
            }

            // High vertical movement (jump/flight)
            if (curr.VerticalSpeed > 5f)
            {
                var ability = new DetectedAbility
                {
                    Tick = curr.Tick,
                    Type = AbilityType.Movement,
                    Name = GuessAbilityName(heroName, "vertical"),
                    Confidence = 0.5f
                };
                abilities.Add(ability);
            }

            // Speed burst detection
            if (i >= 2 && curr.Speed > history[i - 2].Speed * 2 && curr.Speed > 15)
            {
                var ability = new DetectedAbility
                {
                    Tick = curr.Tick,
                    Type = AbilityType.Movement,
                    Name = GuessAbilityName(heroName, "speed"),
                    Confidence = 0.4f
                };
                abilities.Add(ability);
            }
        }

        return abilities;
    }

    private string GuessAbilityName(string? heroName, string abilityType)
    {
        if (string.IsNullOrEmpty(heroName))
            return $"Unknown {abilityType} ability";

        return (heroName, abilityType) switch
        {
            ("Tracer", "teleport") => "Blink",
            ("Tracer", "speed") => "Blink",
            ("Sombra", "teleport") => "Translocator",
            ("Reaper", "teleport") => "Shadow Step",
            ("Moira", "teleport") => "Fade",
            ("Moira", "speed") => "Fade",
            ("Pharah", "vertical") => "Jump Jet",
            ("Echo", "vertical") => "Flight",
            ("Mercy", "vertical") => "Guardian Angel",
            ("Mercy", "speed") => "Guardian Angel",
            ("Genji", "vertical") => "Cyber-Agility",
            ("Genji", "speed") => "Swift Strike",
            ("Hanzo", "vertical") => "Wall Climb",
            ("Lucio", "speed") => "Speed Boost",
            ("Lucio", "vertical") => "Wall Ride",
            ("Soldier: 76", "speed") => "Sprint",
            ("D.Va", "speed") => "Boosters",
            ("D.Va", "vertical") => "Boosters",
            ("Winston", "vertical") => "Jump Pack",
            ("Reinhardt", "speed") => "Charge",
            ("Doomfist", "speed") => "Rocket Punch",
            ("Doomfist", "vertical") => "Seismic Slam",
            ("Wrecking Ball", "speed") => "Grappling Claw",
            ("Wrecking Ball", "vertical") => "Grappling Claw",
            _ => $"{heroName} {abilityType} ability"
        };
    }
}

public class TrackedEntity
{
    public uint EntityIndex { get; set; }
    public int FirstSeenTick { get; set; }
    public int LastSeenTick { get; set; }
    public int TotalSamples { get; set; }
    public float MaxAltitude { get; set; }
    public float MinAltitude { get; set; }
    public int TeleportCount { get; set; }
    public int HighAltitudeSamples { get; set; }
    public List<float> SpeedSamples { get; set; } = new();
    public List<int> DataSizeSamples { get; set; } = new();
}

public class EntityStateSnapshot
{
    public int Tick { get; set; }
    public bool HasPosition { get; set; }
    public Position? Position { get; set; }
    public bool HasRotation { get; set; }
    public Rotation? Rotation { get; set; }
    public int DataSize { get; set; }
    public uint EntityType { get; set; }
    public float Speed { get; set; }
    public float VerticalSpeed { get; set; }
    public bool IsTeleport { get; set; }
}

/// <summary>
/// Simple position for behavior tracking
/// </summary>
public class Position
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

/// <summary>
/// Simple rotation for behavior tracking
/// </summary>
public class Rotation
{
    public float Pitch { get; set; }
    public float Yaw { get; set; }
    public float Roll { get; set; }
}

public class DetectedAbility
{
    public int Tick { get; set; }
    public AbilityType Type { get; set; }
    public string Name { get; set; } = "";
    public float Confidence { get; set; }
}

public enum AbilityType
{
    Unknown,
    Movement,
    Attack,
    Defense,
    Healing,
    Ultimate
}
