using System;
using System.Collections.Generic;
using TankLib.Replay.State;

namespace TankLib.Statescript
{
    /// <summary>
    /// Tracks ability state over time by interpreting Statescript graphs.
    /// Maintains cooldowns, charges, and active ability states for each player.
    /// </summary>
    public class AbilityTracker
    {
        private readonly Dictionary<int, PlayerAbilityContext> _playerContexts = new Dictionary<int, PlayerAbilityContext>();
        private readonly GraphLoader _graphLoader;

        public AbilityTracker(GraphLoader graphLoader = null)
        {
            _graphLoader = graphLoader ?? new GraphLoader();
        }

        /// <summary>
        /// Initialize ability tracking for a player with a specific hero
        /// </summary>
        public void InitializePlayer(int playerSlot, teResourceGUID heroGuid, HeroAbilityDefinition heroAbilities)
        {
            var context = new PlayerAbilityContext
            {
                PlayerSlot = playerSlot,
                HeroGuid = heroGuid,
                Abilities = new Dictionary<string, TrackedAbility>()
            };

            if (heroAbilities != null)
            {
                foreach (var ability in heroAbilities.Abilities)
                {
                    context.Abilities[ability.Name] = new TrackedAbility
                    {
                        Definition = ability,
                        State = AbilityStateType.Ready,
                        CurrentCharges = ability.MaxCharges,
                        CooldownRemaining = 0,
                        ResourceCurrent = ability.MaxResource
                    };
                }
            }

            _playerContexts[playerSlot] = context;
        }

        /// <summary>
        /// Update all ability states for a tick
        /// </summary>
        public void UpdateTick(float deltaTimeSeconds)
        {
            foreach (var context in _playerContexts.Values)
            {
                foreach (var ability in context.Abilities.Values)
                {
                    UpdateAbilityTick(ability, deltaTimeSeconds);
                }
            }
        }

        private void UpdateAbilityTick(TrackedAbility ability, float deltaTime)
        {
            // Update cooldown
            if (ability.CooldownRemaining > 0)
            {
                ability.CooldownRemaining -= deltaTime;
                if (ability.CooldownRemaining <= 0)
                {
                    ability.CooldownRemaining = 0;

                    // Restore a charge if below max
                    if (ability.CurrentCharges < ability.Definition.MaxCharges)
                    {
                        ability.CurrentCharges++;

                        // Start cooldown again if still below max
                        if (ability.CurrentCharges < ability.Definition.MaxCharges)
                        {
                            ability.CooldownRemaining = ability.Definition.CooldownSeconds;
                        }
                    }

                    // Transition from cooldown to ready if charges available
                    if (ability.State == AbilityStateType.OnCooldown && ability.CurrentCharges > 0)
                    {
                        ability.State = AbilityStateType.Ready;
                    }
                }
            }

            // Update active duration
            if (ability.ActiveDurationRemaining > 0)
            {
                ability.ActiveDurationRemaining -= deltaTime;
                if (ability.ActiveDurationRemaining <= 0)
                {
                    ability.ActiveDurationRemaining = 0;
                    ability.State = ability.CurrentCharges > 0 ? AbilityStateType.Ready : AbilityStateType.OnCooldown;
                }
            }

            // Update resource regeneration
            if (ability.ResourceCurrent < ability.Definition.MaxResource && ability.Definition.ResourceRegenRate > 0)
            {
                ability.ResourceCurrent = System.Math.Min(
                    ability.Definition.MaxResource,
                    ability.ResourceCurrent + ability.Definition.ResourceRegenRate * deltaTime
                );
            }
        }

        /// <summary>
        /// Record ability usage
        /// </summary>
        public void UseAbility(int playerSlot, string abilityName)
        {
            if (!_playerContexts.TryGetValue(playerSlot, out var context))
                return;

            if (!context.Abilities.TryGetValue(abilityName, out var ability))
                return;

            if (ability.CurrentCharges <= 0)
                return;

            ability.CurrentCharges--;
            ability.State = AbilityStateType.Active;
            ability.ActiveDurationRemaining = ability.Definition.DurationSeconds;

            // Start cooldown if this was last charge
            if (ability.CurrentCharges == 0 || ability.Definition.CooldownPerCharge)
            {
                ability.CooldownRemaining = ability.Definition.CooldownSeconds;
            }

            // Consume resource if applicable
            if (ability.Definition.ResourceCost > 0)
            {
                ability.ResourceCurrent -= ability.Definition.ResourceCost;
            }
        }

        /// <summary>
        /// Cancel an active ability
        /// </summary>
        public void CancelAbility(int playerSlot, string abilityName)
        {
            if (!_playerContexts.TryGetValue(playerSlot, out var context))
                return;

            if (!context.Abilities.TryGetValue(abilityName, out var ability))
                return;

            if (ability.State == AbilityStateType.Active)
            {
                ability.ActiveDurationRemaining = 0;
                ability.State = ability.CurrentCharges > 0 ? AbilityStateType.Ready : AbilityStateType.OnCooldown;
            }
        }

        /// <summary>
        /// Reset cooldown (from ability interactions)
        /// </summary>
        public void ResetCooldown(int playerSlot, string abilityName)
        {
            if (!_playerContexts.TryGetValue(playerSlot, out var context))
                return;

            if (!context.Abilities.TryGetValue(abilityName, out var ability))
                return;

            ability.CooldownRemaining = 0;
            ability.CurrentCharges = ability.Definition.MaxCharges;
            ability.State = AbilityStateType.Ready;
        }

        /// <summary>
        /// Get current ability states for a player
        /// </summary>
        public Dictionary<string, AbilityState> GetPlayerAbilityStates(int playerSlot)
        {
            var result = new Dictionary<string, AbilityState>();

            if (!_playerContexts.TryGetValue(playerSlot, out var context))
                return result;

            foreach (var kvp in context.Abilities)
            {
                result[kvp.Key] = new AbilityState
                {
                    Name = kvp.Key,
                    AbilityGuid = kvp.Value.Definition.Guid,
                    Category = kvp.Value.Definition.Category,
                    IsOnCooldown = kvp.Value.State == AbilityStateType.OnCooldown,
                    CooldownRemaining = kvp.Value.CooldownRemaining,
                    CooldownDuration = kvp.Value.Definition.CooldownSeconds,
                    Charges = kvp.Value.CurrentCharges,
                    MaxCharges = kvp.Value.Definition.MaxCharges,
                    IsActive = kvp.Value.State == AbilityStateType.Active,
                    ActiveDuration = kvp.Value.ActiveDurationRemaining,
                    Resource = kvp.Value.ResourceCurrent,
                    MaxResource = kvp.Value.Definition.MaxResource
                };
            }

            return result;
        }

        /// <summary>
        /// Clear all tracking
        /// </summary>
        public void Clear()
        {
            _playerContexts.Clear();
        }
    }

    internal class PlayerAbilityContext
    {
        public int PlayerSlot { get; set; }
        public teResourceGUID HeroGuid { get; set; }
        public Dictionary<string, TrackedAbility> Abilities { get; set; }
    }

    internal class TrackedAbility
    {
        public AbilityDefinition Definition { get; set; }
        public AbilityStateType State { get; set; }
        public int CurrentCharges { get; set; }
        public float CooldownRemaining { get; set; }
        public float ActiveDurationRemaining { get; set; }
        public float ResourceCurrent { get; set; }
    }

    internal enum AbilityStateType
    {
        Ready,
        Active,
        OnCooldown,
        Disabled
    }

    /// <summary>
    /// Definition of a hero's abilities loaded from game data
    /// </summary>
    public class HeroAbilityDefinition
    {
        public teResourceGUID HeroGuid { get; set; }
        public string HeroName { get; set; }
        public List<AbilityDefinition> Abilities { get; set; } = new List<AbilityDefinition>();
    }

    /// <summary>
    /// Definition of a single ability
    /// </summary>
    public class AbilityDefinition
    {
        public teResourceGUID Guid { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public TankLib.STU.Types.Enums.LoadoutCategory Category { get; set; }

        /// <summary>
        /// Cooldown in seconds
        /// </summary>
        public float CooldownSeconds { get; set; }

        /// <summary>
        /// Maximum charges
        /// </summary>
        public int MaxCharges { get; set; } = 1;

        /// <summary>
        /// Does cooldown start per charge or after all charges used
        /// </summary>
        public bool CooldownPerCharge { get; set; }

        /// <summary>
        /// Duration if ability has an active period
        /// </summary>
        public float DurationSeconds { get; set; }

        /// <summary>
        /// Resource cost per use
        /// </summary>
        public float ResourceCost { get; set; }

        /// <summary>
        /// Max resource pool
        /// </summary>
        public float MaxResource { get; set; }

        /// <summary>
        /// Resource regeneration per second
        /// </summary>
        public float ResourceRegenRate { get; set; }

        /// <summary>
        /// Ultimate cost (if this is an ultimate)
        /// </summary>
        public float UltimateCost { get; set; }

        /// <summary>
        /// Is this ability an ultimate
        /// </summary>
        public bool IsUltimate => Category == TankLib.STU.Types.Enums.LoadoutCategory.UltimateAbility;
    }
}
