using System.Runtime.InteropServices;
using TankLib.Helpers.DataSerializer;

namespace TankLib.Replay.Frames
{
    /// <summary>
    /// Represents the header of a replay frame/tick.
    /// Structure is preliminary and will be refined based on binary analysis.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FrameHeader
    {
        /// <summary>
        /// Frame/tick number within the replay
        /// </summary>
        public uint TickNumber;
        
        /// <summary>
        /// Timestamp in milliseconds from replay start
        /// </summary>
        public uint TimestampMs;
        
        /// <summary>
        /// Frame flags (content type indicators)
        /// </summary>
        public ushort Flags;
        
        /// <summary>
        /// Number of entity updates in this frame
        /// </summary>
        public ushort EntityUpdateCount;
        
        /// <summary>
        /// Number of events in this frame
        /// </summary>
        public ushort EventCount;
        
        /// <summary>
        /// Total size of frame data following header
        /// </summary>
        public ushort DataSize;
        
        // Frame flag bits (preliminary)
        public const ushort FLAG_KEYFRAME = 0x0001;      // Full state snapshot
        public const ushort FLAG_DELTA = 0x0002;         // Delta from previous frame
        public const ushort FLAG_HAS_EVENTS = 0x0004;    // Contains game events
        public const ushort FLAG_HAS_POSITIONS = 0x0008; // Contains position updates
        public const ushort FLAG_HAS_HEALTH = 0x0010;    // Contains health changes
        public const ushort FLAG_HAS_ABILITIES = 0x0020; // Contains ability state changes
        
        public bool IsKeyframe => (Flags & FLAG_KEYFRAME) != 0;
        public bool IsDelta => (Flags & FLAG_DELTA) != 0;
        public bool HasEvents => (Flags & FLAG_HAS_EVENTS) != 0;
        public bool HasPositions => (Flags & FLAG_HAS_POSITIONS) != 0;
        public bool HasHealth => (Flags & FLAG_HAS_HEALTH) != 0;
        public bool HasAbilities => (Flags & FLAG_HAS_ABILITIES) != 0;
    }
    
    /// <summary>
    /// Extended frame header for keyframes containing full state
    /// </summary>
    public class KeyframeHeader : ReadableData
    {
        public FrameHeader BaseHeader;
        
        /// <summary>
        /// Number of players in this frame
        /// </summary>
        public byte PlayerCount;
        
        /// <summary>
        /// Current game phase
        /// </summary>
        public byte GamePhase;
        
        /// <summary>
        /// Objective state identifier
        /// </summary>
        public ushort ObjectiveState;
        
        /// <summary>
        /// Game time in seconds (server time)
        /// </summary>
        public float GameTimeSeconds;
    }
}
