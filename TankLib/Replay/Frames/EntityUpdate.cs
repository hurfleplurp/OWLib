using System.Runtime.InteropServices;
using TankLib.Math;

namespace TankLib.Replay.Frames
{
    /// <summary>
    /// Represents an entity state update within a replay frame.
    /// Can be either a full snapshot or a delta update.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EntityUpdate
    {
        /// <summary>
        /// Entity identifier (player slot, NPC ID, or object ID)
        /// </summary>
        public ushort EntityId;
        
        /// <summary>
        /// Entity type discriminator
        /// </summary>
        public EntityType Type;
        
        /// <summary>
        /// Update flags indicating which fields are present
        /// </summary>
        public EntityUpdateFlags UpdateFlags;
    }
    
    public enum EntityType : byte
    {
        Player = 0,
        Projectile = 1,
        Deployable = 2,      // Turrets, traps, etc.
        Payload = 3,
        CapturePoint = 4,
        EnvironmentObject = 5,
        Unknown = 255
    }
    
    [System.Flags]
    public enum EntityUpdateFlags : byte
    {
        None = 0,
        Position = 0x01,
        Rotation = 0x02,
        Velocity = 0x04,
        Health = 0x08,
        Status = 0x10,
        Abilities = 0x20,
        Ultimate = 0x40,
        All = 0xFF
    }
    
    /// <summary>
    /// Full entity position/orientation state
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EntityTransform
    {
        public teVec3 Position;
        public teVec3 Rotation;  // Euler angles or look direction
        public teVec3 Velocity;
    }
    
    /// <summary>
    /// Entity health state
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EntityHealth
    {
        public ushort CurrentHealth;
        public ushort MaxHealth;
        public ushort CurrentArmor;
        public ushort MaxArmor;
        public ushort CurrentShields;
        public ushort MaxShields;
        public ushort OverHealth;  // Temporary health from abilities
    }
    
    /// <summary>
    /// Player-specific state data
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PlayerEntityData
    {
        /// <summary>
        /// Hero GUID index (maps to hero type)
        /// </summary>
        public uint HeroId;
        
        /// <summary>
        /// Team index (0=Blue, 1=Red, 4=FFA)
        /// </summary>
        public byte TeamIndex;
        
        /// <summary>
        /// Player slot in team (0-5)
        /// </summary>
        public byte SlotIndex;
        
        /// <summary>
        /// Is player alive
        /// </summary>
        public byte IsAlive;
        
        /// <summary>
        /// Is player connected
        /// </summary>
        public byte IsConnected;
        
        /// <summary>
        /// Ultimate charge (0-10000 = 0%-100%)
        /// </summary>
        public ushort UltimateCharge;
        
        /// <summary>
        /// Current primary resource (ammo, etc)
        /// </summary>
        public ushort PrimaryResource;
        
        /// <summary>
        /// On-fire meter (0-10000)
        /// </summary>
        public ushort OnFireMeter;
        
        /// <summary>
        /// Respawn timer in ticks (0 if alive)
        /// </summary>
        public ushort RespawnTimer;
        
        /// <summary>
        /// Active status effect flags
        /// </summary>
        public StatusEffectFlags StatusEffects;
    }
    
    [System.Flags]
    public enum StatusEffectFlags : uint
    {
        None = 0,
        Stunned = 0x0001,
        Frozen = 0x0002,
        Slept = 0x0004,
        Hacked = 0x0008,
        AntiHealed = 0x0010,
        Burning = 0x0020,
        Poisoned = 0x0040,
        Discorded = 0x0080,
        NanoBoost = 0x0100,
        Amplified = 0x0200,
        Immortal = 0x0400,
        Phased = 0x0800,       // Invulnerable/intangible (Reaper wraith, Moira fade)
        Rooted = 0x1000,       // Cannot move
        Silenced = 0x2000,     // Cannot use abilities
        Invisible = 0x4000,
        Resurrecting = 0x8000,
    }
    
    /// <summary>
    /// Compact delta update for position only (most common)
    /// Uses quantized values to save space
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PositionDelta
    {
        public ushort EntityId;
        public short DeltaX;  // Quantized: actual = value / 100.0
        public short DeltaY;
        public short DeltaZ;
        public short DeltaYaw;  // Quantized angle
    }
}
