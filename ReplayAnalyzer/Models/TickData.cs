using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ReplayAnalyzerTool.Models;

/// <summary>
/// Standardized tick data format for ML training.
/// Each tick represents one game simulation frame (~16.67ms at 60fps).
/// </summary>
public class TickData
{
    /// <summary>Absolute tick number within the match</summary>
    public uint TickNumber { get; set; }

    /// <summary>Time delta since last tick in seconds</summary>
    public float DeltaTime { get; set; }

    /// <summary>Cumulative time from first tick in this segment</summary>
    public float CumulativeTime { get; set; }

    /// <summary>Entity states for this tick</summary>
    public List<EntityState> Entities { get; set; } = new();

    /// <summary>Events that occurred this tick (kills, ults, objectives, etc.)</summary>
    public List<GameEvent> Events { get; set; } = new();

    /// <summary>Raw frame data for debugging/further analysis</summary>
    [JsonIgnore]
    public byte[] RawData { get; set; } = Array.Empty<byte>();

    /// <summary>Hex representation of first 64 bytes for inspection</summary>
    public string RawDataPreview => RawData.Length > 0
        ? BitConverter.ToString(RawData, 0, Math.Min(64, RawData.Length)).Replace("-", "")
        : "";

    /// <summary>Whether this tick has any detected events</summary>
    public bool HasEvents => Events.Count > 0;
}

/// <summary>
/// State of a single entity (player, projectile, objective, etc.)
/// </summary>
public class EntityState
{
    /// <summary>Entity index within the tick (0-based)</summary>
    public int Index { get; set; }

    /// <summary>Tracked entity ID (consistent across ticks)</summary>
    public int TrackedId { get; set; }

    /// <summary>Human-readable entity name</summary>
    public string Name { get; set; } = "";

    /// <summary>Tick number this state belongs to</summary>
    public uint TickNumber { get; set; }

    /// <summary>Entity type if identifiable</summary>
    public EntityType Type { get; set; } = EntityType.Unknown;

    /// <summary>Team index (0=spectator, 1=team1, 2=team2)</summary>
    public int Team { get; set; }

    /// <summary>Position in world coordinates</summary>
    public Vector3? Position { get; set; }

    /// <summary>Rotation/facing direction</summary>
    public Vector3? Rotation { get; set; }

    /// <summary>Velocity if detectable</summary>
    public Vector3? Velocity { get; set; }

    /// <summary>Health percentage (0-1)</summary>
    public float? Health { get; set; }

    /// <summary>Ultimate charge (0-1)</summary>
    public float? UltCharge { get; set; }

    /// <summary>Hero ID if this is a player</summary>
    public uint? HeroId { get; set; }

    /// <summary>Current ability/state flags</summary>
    public uint StateFlags { get; set; }

    /// <summary>Raw entity data bytes</summary>
    [JsonIgnore]
    public byte[] RawData { get; set; } = Array.Empty<byte>();

    /// <summary>Size of raw data in bytes</summary>
    public int RawDataSize => RawData.Length;
}

/// <summary>
/// A game event that occurred during a tick
/// </summary>
public class GameEvent
{
    public GameEventType Type { get; set; }
    public uint TickNumber { get; set; }
    public float Time { get; set; }
    public uint? SourceEntityIndex { get; set; }
    public uint? TargetEntityIndex { get; set; }
    public string? Detail { get; set; }
    public float? Value { get; set; }

    /// <summary>CSS color for timeline visualization</summary>
    [JsonIgnore]
    public string Color => Type switch
    {
        GameEventType.SegmentStart => "#00ff00",
        GameEventType.EntitySpawn => "#00d9ff",
        GameEventType.EntityDespawn => "#ff6b6b",
        GameEventType.MassSpawn => "#00ffaa",
        GameEventType.MassDespawn => "#ff3333",
        GameEventType.Teleport => "#ffaa00",
        GameEventType.TimingAnomaly => "#ff00ff",
        GameEventType.TickJump => "#ffff00",
        GameEventType.DataSizeChange => "#aaaaff",
        GameEventType.Kill => "#ff0000",
        GameEventType.Death => "#880000",
        GameEventType.UltimateUsed => "#ffd700",
        GameEventType.ObjectiveCaptured => "#00ff88",
        _ => "#888888"
    };

    /// <summary>Icon for timeline visualization</summary>
    [JsonIgnore]
    public string Icon => Type switch
    {
        GameEventType.SegmentStart => "▶",
        GameEventType.EntitySpawn => "+",
        GameEventType.EntityDespawn => "-",
        GameEventType.MassSpawn => "++",
        GameEventType.MassDespawn => "--",
        GameEventType.Teleport => "⚡",
        GameEventType.TimingAnomaly => "⏱",
        GameEventType.TickJump => "↷",
        GameEventType.DataSizeChange => "📊",
        GameEventType.Kill => "💀",
        GameEventType.Death => "☠",
        GameEventType.UltimateUsed => "⭐",
        GameEventType.ObjectiveCaptured => "🏁",
        _ => "•"
    };
}

public enum EntityType
{
    Unknown = 0,
    Player = 1,
    PlayerPOV = 2,      // The recording player's entity
    Projectile = 3,
    ObjectivePoint = 4,
    Payload = 5,
    Deployable = 6,     // turrets, barriers, etc.
    HealthPack = 7,
    Environment = 8,
    Effect = 9          // visual effects, particles
}

public enum GameEventType
{
    Unknown = 0,
    // Detection-based events
    EntitySpawn = 1,
    EntityDespawn = 2,
    MassSpawn = 3,
    MassDespawn = 4,
    Teleport = 5,
    TimingAnomaly = 6,
    TickJump = 7,
    DataSizeChange = 8,
    SegmentStart = 9,
    SegmentEnd = 10,
    // Game events (future - require deeper parsing)
    Kill = 20,
    Death = 21,
    Assist = 22,
    UltimateUsed = 23,
    UltimateCharged = 24,
    ObjectiveCaptured = 25,
    ObjectiveContested = 26,
    PayloadMoved = 27,
    RoundStart = 28,
    RoundEnd = 29,
    MatchStart = 30,
    MatchEnd = 31,
    Respawn = 32,
    AbilityUsed = 33,
    DamageTaken = 34,
    HealingReceived = 35
}

/// <summary>
/// Simple 3D vector for positions/rotations
/// </summary>
public struct Vector3
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public Vector3(float x, float y, float z)
    {
        X = x; Y = y; Z = z;
    }

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";

    public float Magnitude => MathF.Sqrt(X * X + Y * Y + Z * Z);

    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
}

/// <summary>
/// Metadata about a parsed replay segment
/// </summary>
public class ReplaySegmentInfo
{
    public string SourceFile { get; set; } = "";
    public string MapGuid { get; set; } = "";
    public string GameModeGuid { get; set; } = "";
    public uint PlayerId { get; set; }
    public uint BuildNumber { get; set; }
    public byte FormatVersion { get; set; }

    public uint StartTick { get; set; }
    public uint EndTick { get; set; }
    public float TotalDuration { get; set; }
    public int TickCount { get; set; }

    public int MinEntityCount { get; set; }
    public int MaxEntityCount { get; set; }

    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;

    public List<PlayerInfo> Players { get; set; } = new();

    /// <summary>Summary of tracked entities across the replay</summary>
    public List<TrackedEntitySummary> TrackedEntities { get; set; } = new();

    /// <summary>Total number of detected events</summary>
    public int TotalEvents { get; set; }

    /// <summary>Event type counts</summary>
    public Dictionary<string, int> EventSummary { get; set; } = new();
}

public class PlayerInfo
{
    public string Name { get; set; } = "";
    public string HeroGuid { get; set; } = "";
    public int TeamIndex { get; set; }
    public int SlotIndex { get; set; }
}

/// <summary>
/// Summary of a tracked entity across the replay
/// </summary>
public class TrackedEntitySummary
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public uint FirstSeenTick { get; set; }
    public uint LastSeenTick { get; set; }
    public int TicksPresent { get; set; }
    public int PositionSamples { get; set; }

    /// <summary>Percentage of ticks this entity was present</summary>
    public float PresenceRatio => TicksPresent > 0 && LastSeenTick > FirstSeenTick
        ? (float)TicksPresent / (LastSeenTick - FirstSeenTick + 1) * 100
        : 100f;
}
