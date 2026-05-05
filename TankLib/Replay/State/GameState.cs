using System.Collections.Generic;
using TankLib.Math;

namespace TankLib.Replay.State
{
    /// <summary>
    /// Represents the complete game state at a specific tick.
    /// Contains all player states, objective state, and match context.
    /// </summary>
    public class GameState
    {
        /// <summary>
        /// Current tick/frame number
        /// </summary>
        public uint TickNumber { get; set; }
        
        /// <summary>
        /// Timestamp in milliseconds from match start
        /// </summary>
        public ulong TimestampMs { get; set; }
        
        /// <summary>
        /// Game time in seconds (server time, accounts for pauses)
        /// </summary>
        public float GameTimeSeconds { get; set; }
        
        // === Match Context ===
        
        /// <summary>
        /// Map GUID
        /// </summary>
        public teResourceGUID MapGuid { get; set; }
        
        /// <summary>
        /// Map name (resolved)
        /// </summary>
        public string MapName { get; set; }
        
        /// <summary>
        /// Game mode GUID
        /// </summary>
        public teResourceGUID GameModeGuid { get; set; }
        
        /// <summary>
        /// Game mode name (resolved)
        /// </summary>
        public string GameModeName { get; set; }
        
        /// <summary>
        /// Current match phase
        /// </summary>
        public MatchPhase Phase { get; set; }
        
        /// <summary>
        /// Current round number (1-indexed)
        /// </summary>
        public int RoundNumber { get; set; }
        
        /// <summary>
        /// Is match in overtime
        /// </summary>
        public bool IsOvertime { get; set; }
        
        /// <summary>
        /// Time bank remaining for attacking team (if applicable)
        /// </summary>
        public float TimeBankSeconds { get; set; }
        
        // === Team Scores ===
        
        /// <summary>
        /// Blue team score (rounds won, points, etc)
        /// </summary>
        public int BlueTeamScore { get; set; }
        
        /// <summary>
        /// Red team score
        /// </summary>
        public int RedTeamScore { get; set; }
        
        // === Objective State ===
        
        /// <summary>
        /// Current objective state (payload, capture point, etc)
        /// </summary>
        public ObjectiveState Objective { get; set; } = new ObjectiveState();
        
        // === Players ===
        
        /// <summary>
        /// All player states indexed by slot
        /// </summary>
        public Dictionary<int, PlayerState> Players { get; set; } = new Dictionary<int, PlayerState>();
        
        /// <summary>
        /// Blue team players (convenience accessor)
        /// </summary>
        public List<PlayerState> BlueTeam { get; } = new List<PlayerState>();
        
        /// <summary>
        /// Red team players (convenience accessor)
        /// </summary>
        public List<PlayerState> RedTeam { get; } = new List<PlayerState>();
        
        // === Events This Tick ===
        
        /// <summary>
        /// Events that occurred during this tick
        /// </summary>
        public List<GameEvent> Events { get; set; } = new List<GameEvent>();
        
        /// <summary>
        /// Update team lists from player dictionary
        /// </summary>
        public void UpdateTeamLists()
        {
            BlueTeam.Clear();
            RedTeam.Clear();
            
            foreach (var player in Players.Values)
            {
                if (player.Team == TankLib.STU.Types.Enums.TeamIndex.TeamBlue)
                    BlueTeam.Add(player);
                else if (player.Team == TankLib.STU.Types.Enums.TeamIndex.TeamRed)
                    RedTeam.Add(player);
            }
        }
        
        /// <summary>
        /// Clone current state for history
        /// </summary>
        public GameState Clone()
        {
            var clone = new GameState
            {
                TickNumber = TickNumber,
                TimestampMs = TimestampMs,
                GameTimeSeconds = GameTimeSeconds,
                MapGuid = MapGuid,
                MapName = MapName,
                GameModeGuid = GameModeGuid,
                GameModeName = GameModeName,
                Phase = Phase,
                RoundNumber = RoundNumber,
                IsOvertime = IsOvertime,
                TimeBankSeconds = TimeBankSeconds,
                BlueTeamScore = BlueTeamScore,
                RedTeamScore = RedTeamScore,
                Objective = Objective.Clone(),
            };
            
            foreach (var kvp in Players)
                clone.Players[kvp.Key] = kvp.Value.Clone();
            
            clone.UpdateTeamLists();
            
            // Don't clone events - they're point-in-time
            
            return clone;
        }
    }
    
    public enum MatchPhase
    {
        Unknown = 0,
        Assemble = 1,       // Hero select / setup
        Setup = 2,          // Attackers waiting for gates
        InProgress = 3,     // Main gameplay
        Overtime = 4,       // Overtime period
        PostMatch = 5,      // POTG / stats
        BetweenRounds = 6,  // Side swap
    }
    
    /// <summary>
    /// Objective state (payload, capture point, control)
    /// </summary>
    public class ObjectiveState
    {
        /// <summary>
        /// Type of current objective
        /// </summary>
        public ObjectiveType Type { get; set; }
        
        /// <summary>
        /// Capture/push progress (0.0 - 1.0 per checkpoint)
        /// </summary>
        public float Progress { get; set; }
        
        /// <summary>
        /// Current checkpoint (0-indexed)
        /// </summary>
        public int Checkpoint { get; set; }
        
        /// <summary>
        /// Total checkpoints in this phase
        /// </summary>
        public int TotalCheckpoints { get; set; }
        
        /// <summary>
        /// Team currently in control (-1=contested, 0=blue, 1=red)
        /// </summary>
        public int ControllingTeam { get; set; }
        
        /// <summary>
        /// Is objective currently contested
        /// </summary>
        public bool IsContested { get; set; }
        
        /// <summary>
        /// Payload/robot position (if applicable)
        /// </summary>
        public teVec3 ObjectivePosition { get; set; }
        
        /// <summary>
        /// Number of attackers on payload/point
        /// </summary>
        public int AttackersOnObjective { get; set; }
        
        /// <summary>
        /// Number of defenders on payload/point
        /// </summary>
        public int DefendersOnObjective { get; set; }
        
        public ObjectiveState Clone()
        {
            return new ObjectiveState
            {
                Type = Type,
                Progress = Progress,
                Checkpoint = Checkpoint,
                TotalCheckpoints = TotalCheckpoints,
                ControllingTeam = ControllingTeam,
                IsContested = IsContested,
                ObjectivePosition = ObjectivePosition,
                AttackersOnObjective = AttackersOnObjective,
                DefendersOnObjective = DefendersOnObjective
            };
        }
    }
    
    public enum ObjectiveType
    {
        None = 0,
        Payload = 1,        // Escort
        CapturePoint = 2,   // Assault / Hybrid first point
        ControlPoint = 3,   // Control
        PushRobot = 4,      // Push
        Flag = 5,           // CTF
    }
    
    /// <summary>
    /// A game event (damage, kill, ability, etc)
    /// </summary>
    public class GameEvent
    {
        public uint TickNumber { get; set; }
        public ulong TimestampMs { get; set; }
        
        public GameEventType Type { get; set; }
        public uint EventHash { get; set; }  // Original hash for unknown events
        
        /// <summary>
        /// Source player slot (-1 if no source)
        /// </summary>
        public int SourceSlot { get; set; } = -1;
        
        /// <summary>
        /// Target player slot (-1 if no target)
        /// </summary>
        public int TargetSlot { get; set; } = -1;
        
        /// <summary>
        /// Ability used (if applicable)
        /// </summary>
        public string AbilityName { get; set; }
        
        public teResourceGUID AbilityGuid { get; set; }
        
        /// <summary>
        /// Primary value (damage amount, heal amount, etc)
        /// </summary>
        public float Value { get; set; }
        
        /// <summary>
        /// Was this a critical hit
        /// </summary>
        public bool WasCritical { get; set; }
        
        /// <summary>
        /// Additional event-specific data
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
    
    public enum GameEventType
    {
        Unknown = 0,
        
        // Combat
        Damage = 1,
        Healing = 2,
        Elimination = 3,
        Death = 4,
        Assist = 5,
        Resurrect = 6,
        
        // Abilities
        AbilityUsed = 10,
        AbilityEnded = 11,
        UltimateUsed = 12,
        UltimateEnded = 13,
        UltimateCharged = 14,
        
        // Status
        StatusApplied = 20,
        StatusRemoved = 21,
        
        // Objective
        ObjectiveCapturing = 30,
        ObjectiveCaptured = 31,
        ObjectiveLost = 32,
        PayloadMoving = 33,
        PayloadStopped = 34,
        CheckpointReached = 35,
        
        // Match
        RoundStart = 40,
        RoundEnd = 41,
        OvertimeStart = 42,
        OvertimeEnd = 43,
        MatchEnd = 44,
        
        // Player
        HeroSwap = 50,
        PlayerJoined = 51,
        PlayerLeft = 52,
        Respawn = 53,
    }
}
