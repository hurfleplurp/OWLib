using System.Collections.Generic;
using TankLib.Math;
using TankLib.Replay.Frames;
using TankLib.STU.Types.Enums;

namespace TankLib.Replay.State
{
    /// <summary>
    /// Represents the complete state of a player at a specific tick.
    /// Designed for ML training with full decision context.
    /// </summary>
    public class PlayerState
    {
        /// <summary>
        /// Player slot index (0-11 typically)
        /// </summary>
        public int SlotIndex { get; set; }
        
        /// <summary>
        /// Team index (Blue=0, Red=1, FFA=4)
        /// </summary>
        public TeamIndex Team { get; set; }
        
        /// <summary>
        /// Hero GUID
        /// </summary>
        public teResourceGUID HeroGuid { get; set; }
        
        /// <summary>
        /// Hero name (resolved from GUID)
        /// </summary>
        public string HeroName { get; set; }
        
        /// <summary>
        /// Player name (if available)
        /// </summary>
        public string PlayerName { get; set; }
        
        // === Transform ===
        
        /// <summary>
        /// World position
        /// </summary>
        public teVec3 Position { get; set; }
        
        /// <summary>
        /// Look direction (normalized)
        /// </summary>
        public teVec3 Facing { get; set; }
        
        /// <summary>
        /// Current velocity
        /// </summary>
        public teVec3 Velocity { get; set; }
        
        // === Health ===
        
        /// <summary>
        /// Current health pool state
        /// </summary>
        public HealthState Health { get; set; } = new HealthState();
        
        /// <summary>
        /// Is player currently alive
        /// </summary>
        public bool IsAlive { get; set; }
        
        /// <summary>
        /// Respawn timer in seconds (0 if alive)
        /// </summary>
        public float RespawnTimer { get; set; }
        
        // === Resources ===
        
        /// <summary>
        /// Ultimate charge (0.0 - 1.0)
        /// </summary>
        public float UltimateCharge { get; set; }
        
        /// <summary>
        /// Is ultimate ready to use
        /// </summary>
        public bool UltimateReady => UltimateCharge >= 1.0f;
        
        /// <summary>
        /// Primary resource (ammo, etc) as percentage
        /// </summary>
        public float PrimaryResource { get; set; }
        
        /// <summary>
        /// On-fire meter (0.0 - 1.0)
        /// </summary>
        public float OnFireMeter { get; set; }
        
        // === Abilities ===
        
        /// <summary>
        /// State of each ability (keyed by ability name/slot)
        /// </summary>
        public Dictionary<string, AbilityState> Abilities { get; set; } = new Dictionary<string, AbilityState>();
        
        // === Status Effects ===
        
        /// <summary>
        /// Currently active status effects
        /// </summary>
        public List<ActiveStatusEffect> StatusEffects { get; set; } = new List<ActiveStatusEffect>();
        
        /// <summary>
        /// Quick access to status flags
        /// </summary>
        public StatusEffectFlags StatusFlags { get; set; }
        
        // === Decision Context ===
        
        /// <summary>
        /// List of actions available to this player right now
        /// </summary>
        public List<string> AvailableActions { get; set; } = new List<string>();
        
        /// <summary>
        /// Compute available actions based on current state
        /// </summary>
        public void ComputeAvailableActions()
        {
            AvailableActions.Clear();
            
            if (!IsAlive)
            {
                // Dead players can only spectate
                return;
            }
            
            // Movement (unless rooted)
            if ((StatusFlags & StatusEffectFlags.Rooted) == 0 &&
                (StatusFlags & StatusEffectFlags.Stunned) == 0)
            {
                AvailableActions.Add("Move");
                AvailableActions.Add("Jump");
                AvailableActions.Add("Crouch");
            }
            
            // Primary fire (unless silenced)
            if ((StatusFlags & StatusEffectFlags.Silenced) == 0 &&
                (StatusFlags & StatusEffectFlags.Hacked) == 0 &&
                (StatusFlags & StatusEffectFlags.Stunned) == 0)
            {
                if (PrimaryResource > 0)
                    AvailableActions.Add("PrimaryFire");
                
                AvailableActions.Add("SecondaryFire");
                AvailableActions.Add("Melee");
                
                // Check each ability
                foreach (var kvp in Abilities)
                {
                    if (kvp.Value.IsAvailable)
                    {
                        AvailableActions.Add(kvp.Key);
                    }
                }
                
                // Ultimate
                if (UltimateReady)
                {
                    AvailableActions.Add("Ultimate");
                }
            }
            
            // Reload (always possible if not full)
            if (PrimaryResource < 1.0f)
            {
                AvailableActions.Add("Reload");
            }
        }
        
        /// <summary>
        /// Clone current state for history tracking
        /// </summary>
        public PlayerState Clone()
        {
            var clone = new PlayerState
            {
                SlotIndex = SlotIndex,
                Team = Team,
                HeroGuid = HeroGuid,
                HeroName = HeroName,
                PlayerName = PlayerName,
                Position = Position,
                Facing = Facing,
                Velocity = Velocity,
                Health = Health.Clone(),
                IsAlive = IsAlive,
                RespawnTimer = RespawnTimer,
                UltimateCharge = UltimateCharge,
                PrimaryResource = PrimaryResource,
                OnFireMeter = OnFireMeter,
                StatusFlags = StatusFlags,
            };
            
            foreach (var kvp in Abilities)
                clone.Abilities[kvp.Key] = kvp.Value.Clone();
            
            foreach (var effect in StatusEffects)
                clone.StatusEffects.Add(effect.Clone());
            
            clone.AvailableActions.AddRange(AvailableActions);
            
            return clone;
        }
    }
    
    /// <summary>
    /// Health pool state
    /// </summary>
    public class HealthState
    {
        public float Current { get; set; }
        public float Max { get; set; }
        public float Armor { get; set; }
        public float MaxArmor { get; set; }
        public float Shields { get; set; }
        public float MaxShields { get; set; }
        public float OverHealth { get; set; }  // Temporary HP
        
        public float TotalCurrent => Current + Armor + Shields + OverHealth;
        public float TotalMax => Max + MaxArmor + MaxShields;
        public float Percentage => TotalMax > 0 ? TotalCurrent / TotalMax : 0;
        
        public HealthState Clone()
        {
            return new HealthState
            {
                Current = Current,
                Max = Max,
                Armor = Armor,
                MaxArmor = MaxArmor,
                Shields = Shields,
                MaxShields = MaxShields,
                OverHealth = OverHealth
            };
        }
    }
    
    /// <summary>
    /// State of a single ability
    /// </summary>
    public class AbilityState
    {
        public string Name { get; set; }
        public teResourceGUID AbilityGuid { get; set; }
        public LoadoutCategory Category { get; set; }
        
        /// <summary>
        /// Is ability currently available to use
        /// </summary>
        public bool IsAvailable => !IsOnCooldown && Charges > 0 && !IsActive;
        
        /// <summary>
        /// Is ability currently on cooldown
        /// </summary>
        public bool IsOnCooldown { get; set; }
        
        /// <summary>
        /// Remaining cooldown in seconds
        /// </summary>
        public float CooldownRemaining { get; set; }
        
        /// <summary>
        /// Total cooldown duration
        /// </summary>
        public float CooldownDuration { get; set; }
        
        /// <summary>
        /// Current charges available
        /// </summary>
        public int Charges { get; set; }
        
        /// <summary>
        /// Maximum charges
        /// </summary>
        public int MaxCharges { get; set; }
        
        /// <summary>
        /// Is ability currently active/channeling
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Duration remaining if active
        /// </summary>
        public float ActiveDuration { get; set; }
        
        /// <summary>
        /// Resource associated with this ability (if any)
        /// </summary>
        public float Resource { get; set; }
        
        public float MaxResource { get; set; }
        
        public AbilityState Clone()
        {
            return new AbilityState
            {
                Name = Name,
                AbilityGuid = AbilityGuid,
                Category = Category,
                IsOnCooldown = IsOnCooldown,
                CooldownRemaining = CooldownRemaining,
                CooldownDuration = CooldownDuration,
                Charges = Charges,
                MaxCharges = MaxCharges,
                IsActive = IsActive,
                ActiveDuration = ActiveDuration,
                Resource = Resource,
                MaxResource = MaxResource
            };
        }
    }
    
    /// <summary>
    /// Active status effect on a player
    /// </summary>
    public class ActiveStatusEffect
    {
        public StatusEffectFlags Type { get; set; }
        public string SourceAbility { get; set; }
        public int SourceEntityId { get; set; }
        public float DurationRemaining { get; set; }
        public float TotalDuration { get; set; }
        public float Strength { get; set; }  // For stackable effects
        
        public ActiveStatusEffect Clone()
        {
            return new ActiveStatusEffect
            {
                Type = Type,
                SourceAbility = SourceAbility,
                SourceEntityId = SourceEntityId,
                DurationRemaining = DurationRemaining,
                TotalDuration = TotalDuration,
                Strength = Strength
            };
        }
    }
}
