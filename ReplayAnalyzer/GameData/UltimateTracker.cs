using System;
using System.Collections.Generic;
using System.Linq;
using ReplayAnalyzerTool.Models;

namespace ReplayAnalyzerTool.GameData;

/// <summary>
/// Tracks ultimate ability charge and usage for players
/// Ultimate detection is based on:
/// - Entity spawn patterns (many ults spawn unique entities)
/// - Multi-kill sequences (common ultimate outcome)
/// - Behavior anomalies (sudden position changes, invulnerability frames)
/// - Time-based estimation (ults charge over ~90-180 seconds typically)
/// </summary>
public class UltimateTracker
{
    private readonly Dictionary<int, PlayerUltimateState> _playerStates = new();
    private readonly List<DetectedUltimate> _detectedUltimates = new();

    // Ultimate signatures for detection
    private static readonly Dictionary<string, UltimateSignature> _ultimateSignatures = new()
    {
        // Damage ultimates with distinct signatures
        ["Tracer"] = new UltimateSignature
        {
            Name = "Pulse Bomb",
            SpawnsEntities = true,
            ExpectedEntityCount = 1,
            TypicalDuration = 60, // ticks (~1 second)
            CanCauseMultiKill = true,
            HasDistinctAnimation = true
        },
        ["Junkrat"] = new UltimateSignature
        {
            Name = "RIP-Tire",
            SpawnsEntities = true,
            ExpectedEntityCount = 1,
            TypicalDuration = 600, // 10 seconds
            CanCauseMultiKill = true,
            ControlledEntity = true
        },
        ["D.Va"] = new UltimateSignature
        {
            Name = "Self-Destruct",
            SpawnsEntities = true,
            ExpectedEntityCount = 1,
            TypicalDuration = 180, // 3 seconds
            CanCauseMultiKill = true,
            HasDistinctAnimation = true
        },
        ["Reaper"] = new UltimateSignature
        {
            Name = "Death Blossom",
            TypicalDuration = 180,
            CanCauseMultiKill = true,
            HasMovementRestriction = true
        },
        ["Pharah"] = new UltimateSignature
        {
            Name = "Barrage",
            TypicalDuration = 180,
            CanCauseMultiKill = true,
            HasMovementRestriction = true,
            RequiresAirborne = true
        },
        ["Soldier: 76"] = new UltimateSignature
        {
            Name = "Tactical Visor",
            TypicalDuration = 360, // 6 seconds
            HasAutoAim = true
        },
        ["Genji"] = new UltimateSignature
        {
            Name = "Dragonblade",
            TypicalDuration = 360,
            CanCauseMultiKill = true,
            HasDistinctAnimation = true,
            IncreasedMobility = true
        },
        ["Hanzo"] = new UltimateSignature
        {
            Name = "Dragonstrike",
            SpawnsEntities = true,
            ExpectedEntityCount = 2, // Two dragons
            TypicalDuration = 240,
            CanCauseMultiKill = true
        },
        ["Cassidy"] = new UltimateSignature
        {
            Name = "Deadeye",
            TypicalDuration = 360,
            HasMovementRestriction = true,
            CanCauseMultiKill = true
        },
        ["Bastion"] = new UltimateSignature
        {
            Name = "Configuration: Artillery",
            TypicalDuration = 480,
            SpawnsEntities = true,
            HasMovementRestriction = true
        },
        ["Widowmaker"] = new UltimateSignature
        {
            Name = "Infra-Sight",
            TypicalDuration = 900, // 15 seconds
            AffectsAllAllies = true
        },
        ["Sombra"] = new UltimateSignature
        {
            Name = "EMP",
            TypicalDuration = 6, // Instant
            AffectsArea = true,
            HasDistinctAnimation = true
        },
        ["Mei"] = new UltimateSignature
        {
            Name = "Blizzard",
            SpawnsEntities = true,
            ExpectedEntityCount = 1,
            TypicalDuration = 300,
            AffectsArea = true,
            CanCauseMultiKill = true
        },
        ["Torbjorn"] = new UltimateSignature
        {
            Name = "Overcharge",
            TypicalDuration = 600,
            HasDistinctAnimation = true
        },
        ["Echo"] = new UltimateSignature
        {
            Name = "Duplicate",
            TypicalDuration = 900,
            TransformsPlayer = true
        },
        ["Sojourn"] = new UltimateSignature
        {
            Name = "Overclock",
            TypicalDuration = 540,
            HasAutoAim = true
        },
        ["Venture"] = new UltimateSignature
        {
            Name = "Tectonic Shock",
            SpawnsEntities = true,
            TypicalDuration = 120,
            AffectsArea = true
        },

        // Tank ultimates
        ["Reinhardt"] = new UltimateSignature
        {
            Name = "Earthshatter",
            TypicalDuration = 6, // Instant stun
            AffectsArea = true,
            CanCauseMultiKill = true,
            HasDistinctAnimation = true
        },
        ["Winston"] = new UltimateSignature
        {
            Name = "Primal Rage",
            TypicalDuration = 600,
            TransformsPlayer = true,
            IncreasedMobility = true
        },
        ["Roadhog"] = new UltimateSignature
        {
            Name = "Whole Hog",
            TypicalDuration = 360,
            HasMovementRestriction = true,
            CanCauseMultiKill = true
        },
        ["Zarya"] = new UltimateSignature
        {
            Name = "Graviton Surge",
            SpawnsEntities = true,
            ExpectedEntityCount = 1,
            TypicalDuration = 240,
            AffectsArea = true,
            CanCauseMultiKill = true
        },
        ["Orisa"] = new UltimateSignature
        {
            Name = "Terra Surge",
            TypicalDuration = 240,
            AffectsArea = true,
            HasMovementRestriction = true
        },
        ["Sigma"] = new UltimateSignature
        {
            Name = "Gravitic Flux",
            TypicalDuration = 240,
            AffectsArea = true,
            CanCauseMultiKill = true,
            HasDistinctAnimation = true
        },
        ["Wrecking Ball"] = new UltimateSignature
        {
            Name = "Minefield",
            SpawnsEntities = true,
            ExpectedEntityCount = 15, // Many mines
            TypicalDuration = 1200, // Mines persist
            AffectsArea = true
        },
        ["Doomfist"] = new UltimateSignature
        {
            Name = "Meteor Strike",
            TypicalDuration = 240,
            HasDistinctAnimation = true,
            CanCauseMultiKill = true,
            HasInvulnerability = true
        },
        ["Junker Queen"] = new UltimateSignature
        {
            Name = "Rampage",
            TypicalDuration = 120,
            HasDistinctAnimation = true,
            AffectsArea = true
        },
        ["Ramattra"] = new UltimateSignature
        {
            Name = "Annihilation",
            TypicalDuration = 180, // Base, extends with damage
            AffectsArea = true,
            TransformsPlayer = true
        },
        ["Mauga"] = new UltimateSignature
        {
            Name = "Cage Fight",
            SpawnsEntities = true,
            TypicalDuration = 480,
            AffectsArea = true
        },

        // Support ultimates
        ["Mercy"] = new UltimateSignature
        {
            Name = "Valkyrie",
            TypicalDuration = 900,
            IncreasedMobility = true,
            AffectsAllAllies = true
        },
        ["Lucio"] = new UltimateSignature
        {
            Name = "Sound Barrier",
            TypicalDuration = 360,
            AffectsAllAllies = true,
            HasDistinctAnimation = true
        },
        ["Ana"] = new UltimateSignature
        {
            Name = "Nano Boost",
            TypicalDuration = 480,
            AffectsSingleAlly = true
        },
        ["Zenyatta"] = new UltimateSignature
        {
            Name = "Transcendence",
            TypicalDuration = 360,
            HasInvulnerability = true,
            AffectsAllAllies = true,
            IncreasedMobility = true
        },
        ["Moira"] = new UltimateSignature
        {
            Name = "Coalescence",
            TypicalDuration = 480,
            HasDistinctAnimation = true,
            HasMovementRestriction = true
        },
        ["Brigitte"] = new UltimateSignature
        {
            Name = "Rally",
            TypicalDuration = 600,
            AffectsAllAllies = true,
            IncreasedMobility = true
        },
        ["Baptiste"] = new UltimateSignature
        {
            Name = "Amplification Matrix",
            SpawnsEntities = true,
            ExpectedEntityCount = 1,
            TypicalDuration = 600,
            AffectsArea = true
        },
        ["Kiriko"] = new UltimateSignature
        {
            Name = "Kitsune Rush",
            SpawnsEntities = true,
            ExpectedEntityCount = 1,
            TypicalDuration = 600,
            AffectsArea = true
        },
        ["Lifeweaver"] = new UltimateSignature
        {
            Name = "Tree of Life",
            SpawnsEntities = true,
            ExpectedEntityCount = 1,
            TypicalDuration = 900,
            AffectsArea = true
        },
        ["Illari"] = new UltimateSignature
        {
            Name = "Captive Sun",
            SpawnsEntities = true,
            TypicalDuration = 180,
            AffectsArea = true,
            CanCauseMultiKill = true
        },
        ["Juno"] = new UltimateSignature
        {
            Name = "Orbital Ray",
            SpawnsEntities = true,
            TypicalDuration = 480,
            AffectsAllAllies = true
        }
    };

    public void InitializePlayer(int playerId, string? heroName)
    {
        _playerStates[playerId] = new PlayerUltimateState
        {
            PlayerId = playerId,
            HeroName = heroName,
            EstimatedCharge = 0,
            LastDeathTick = 0,
            LastKillTick = 0,
            UltimatesUsed = 0
        };
    }

    /// <summary>
    /// Process a tick to track ultimate charge and usage
    /// </summary>
    public void ProcessTick(TickData tick, GameStateTracker gameState)
    {
        int currentTick = (int)tick.TickNumber;

        foreach (var player in gameState.Players)
        {
            if (!_playerStates.TryGetValue(player.PlayerId, out var state))
            {
                InitializePlayer(player.PlayerId, player.CurrentHero?.Name);
                state = _playerStates[player.PlayerId];
            }

            // Update hero if changed
            if (player.CurrentHero?.Name != state.HeroName)
            {
                state.HeroName = player.CurrentHero?.Name;
                state.EstimatedCharge = 0; // Hero swap resets ult
            }

            // Estimate charge increase over time (~1.5% per second at 62.5 ticks/sec)
            if (state.LastProcessedTick > 0)
            {
                int tickDelta = currentTick - state.LastProcessedTick;
                state.EstimatedCharge += tickDelta * 0.024f; // ~1.5% per second
            }

            // Cap charge at 100%
            state.EstimatedCharge = Math.Min(state.EstimatedCharge, 100f);
            state.LastProcessedTick = currentTick;
        }
    }

    /// <summary>
    /// Record a kill event - increases ultimate charge
    /// </summary>
    public void RecordKill(int playerId, int tick, bool isFinalBlow = true)
    {
        if (!_playerStates.TryGetValue(playerId, out var state)) return;

        state.LastKillTick = tick;
        state.KillsSinceLastUlt++;

        // Kills give significant ult charge (~10-15% for final blow)
        state.EstimatedCharge += isFinalBlow ? 12f : 5f;
        state.EstimatedCharge = Math.Min(state.EstimatedCharge, 100f);
    }

    /// <summary>
    /// Record a death event - might indicate ult was used defensively
    /// </summary>
    public void RecordDeath(int playerId, int tick)
    {
        if (!_playerStates.TryGetValue(playerId, out var state)) return;

        state.LastDeathTick = tick;
        // Don't reset ult on death in OW2, but track for patterns
    }

    /// <summary>
    /// Try to detect ultimate usage from behavior patterns
    /// </summary>
    public DetectedUltimate? TryDetectUltimate(
        int playerId,
        int tick,
        EntityBehaviorProfile? profile,
        List<int> recentKillTicks,
        int entitySpawnCount,
        bool isPovPlayer = false)
    {
        if (!_playerStates.TryGetValue(playerId, out var state)) return null;
        if (string.IsNullOrEmpty(state.HeroName)) return null;

        // Prevent detecting same ult twice within 5 seconds (312 ticks)
        if (state.LastUltimateTick > 0 && tick - state.LastUltimateTick < 312) return null;

        // Get signature for this hero's ultimate
        if (!_ultimateSignatures.TryGetValue(state.HeroName, out var signature)) return null;

        float confidence = 0f;
        var reasons = new List<string>();

        // Base confidence from ult charge (higher = more likely to use ult)
        // For POV player in highlights, we can't accurately estimate charge
        bool likelyHasUlt = isPovPlayer || state.EstimatedCharge >= 80f;
        if (isPovPlayer)
        {
            // POV player in highlight - they likely had ult (that's why it's a highlight)
            confidence += 0.2f;
            reasons.Add("POV player (highlight moment)");
        }
        else if (state.EstimatedCharge >= 95f)
        {
            confidence += 0.15f;
            reasons.Add("Ult fully charged");
        }
        else if (state.EstimatedCharge >= 80f)
        {
            confidence += 0.1f;
            reasons.Add("Ult likely charged");
        }

        // ========== MULTI-KILL DETECTION ==========
        if (signature.CanCauseMultiKill && recentKillTicks.Count >= 2)
        {
            var killWindow = recentKillTicks.Max() - recentKillTicks.Min();
            if (killWindow <= 120) // Within 2 seconds
            {
                // Multi-kill is strong evidence
                confidence += 0.3f + (recentKillTicks.Count * 0.1f);
                reasons.Add($"Multi-kill ({recentKillTicks.Count} kills in {killWindow / 62.5f:F1}s)");
            }
        }

        // ========== SINGLE KILL WITH HIGH CHARGE (Solo Pick Ults) ==========
        // For ults like Pulse Bomb, EMP + kill, Widow headshot after Infra-Sight
        if (recentKillTicks.Count == 1 && likelyHasUlt)
        {
            // Check if this was a recent kill (within last 60 ticks = 1 second)
            if (tick - recentKillTicks[0] <= 60)
            {
                confidence += 0.15f;
                reasons.Add("Single elimination with ult ready");
            }
        }

        // ========== ENTITY SPAWN DETECTION ==========
        if (signature.SpawnsEntities)
        {
            if (entitySpawnCount >= signature.ExpectedEntityCount)
            {
                // Direct match to expected entity count
                confidence += 0.35f;
                reasons.Add($"Entity spawn ({entitySpawnCount} entities)");
            }
            else if (entitySpawnCount >= 3) // Generic entity burst
            {
                confidence += 0.2f;
                reasons.Add($"Entity burst ({entitySpawnCount} spawns)");
            }
        }
        else if (entitySpawnCount >= 5)
        {
            // Even non-entity ults, large spawns could indicate turret/trap combos
            confidence += 0.1f;
            reasons.Add($"Significant entity activity ({entitySpawnCount})");
        }

        // ========== MOVEMENT PATTERN DETECTION ==========
        if (profile != null && profile.PositionSampleCount >= 20)
        {
            // Movement restriction detection (channeled ults like Reaper, Pharah, Roadhog)
            if (signature.HasMovementRestriction)
            {
                if (profile.AverageSpeed < 2.0f)
                {
                    confidence += 0.2f;
                    reasons.Add("Movement restricted");
                }
            }

            // Increased mobility detection (Genji blade, Winston rage, Wrecking Ball)
            if (signature.IncreasedMobility)
            {
                if (profile.AverageSpeed > 15.0f || profile.MaxSpeed > 30.0f)
                {
                    confidence += 0.2f;
                    reasons.Add($"High mobility (avg: {profile.AverageSpeed:F1}, max: {profile.MaxSpeed:F1})");
                }
            }

            // Airborne requirement (Pharah Barrage)
            if (signature.RequiresAirborne)
            {
                if (profile.HighAltitudeRatio > 0.7f)
                {
                    confidence += 0.15f;
                    reasons.Add($"Airborne ({profile.HighAltitudeRatio:P0} high altitude)");
                }
            }
        }

        // ========== CONFIDENCE THRESHOLD ==========
        // Lower threshold when we have multiple weak signals, even lower for POV
        float requiredConfidence = isPovPlayer ? 0.35f : (reasons.Count >= 3 ? 0.4f : 0.5f);

        if (confidence >= requiredConfidence)
        {
            var detected = new DetectedUltimate
            {
                PlayerId = playerId,
                Tick = tick,
                HeroName = state.HeroName,
                UltimateName = signature.Name,
                Confidence = Math.Min(confidence, 1.0f),
                DetectionReasons = reasons
            };

            _detectedUltimates.Add(detected);

            // Update player state
            state.UltimatesUsed++;
            state.LastUltimateTick = tick;
            state.EstimatedCharge = 0; // Reset charge after ult
            state.KillsSinceLastUlt = 0;

            return detected;
        }

        return null;
    }

    /// <summary>
    /// Detect ultimate from entity spawn burst alone (for entity-heavy ults)
    /// Call this when a significant entity spawn is detected
    /// </summary>
    public DetectedUltimate? TryDetectFromEntitySpawn(int playerId, int tick, int entityCount, string? entityPattern = null)
    {
        if (!_playerStates.TryGetValue(playerId, out var state)) return null;
        if (string.IsNullOrEmpty(state.HeroName)) return null;

        // Prevent double detection
        if (state.LastUltimateTick > 0 && tick - state.LastUltimateTick < 312) return null;

        if (!_ultimateSignatures.TryGetValue(state.HeroName, out var signature)) return null;
        if (!signature.SpawnsEntities) return null;

        // Need enough entities and likely have ult
        if (entityCount < signature.ExpectedEntityCount) return null;
        if (state.EstimatedCharge < 60f) return null;

        var confidence = 0.4f;
        var reasons = new List<string>();

        if (entityCount == signature.ExpectedEntityCount)
        {
            confidence += 0.25f;
            reasons.Add($"Exact entity spawn match ({entityCount})");
        }
        else
        {
            confidence += 0.15f;
            reasons.Add($"Entity spawn ({entityCount} entities)");
        }

        if (state.EstimatedCharge >= 90f)
        {
            confidence += 0.15f;
            reasons.Add("Ult charged");
        }

        if (confidence >= 0.5f)
        {
            var detected = new DetectedUltimate
            {
                PlayerId = playerId,
                Tick = tick,
                HeroName = state.HeroName,
                UltimateName = signature.Name,
                Confidence = Math.Min(confidence, 0.85f),
                DetectionReasons = reasons
            };

            _detectedUltimates.Add(detected);
            state.UltimatesUsed++;
            state.LastUltimateTick = tick;
            state.EstimatedCharge = 0;

            return detected;
        }

        return null;
    }

    /// <summary>
    /// Detect ultimates from multi-kill sequences
    /// </summary>
    public DetectedUltimate? DetectFromMultiKill(int playerId, List<int> killTicks, int tick)
    {
        if (!_playerStates.TryGetValue(playerId, out var state)) return null;
        if (string.IsNullOrEmpty(state.HeroName)) return null;
        if (!_ultimateSignatures.TryGetValue(state.HeroName, out var signature)) return null;

        if (!signature.CanCauseMultiKill) return null;
        if (killTicks.Count < 2) return null;

        // Check kill timing
        var killWindow = killTicks.Max() - killTicks.Min();
        if (killWindow > 180) return null; // Must be within 3 seconds

        var confidence = 0.3f + (killTicks.Count * 0.15f); // More kills = higher confidence
        confidence = Math.Min(confidence, 0.9f);

        var detected = new DetectedUltimate
        {
            PlayerId = playerId,
            Tick = tick,
            HeroName = state.HeroName,
            UltimateName = signature.Name,
            Confidence = confidence,
            DetectionReasons = new List<string>
            {
                $"Multi-kill sequence: {killTicks.Count} eliminations"
            }
        };

        _detectedUltimates.Add(detected);
        state.UltimatesUsed++;
        state.LastUltimateTick = tick;
        state.EstimatedCharge = 0;
        state.KillsSinceLastUlt = 0;

        return detected;
    }

    /// <summary>
    /// Get all detected ultimates
    /// </summary>
    public IReadOnlyList<DetectedUltimate> GetDetectedUltimates() => _detectedUltimates;

    /// <summary>
    /// Get ultimate state for a player
    /// </summary>
    public PlayerUltimateState? GetPlayerState(int playerId)
    {
        return _playerStates.TryGetValue(playerId, out var state) ? state : null;
    }

    /// <summary>
    /// Get ultimate signature for a hero
    /// </summary>
    public static UltimateSignature? GetSignature(string heroName)
    {
        return _ultimateSignatures.TryGetValue(heroName, out var sig) ? sig : null;
    }
}

/// <summary>
/// Tracks ultimate state for a single player
/// </summary>
public class PlayerUltimateState
{
    public int PlayerId { get; set; }
    public string? HeroName { get; set; }
    public float EstimatedCharge { get; set; }
    public int LastDeathTick { get; set; }
    public int LastKillTick { get; set; }
    public int LastUltimateTick { get; set; }
    public int LastProcessedTick { get; set; }
    public int UltimatesUsed { get; set; }
    public int KillsSinceLastUlt { get; set; }
}

/// <summary>
/// Signature describing an ultimate's characteristics for detection
/// </summary>
public class UltimateSignature
{
    public string Name { get; set; } = "";
    public bool SpawnsEntities { get; set; }
    public int ExpectedEntityCount { get; set; }
    public int TypicalDuration { get; set; } // In ticks
    public bool CanCauseMultiKill { get; set; }
    public bool HasMovementRestriction { get; set; }
    public bool HasDistinctAnimation { get; set; }
    public bool HasInvulnerability { get; set; }
    public bool HasAutoAim { get; set; }
    public bool AffectsArea { get; set; }
    public bool AffectsAllAllies { get; set; }
    public bool AffectsSingleAlly { get; set; }
    public bool TransformsPlayer { get; set; }
    public bool ControlledEntity { get; set; }
    public bool IncreasedMobility { get; set; }
    public bool RequiresAirborne { get; set; }
}

/// <summary>
/// A detected ultimate usage
/// </summary>
public class DetectedUltimate
{
    public int PlayerId { get; set; }
    public int Tick { get; set; }
    public string HeroName { get; set; } = "";
    public string UltimateName { get; set; } = "";
    public float Confidence { get; set; }
    public List<string> DetectionReasons { get; set; } = new();

    public string Description => $"{HeroName} used {UltimateName}";
}
