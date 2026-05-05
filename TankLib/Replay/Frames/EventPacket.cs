using System.Runtime.InteropServices;

namespace TankLib.Replay.Frames
{
    /// <summary>
    /// Represents a game event within a replay frame.
    /// Events include damage, healing, kills, ability usage, etc.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EventPacket
    {
        /// <summary>
        /// Event type hash (maps to STUStatEvent or game event type)
        /// </summary>
        public uint EventTypeHash;
        
        /// <summary>
        /// Source entity ID (player/object causing the event)
        /// </summary>
        public ushort SourceEntityId;
        
        /// <summary>
        /// Target entity ID (player/object receiving the event)
        /// </summary>
        public ushort TargetEntityId;
        
        /// <summary>
        /// Ability identifier (GUID index for ability/weapon used)
        /// </summary>
        public uint AbilityId;
        
        /// <summary>
        /// Primary value (damage amount, healing amount, etc.)
        /// </summary>
        public int Value;
        
        /// <summary>
        /// Event-specific flags
        /// </summary>
        public EventFlags Flags;
    }
    
    [System.Flags]
    public enum EventFlags : ushort
    {
        None = 0,
        Critical = 0x0001,        // Headshot/critical hit
        Elimination = 0x0002,     // Kill/final blow
        Assist = 0x0004,          // Assist credit
        Environmental = 0x0008,   // Environmental kill
        SelfInflicted = 0x0010,   // Self damage
        Blocked = 0x0020,         // Blocked by shield/barrier
        Absorbed = 0x0040,        // Absorbed by ability (Defense Matrix, etc)
        Reflected = 0x0080,       // Reflected by ability (Genji deflect)
        Resurrect = 0x0100,       // Resurrection event
        UltimateUsed = 0x0200,    // Ultimate ability used
        UltimateCharge = 0x0400,  // Ultimate charge gained
    }
    
    /// <summary>
    /// Extended event data for damage events
    /// </summary>
    public struct DamageEventData
    {
        public EventPacket Base;
        
        /// <summary>
        /// Damage before mitigation
        /// </summary>
        public int RawDamage;
        
        /// <summary>
        /// Damage after armor reduction
        /// </summary>
        public int MitigatedDamage;
        
        /// <summary>
        /// Hit location (0=body, 1=head, 2=limb)
        /// </summary>
        public byte HitLocation;
        
        /// <summary>
        /// Distance from source to target
        /// </summary>
        public float Distance;
    }
    
    /// <summary>
    /// Extended event data for ability events
    /// </summary>
    public struct AbilityEventData
    {
        public EventPacket Base;
        
        /// <summary>
        /// Ability state transition type
        /// </summary>
        public AbilityEventType EventType;
        
        /// <summary>
        /// Remaining cooldown in milliseconds
        /// </summary>
        public ushort CooldownMs;
        
        /// <summary>
        /// Remaining charges
        /// </summary>
        public byte ChargesRemaining;
        
        /// <summary>
        /// Resource cost spent
        /// </summary>
        public ushort ResourceCost;
    }
    
    public enum AbilityEventType : byte
    {
        Started = 0,        // Ability activation started
        Cancelled = 1,      // Ability was cancelled
        Completed = 2,      // Ability finished executing
        CooldownStarted = 3,
        CooldownReset = 4,
        ChargeGained = 5,
        ChargeUsed = 6,
        ResourceSpent = 7,
    }
    
    /// <summary>
    /// Extended event data for elimination events
    /// </summary>
    public struct EliminationEventData
    {
        public EventPacket Base;
        
        /// <summary>
        /// Final blow damage
        /// </summary>
        public int FinalBlowDamage;
        
        /// <summary>
        /// Ability that dealt final blow
        /// </summary>
        public uint FinalBlowAbility;
        
        /// <summary>
        /// Kill streak of source player
        /// </summary>
        public byte KillStreak;
        
        /// <summary>
        /// Was this a shutdown (killed player on killstreak)
        /// </summary>
        public bool IsShutdown;
        
        /// <summary>
        /// Was victim using ultimate
        /// </summary>
        public bool WasUltShutdown;
    }
    
    /// <summary>
    /// Extended event data for objective events
    /// </summary>
    public struct ObjectiveEventData
    {
        public EventPacket Base;
        
        /// <summary>
        /// Objective type
        /// </summary>
        public ObjectiveType ObjectiveType;
        
        /// <summary>
        /// Progress value (0-10000)
        /// </summary>
        public ushort Progress;
        
        /// <summary>
        /// Owning team (0=contested, 1=blue, 2=red)
        /// </summary>
        public byte OwningTeam;
        
        /// <summary>
        /// Checkpoint/stage number
        /// </summary>
        public byte Checkpoint;
    }
    
    public enum ObjectiveType : byte
    {
        CapturePoint = 0,
        Payload = 1,
        ControlPoint = 2,
        Flag = 3,           // CTF
        Robot = 4,          // Push
    }
    
    /// <summary>
    /// Known event type hashes (will be populated through analysis)
    /// </summary>
    public static class KnownEventHashes
    {
        // These are placeholder values - actual hashes will be discovered through binary analysis
        // and correlation with STUStatEvent enum values
        
        // Combat events
        public const uint Damage = 0x00000001;          // Placeholder
        public const uint Healing = 0x00000002;
        public const uint Elimination = 0x00000003;
        public const uint Death = 0x00000004;
        public const uint Assist = 0x00000005;
        public const uint Resurrect = 0x00000006;
        
        // Ability events
        public const uint AbilityUsed = 0x00000010;
        public const uint UltimateUsed = 0x00000011;
        public const uint UltimateCharged = 0x00000012;
        
        // Status events
        public const uint StatusApplied = 0x00000020;
        public const uint StatusRemoved = 0x00000021;
        
        // Objective events
        public const uint ObjectiveCapture = 0x00000030;
        public const uint PayloadProgress = 0x00000031;
        public const uint CheckpointReached = 0x00000032;
        
        // Match events
        public const uint RoundStart = 0x00000040;
        public const uint RoundEnd = 0x00000041;
        public const uint OvertimeStart = 0x00000042;
        public const uint OvertimeEnd = 0x00000043;
        public const uint MatchComplete = 0x00000044;
    }
}
