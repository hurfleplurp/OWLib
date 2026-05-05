using System;
using System.Collections.Generic;
using System.Linq;
using ReplayAnalyzerTool.Models;

namespace ReplayAnalyzerTool.GameData;

/// <summary>
/// Tracks the complete game state across ticks, including players, heroes, abilities, and interactions.
/// This is the central integration point for all game data systems.
/// </summary>
public class GameStateTracker
{
    public List<PlayerTracker> Players { get; } = new();
    public List<GameInteraction> Interactions { get; } = new();
    public MapDefinition CurrentMap { get; private set; }
    public GameMode CurrentGameMode { get; private set; }
    public int CurrentTick { get; private set; }
    public float MatchTime { get; private set; }
    public bool IsInProgress { get; private set; }

    // Killfeed
    public List<KillfeedEntry> Killfeed { get; } = new();

    // Ultimate tracking
    public Dictionary<int, float> UltimateCharges { get; } = new();

    private readonly Dictionary<int, PlayerTracker> _entityToPlayer = new();

    public void SetMap(string mapGuid)
    {
        CurrentMap = MapDatabase.GetByGuid(mapGuid);
    }

    public void SetGameMode(GameMode mode)
    {
        CurrentGameMode = mode;
    }

    /// <summary>
    /// Create a new player tracker
    /// </summary>
    public PlayerTracker CreatePlayer(int playerId, int entityId = -1)
    {
        var player = new PlayerTracker
        {
            PlayerId = playerId,
            EntityId = entityId,
            PlayerName = $"Player-{playerId}"
        };
        Players.Add(player);

        if (entityId >= 0)
            _entityToPlayer[entityId] = player;

        return player;
    }

    /// <summary>
    /// Assign a hero to a player
    /// </summary>
    public void AssignHero(int playerId, string heroName)
    {
        var player = Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null) return;

        // Handle uncertain heroes (marked with "?")
        bool isUncertain = heroName.EndsWith("?");
        string lookupName = isUncertain ? heroName.TrimEnd('?') : heroName;

        var hero = HeroDatabase.GetByName(lookupName);
        if (hero != null)
        {
            player.CurrentHero = hero;
            player.IsHeroUncertain = isUncertain;
            player.AbilityStates.Clear();

            // Initialize ability states
            foreach (var ability in hero.Abilities)
            {
                player.AbilityStates[ability.Name] = new AbilityState
                {
                    Definition = ability,
                    CurrentCooldown = 0,
                    CurrentCharges = ability.Charges,
                    IsActive = false
                };
            }

            // Record the hero swap
            player.HeroHistory.Add(new HeroSwap
            {
                Tick = CurrentTick,
                HeroName = heroName,
                Role = hero.Role
            });
        }
    }

    /// <summary>
    /// Update the game state for a new tick
    /// </summary>
    public void ProcessTick(int tickNumber, float deltaTime)
    {
        CurrentTick = tickNumber;
        MatchTime += deltaTime;
        IsInProgress = true;

        // Update all cooldowns
        foreach (var player in Players)
        {
            foreach (var ability in player.AbilityStates.Values)
            {
                if (ability.CurrentCooldown > 0)
                {
                    ability.CurrentCooldown = Math.Max(0, ability.CurrentCooldown - deltaTime);
                    if (ability.CurrentCooldown == 0 && ability.CurrentCharges < ability.Definition.Charges)
                    {
                        ability.CurrentCharges++;
                    }
                }

                if (ability.IsActive && ability.ActiveDuration > 0)
                {
                    ability.ActiveDuration -= deltaTime;
                    if (ability.ActiveDuration <= 0)
                    {
                        ability.IsActive = false;
                    }
                }
            }

            // Update status effects
            for (int i = player.StatusEffects.Count - 1; i >= 0; i--)
            {
                player.StatusEffects[i].RemainingDuration -= deltaTime;
                if (player.StatusEffects[i].RemainingDuration <= 0)
                {
                    player.StatusEffects.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Record an ability use
    /// </summary>
    public GameInteraction RecordAbilityUse(int playerId, string abilityName, int? targetId = null)
    {
        var player = Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null) return null;

        var target = targetId.HasValue ? Players.FirstOrDefault(p => p.PlayerId == targetId.Value) : null;

        // Update ability state
        if (player.AbilityStates.TryGetValue(abilityName, out var abilityState))
        {
            if (abilityState.CurrentCharges > 0)
            {
                abilityState.CurrentCharges--;
                abilityState.CurrentCooldown = abilityState.Definition.Cooldown;

                if (abilityState.Definition.Duration > 0)
                {
                    abilityState.IsActive = true;
                    abilityState.ActiveDuration = abilityState.Definition.Duration;
                }
            }
        }

        var interaction = new GameInteraction
        {
            Tick = CurrentTick,
            Time = MatchTime,
            Type = InteractionType.AbilityUse,
            SourcePlayer = player,
            TargetPlayer = target,
            AbilityName = abilityName,
            Description = InteractionDescriber.DescribeAbilityUse(
                player.DisplayName,
                player.CurrentHero?.Name,
                abilityName,
                target?.DisplayName
            )
        };

        Interactions.Add(interaction);
        player.InteractionHistory.Add(interaction);

        return interaction;
    }

    /// <summary>
    /// Record an ultimate ability use
    /// </summary>
    public GameInteraction RecordUltimateUse(int playerId, string ultimateName)
    {
        var player = Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null) return null;

        player.UltimatesUsed++;

        var interaction = new GameInteraction
        {
            Tick = CurrentTick,
            Time = MatchTime,
            Type = InteractionType.UltimateUse,
            SourcePlayer = player,
            AbilityName = ultimateName,
            Description = InteractionDescriber.DescribeUltimateUse(
                player.DisplayName,
                player.CurrentHero?.Name,
                ultimateName
            )
        };

        Interactions.Add(interaction);
        player.InteractionHistory.Add(interaction);

        return interaction;
    }

    /// <summary>
    /// Record a kill
    /// </summary>
    public GameInteraction? RecordKill(int killerId, int victimId, string? weaponOrAbility = null, bool isCritical = false, bool isEnvironmental = false)
    {
        var killer = Players.FirstOrDefault(p => p.PlayerId == killerId);
        var victim = Players.FirstOrDefault(p => p.PlayerId == victimId);

        if (killer == null || victim == null) return null;

        // Update stats
        killer.Eliminations++;
        if (isCritical) killer.CriticalKills++;
        victim.Deaths++;

        var weapon = weaponOrAbility ?? killer.CurrentHero?.GetPrimary()?.Name ?? "weapon";

        var interaction = new GameInteraction
        {
            Tick = CurrentTick,
            Time = MatchTime,
            Type = InteractionType.Kill,
            SourcePlayer = killer,
            TargetPlayer = victim,
            AbilityName = weapon,
            IsCritical = isCritical,
            IsEnvironmental = isEnvironmental,
            Description = InteractionDescriber.DescribeKill(
                killer.DisplayName,
                killer.CurrentHero?.Name,
                victim.DisplayName,
                victim.CurrentHero?.Name,
                weapon,
                isCritical,
                isEnvironmental
            )
        };

        Interactions.Add(interaction);
        killer.InteractionHistory.Add(interaction);
        victim.InteractionHistory.Add(interaction);

        // Add to killfeed
        Killfeed.Add(new KillfeedEntry
        {
            Tick = CurrentTick,
            KillerName = killer.DisplayName,
            KillerHero = killer.CurrentHero?.Name,
            VictimName = victim.DisplayName,
            VictimHero = victim.CurrentHero?.Name,
            Weapon = weapon,
            IsCritical = isCritical
        });

        return interaction;
    }

    /// <summary>
    /// Record a death (possibly without a killer - suicide/environment)
    /// </summary>
    public GameInteraction RecordDeath(int victimId, int? killerId = null, string cause = null)
    {
        var victim = Players.FirstOrDefault(p => p.PlayerId == victimId);
        if (victim == null) return null;

        victim.Deaths++;

        var killer = killerId.HasValue ? Players.FirstOrDefault(p => p.PlayerId == killerId.Value) : null;
        if (killer != null) killer.Eliminations++;

        var interaction = new GameInteraction
        {
            Tick = CurrentTick,
            Time = MatchTime,
            Type = InteractionType.Death,
            SourcePlayer = killer,
            TargetPlayer = victim,
            AbilityName = cause,
            Description = InteractionDescriber.DescribeDeath(
                victim.DisplayName,
                victim.CurrentHero?.Name,
                killer?.DisplayName,
                killer?.CurrentHero?.Name,
                cause
            )
        };

        Interactions.Add(interaction);
        victim.InteractionHistory.Add(interaction);

        return interaction;
    }

    /// <summary>
    /// Record a resurrection
    /// </summary>
    public GameInteraction RecordResurrection(int healerId, int targetId)
    {
        var healer = Players.FirstOrDefault(p => p.PlayerId == healerId);
        var target = Players.FirstOrDefault(p => p.PlayerId == targetId);

        if (healer == null || target == null) return null;

        var interaction = new GameInteraction
        {
            Tick = CurrentTick,
            Time = MatchTime,
            Type = InteractionType.Resurrection,
            SourcePlayer = healer,
            TargetPlayer = target,
            Description = InteractionDescriber.DescribeResurrection(
                healer.DisplayName,
                healer.CurrentHero?.Name,
                target.DisplayName,
                target.CurrentHero?.Name
            )
        };

        Interactions.Add(interaction);
        healer.InteractionHistory.Add(interaction);
        target.InteractionHistory.Add(interaction);

        return interaction;
    }

    /// <summary>
    /// Record an ultimate use
    /// </summary>
    public GameInteraction? RecordUltimate(int playerId)
    {
        var player = Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null) return null;

        var ultAbility = player.CurrentHero?.GetUltimate();

        player.UltimatesUsed++;
        UltimateCharges[playerId] = 0;

        var interaction = new GameInteraction
        {
            Tick = CurrentTick,
            Time = MatchTime,
            Type = InteractionType.UltimateUse,
            SourcePlayer = player,
            AbilityName = ultAbility?.Name ?? "Ultimate",
            Description = InteractionDescriber.DescribeUltimate(
                player.DisplayName,
                player.CurrentHero?.Name,
                ultAbility?.Name ?? "Ultimate"
            )
        };

        Interactions.Add(interaction);
        player.InteractionHistory.Add(interaction);

        return interaction;
    }

    /// <summary>
    /// Apply a status effect to a player
    /// </summary>
    public void ApplyStatusEffect(int playerId, StatusEffectType effectType, float duration, int? sourceId = null)
    {
        var player = Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null) return;

        var source = sourceId.HasValue ? Players.FirstOrDefault(p => p.PlayerId == sourceId.Value) : null;

        player.StatusEffects.Add(new ActiveStatusEffect
        {
            Type = effectType,
            RemainingDuration = duration,
            Source = source
        });

        var interaction = new GameInteraction
        {
            Tick = CurrentTick,
            Time = MatchTime,
            Type = InteractionType.StatusEffect,
            SourcePlayer = source,
            TargetPlayer = player,
            Description = InteractionDescriber.DescribeStatusEffect(
                player.DisplayName,
                effectType.ToString(),
                duration
            )
        };

        Interactions.Add(interaction);
    }

    /// <summary>
    /// Record objective progress
    /// </summary>
    public GameInteraction RecordObjectiveEvent(ObjectiveEventType eventType, int? playerId = null, float progress = 0)
    {
        var player = playerId.HasValue ? Players.FirstOrDefault(p => p.PlayerId == playerId.Value) : null;

        var interaction = new GameInteraction
        {
            Tick = CurrentTick,
            Time = MatchTime,
            Type = InteractionType.ObjectiveEvent,
            SourcePlayer = player,
            Description = InteractionDescriber.DescribeObjective(
                eventType,
                player?.DisplayName,
                player?.CurrentHero?.Name
            )
        };

        Interactions.Add(interaction);
        return interaction;
    }

    /// <summary>
    /// Get player by entity ID
    /// </summary>
    public PlayerTracker GetPlayerByEntity(int entityId)
    {
        return _entityToPlayer.TryGetValue(entityId, out var player) ? player : null;
    }

    /// <summary>
    /// Get the POV player (entity 0)
    /// </summary>
    public PlayerTracker? GetPovPlayer()
    {
        return Players.FirstOrDefault(p => p.IsPov);
    }

    /// <summary>
    /// Get recent interactions for display
    /// </summary>
    public IEnumerable<GameInteraction> GetRecentInteractions(int count = 10)
    {
        return Interactions.TakeLast(count);
    }

    /// <summary>
    /// Get interactions at a specific tick
    /// </summary>
    public IEnumerable<GameInteraction> GetInteractionsAtTick(int tick)
    {
        return Interactions.Where(i => i.Tick == tick);
    }

    /// <summary>
    /// Generate a summary of the match so far
    /// </summary>
    public string GenerateMatchSummary()
    {
        var lines = new List<string>();

        lines.Add($"=== Match Summary ===");
        if (CurrentMap != null)
            lines.Add($"Map: {CurrentMap.Name} ({CurrentMap.Type})");
        lines.Add($"Duration: {MatchTime:F1}s ({CurrentTick} ticks)");
        lines.Add("");

        lines.Add("--- Player Stats ---");
        foreach (var player in Players.OrderByDescending(p => p.Eliminations))
        {
            var heroName = player.CurrentHero?.Name ?? "Unknown";
            lines.Add($"{player.DisplayName} ({heroName}): {player.Eliminations}E / {player.Deaths}D / {player.UltimatesUsed}Q");
        }

        lines.Add("");
        lines.Add($"--- Last {Math.Min(5, Killfeed.Count)} Kills ---");
        foreach (var kill in Killfeed.TakeLast(5))
        {
            lines.Add($"  [{kill.Tick}] {kill.KillerName}({kill.KillerHero}) → {kill.VictimName}({kill.VictimHero}) with {kill.Weapon}");
        }

        return string.Join("\n", lines);
    }
}

/// <summary>
/// Tracks a single player throughout the match
/// </summary>
public class PlayerTracker
{
    public int PlayerId { get; set; }
    public int EntityId { get; set; }
    public string PlayerName { get; set; }
    public int Team { get; set; } // 0 = unknown, 1 = team1, 2 = team2
    public bool IsPov { get; set; } // True if this is the POV player (entity 0)
    public HeroDefinition CurrentHero { get; set; }
    public bool IsHeroUncertain { get; set; } // True if hero was guessed from behavior
    public List<HeroSwap> HeroHistory { get; } = new();
    public Dictionary<string, AbilityState> AbilityStates { get; } = new();
    public List<ActiveStatusEffect> StatusEffects { get; } = new();
    public List<GameInteraction> InteractionHistory { get; } = new();

    // Stats
    public int Eliminations { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int CriticalKills { get; set; }
    public int UltimatesUsed { get; set; }
    public float DamageDealt { get; set; }
    public float HealingDone { get; set; }
    public float DamageTaken { get; set; }
    public float ObjectiveTime { get; set; }

    public string DisplayName => CurrentHero != null
        ? $"{PlayerName} ({CurrentHero.Name})"
        : PlayerName;

    public string RoleIcon => CurrentHero?.Role switch
    {
        HeroRole.Tank => "🛡️",
        HeroRole.Damage => "⚔️",
        HeroRole.Support => "💚",
        _ => "❓"
    };
}

public class HeroSwap
{
    public int Tick { get; set; }
    public string HeroName { get; set; }
    public HeroRole Role { get; set; }
}

public class AbilityState
{
    public AbilityDefinition Definition { get; set; }
    public float CurrentCooldown { get; set; }
    public int CurrentCharges { get; set; }
    public bool IsActive { get; set; }
    public float ActiveDuration { get; set; }

    public bool IsReady => CurrentCharges > 0;
    public float CooldownPercent => Definition.Cooldown > 0
        ? (Definition.Cooldown - CurrentCooldown) / Definition.Cooldown
        : 1f;
}

public class ActiveStatusEffect
{
    public StatusEffectType Type { get; set; }
    public float RemainingDuration { get; set; }
    public PlayerTracker Source { get; set; }
}

public enum StatusEffectType
{
    None,
    Stunned,
    Frozen,
    Slept,
    Hacked,
    AntiHealed,
    Burning,
    Poisoned,
    Slowed,
    Rooted,
    Silenced,
    Discorded,
    NanoBoost,
    Amplified,
    Immortal,
    Phased,
    Invisible
}

/// <summary>
/// Represents a single interaction between players or with the game
/// </summary>
public class GameInteraction
{
    public int Tick { get; set; }
    public float Time { get; set; }
    public InteractionType Type { get; set; }
    public PlayerTracker SourcePlayer { get; set; }
    public PlayerTracker TargetPlayer { get; set; }
    public string AbilityName { get; set; }
    public string Description { get; set; }
    public bool IsCritical { get; set; }
    public bool IsEnvironmental { get; set; }

    public string Icon => Type switch
    {
        InteractionType.Kill => "💀",
        InteractionType.Death => "☠️",
        InteractionType.AbilityUse => "✨",
        InteractionType.UltimateUse => "⚡",
        InteractionType.Resurrection => "🔄",
        InteractionType.StatusEffect => "🎯",
        InteractionType.Healing => "💚",
        InteractionType.Damage => "💥",
        InteractionType.ObjectiveEvent => "🏁",
        InteractionType.HeroSwap => "🔀",
        _ => "❔"
    };

    public string Color => Type switch
    {
        InteractionType.Kill => "#e74c3c",
        InteractionType.Death => "#c0392b",
        InteractionType.UltimateUse => "#f39c12",
        InteractionType.Resurrection => "#2ecc71",
        InteractionType.Healing => "#27ae60",
        InteractionType.ObjectiveEvent => "#3498db",
        _ => "#95a5a6"
    };
}

public enum InteractionType
{
    Kill,
    Death,
    Damage,
    Healing,
    AbilityUse,
    UltimateUse,
    Resurrection,
    StatusEffect,
    ObjectiveEvent,
    HeroSwap,
    Respawn
}

public enum ObjectiveEventType
{
    PointCapturing,
    PointCaptured,
    PointLost,
    PayloadMoving,
    PayloadStalled,
    PayloadCheckpoint,
    PayloadDelivered,
    RobotContested,
    FlashpointCapturing,
    ClashPointFlipped,
    Overtime,
    RoundEnd,
    MatchEnd
}

public class KillfeedEntry
{
    public int Tick { get; set; }
    public string KillerName { get; set; }
    public string KillerHero { get; set; }
    public string VictimName { get; set; }
    public string VictimHero { get; set; }
    public string Weapon { get; set; }
    public bool IsCritical { get; set; }
    public bool IsAssist { get; set; }
}

public enum GameMode
{
    Unknown,
    QuickPlay,
    Competitive,
    Arcade,
    Custom,
    Practice
}
