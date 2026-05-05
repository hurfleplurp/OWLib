using System;
using System.Collections.Generic;
using System.Linq;
using ReplayAnalyzerTool.GameData;
using ReplayAnalyzerTool.Models;

namespace ReplayAnalyzerTool;

/// <summary>
/// Enhanced replay analyzer that integrates frame parsing with game-specific semantics.
/// Provides hero identification, ability tracking, and rich event descriptions.
/// </summary>
public class EnhancedReplayAnalyzer
{
    private readonly FrameParser _frameParser = new();
    private readonly GameStateTracker _gameState = new();
    private readonly EntityBehaviorTracker _behaviorTracker = new();
    private readonly UltimateTracker _ultimateTracker = new();
    private readonly List<string> _analysisLog = new();

    // Heuristic thresholds
    private const float TELEPORT_DISTANCE = 15f;
    private const float DEATH_HEIGHT_THRESHOLD = -100f;
    private const int MIN_PLAYERS_PER_TEAM = 5;
    private const int MAX_PLAYER_ENTITIES = 12;

    // Known entity patterns for hero identification
    private static readonly Dictionary<byte[], string> _heroSignatures = new();

    public GameStateTracker GameState => _gameState;
    public IReadOnlyList<string> AnalysisLog => _analysisLog;

    /// <summary>
    /// Perform enhanced analysis on parsed ticks, adding game-specific context
    /// </summary>
    public EnhancedAnalysisResult Analyze(List<TickData> ticks, ReplaySegmentInfo segmentInfo)
    {
        _analysisLog.Clear();
        Log($"Starting enhanced analysis of {ticks.Count} ticks");

        var result = new EnhancedAnalysisResult
        {
            SegmentInfo = segmentInfo,
            OriginalTicks = ticks
        };

        // Phase 0: Import player/hero info from highlight header
        ImportPlayersFromHeader(segmentInfo);

        // Phase 1: Identify players and assign initial hero guesses
        IdentifyPlayers(ticks);

        // Phase 2: Track entities across time and detect patterns
        TrackEntityPatterns(ticks);

        // Phase 2.5: Build behavior profiles and refine hero identification
        RefineHeroIdentification();

        // Phase 3: Detect game events (kills, deaths, abilities)
        DetectGameEvents(ticks);

        // Phase 3.5: Detect abilities from behavior tracking
        DetectAbilitiesFromBehavior();

        // Phase 3.6: Track and detect ultimate abilities
        TrackUltimates(ticks);

        // Phase 4: Build rich event descriptions
        BuildEventDescriptions(ticks);

        // Phase 5: Generate enhanced summaries
        result.GameState = _gameState;
        result.EnhancedEvents = ticks.SelectMany(t => t.Events).ToList();
        result.PlayerSummaries = BuildPlayerSummaries();
        result.NarrativeSummary = BuildNarrativeSummary();

        Log($"Analysis complete: {_gameState.Players.Count} players, {_gameState.Interactions.Count} interactions");

        return result;
    }

    /// <summary>
    /// Import player and hero information from the highlight header
    /// </summary>
    private void ImportPlayersFromHeader(ReplaySegmentInfo segmentInfo)
    {
        Log("Phase 0: Importing players from highlight header...");

        if (segmentInfo.Players == null || segmentInfo.Players.Count == 0)
        {
            Log("  No player info in header");
            return;
        }

        for (int i = 0; i < segmentInfo.Players.Count; i++)
        {
            var playerInfo = segmentInfo.Players[i];
            var player = _gameState.CreatePlayer(i, i);

            // Use actual player name from header
            if (!string.IsNullOrEmpty(playerInfo.Name))
            {
                player.PlayerName = playerInfo.Name;
            }
            else
            {
                player.PlayerName = $"Player-{i + 1}";
            }

            // Assign team from header
            player.Team = playerInfo.TeamIndex + 1; // Convert 0-based to 1-based

            // Try to map hero GUID to hero name
            if (!string.IsNullOrEmpty(playerInfo.HeroGuid))
            {
                var heroName = HeroGuidMapper.GetHeroName(playerInfo.HeroGuid);
                if (heroName != null)
                {
                    _gameState.AssignHero(i, heroName);
                    Log($"  Player {i}: {player.PlayerName} (Team {player.Team}) = {heroName}");

                    // Learn this mapping for future use
                    HeroGuidMapper.LearnMapping(playerInfo.HeroGuid, heroName);
                }
                else
                {
                    Log($"  Player {i}: {player.PlayerName} (Team {player.Team}) = Unknown (GUID: {playerInfo.HeroGuid})");

                    // Try to identify from GUID index
                    var guidIndex = HeroGuidMapper.GetGuidIndex(playerInfo.HeroGuid);
                    if (guidIndex.HasValue)
                    {
                        Log($"    (GUID index: 0x{guidIndex.Value:X})");
                    }
                }
            }
        }

        Log($"  Imported {segmentInfo.Players.Count} players from header");
    }

    private void Log(string message)
    {
        _analysisLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }

    /// <summary>
    /// Phase 1: Identify player entities from the tick data (supplements header info)
    /// </summary>
    private void IdentifyPlayers(List<TickData> ticks)
    {
        if (ticks.Count == 0) return;

        Log("Phase 1: Supplementing player identification from entity data...");

        // The first tick often has the most reliable player data
        var firstTick = ticks.First();

        int playerCount = Math.Min(firstTick.Entities.Count, MAX_PLAYER_ENTITIES);

        for (int i = 0; i < playerCount; i++)
        {
            var entity = firstTick.Entities[i];

            // Check if player already exists from header
            var existingPlayer = _gameState.GetPlayerByEntity(i);
            if (existingPlayer == null)
            {
                // Create player if not from header
                var player = _gameState.CreatePlayer(i, i);

                // Assign team based on index heuristic
                if (i == 0)
                {
                    player.Team = 1;
                    player.PlayerName = "You (POV)";
                    player.IsPov = true;  // Mark as POV player
                }
                else if (i < 6)
                {
                    player.Team = 1;
                    player.PlayerName = $"Ally-{i}";
                }
                else
                {
                    player.Team = 2;
                    player.PlayerName = $"Enemy-{i - 5}";
                }

                existingPlayer = player;
            }

            // Try to identify hero from entity data patterns if not already known
            if (existingPlayer.CurrentHero == null)
            {
                var heroGuess = TryIdentifyHero(entity);
                if (heroGuess != null)
                {
                    _gameState.AssignHero(i, heroGuess);
                    Log($"  Player {i} ({existingPlayer.PlayerName}) identified as {heroGuess} from entity data");
                }
            }
        }

        Log($"  Total tracked players: {_gameState.Players.Count}");
    }

    /// <summary>
    /// Attempt to identify a hero from entity data patterns
    /// </summary>
    private string TryIdentifyHero(EntityState entity)
    {
        if (entity.RawData == null || entity.RawData.Length < 16)
            return null;

        // Look for common hero identification patterns in the raw data
        // This is highly speculative - real hero IDs would come from replay header
        // or specific tagged data within frames

        // For now, try to identify based on data size patterns
        // Different heroes have different state data sizes
        int dataSize = entity.RawData.Length;

        // These are placeholder heuristics - need real data analysis
        // to determine actual hero signatures

        // We can look for things like:
        // - Number of abilities (ability state size)
        // - Unique ability patterns
        // - Health pool patterns (tanks vs dps vs support)

        // For now, return null and rely on manual assignment or header data
        return null;
    }

    /// <summary>
    /// Phase 2: Track entity movement patterns to identify behaviors
    /// </summary>
    private void TrackEntityPatterns(List<TickData> ticks)
    {
        Log("Phase 2: Tracking entity patterns...");

        TickData? prevTick = null;
        foreach (var tick in ticks)
        {
            _gameState.ProcessTick((int)tick.TickNumber, tick.DeltaTime);

            // Feed tick to behavior tracker for profiling
            _behaviorTracker.ProcessTick(tick);

            if (prevTick != null)
            {
                TrackMovement(prevTick, tick);
            }

            prevTick = tick;
        }

        Log($"  Tracked {_behaviorTracker.GetTrackedEntities().Count()} entities");
    }

    /// <summary>
    /// Phase 2.5: Use behavior profiles to refine hero identification for unknown heroes
    /// </summary>
    private void RefineHeroIdentification()
    {
        Log("Phase 2.5: Refining hero identification from behavior...");

        foreach (var player in _gameState.Players)
        {
            if (player.CurrentHero != null)
                continue; // Already identified

            var profile = _behaviorTracker.GetProfile((uint)player.EntityId);
            if (profile == null)
            {
                Log($"  Player {player.PlayerId}: No behavior profile");
                continue;
            }

            // Log behavior profile for debugging
            Log($"  Player {player.PlayerId} ({player.PlayerName}) profile:");
            Log($"    AvgSpeed: {profile.AverageSpeed:F2}, MaxSpeed: {profile.MaxSpeed:F2}");
            Log($"    MaxAlt: {profile.MaxAltitude:F2}, HighAltRatio: {profile.HighAltitudeRatio:P1}");
            Log($"    Teleports: {profile.TeleportCount}, AvgDataSize: {profile.AverageDataSize}");
            Log($"    Samples: {profile.PositionSampleCount}");

            var identification = HeroGuidMapper.IdentifyFromBehavior(profile);
            if (identification.MostLikely != null && identification.Confidence > 0.5f)
            {
                _gameState.AssignHero(player.PlayerId, identification.MostLikely);
                Log($"    -> Identified as {identification.MostLikely} (confidence: {identification.Confidence:P0})");
            }
            else if (identification.MostLikely != null && identification.Confidence >= 0.1f)
            {
                // Lower confidence - assign but mark as guess
                _gameState.AssignHero(player.PlayerId, identification.MostLikely + "?");
                Log($"    -> Best guess: {identification.MostLikely} (low confidence: {identification.Confidence:P0})");
            }
            else if (identification.AllCandidates.Count > 0)
            {
                var candidates = string.Join(", ", identification.AllCandidates
                    .OrderByDescending(c => c.Value)
                    .Take(3)
                    .Select(c => $"{c.Key}:{c.Value:P0}"));
                Log($"    -> Candidates: {candidates}");
            }
        }
    }

    /// <summary>
    /// Phase 3.5: Detect abilities from behavior tracking
    /// </summary>
    private void DetectAbilitiesFromBehavior()
    {
        Log("Phase 3.5: Detecting abilities from behavior patterns...");

        int totalAbilities = 0;
        foreach (var player in _gameState.Players)
        {
            var heroName = player.CurrentHero?.Name;
            var abilities = _behaviorTracker.DetectAbilities((uint)player.EntityId, heroName);

            foreach (var ability in abilities.Where(a => a.Confidence > 0.4f))
            {
                _gameState.RecordAbilityUse(player.PlayerId, ability.Name);
                totalAbilities++;
            }
        }

        Log($"  Detected {totalAbilities} ability uses from behavior");
    }

    /// <summary>
    /// Phase 3.6: Track and detect ultimate abilities
    /// </summary>
    private void TrackUltimates(List<TickData> ticks)
    {
        Log("Phase 3.6: Tracking ultimate abilities...");

        // Initialize ultimate tracker for all players
        foreach (var player in _gameState.Players)
        {
            _ultimateTracker.InitializePlayer(player.PlayerId, player.CurrentHero?.Name);
        }

        // Build a lookup of kills/deaths by tick from existing interactions
        var killsByTick = _gameState.Interactions
            .Where(i => i.Type == InteractionType.Kill && i.SourcePlayer != null)
            .GroupBy(i => i.Tick)
            .ToDictionary(g => g.Key, g => g.ToList());

        var deathsByTick = _gameState.Interactions
            .Where(i => i.Type == InteractionType.Death && i.TargetPlayer != null)
            .GroupBy(i => i.Tick)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Track kills per player within sliding windows
        var recentKillsByPlayer = new Dictionary<int, List<int>>();
        foreach (var player in _gameState.Players)
        {
            recentKillsByPlayer[player.PlayerId] = new List<int>();
        }

        int entitySpawnBurst = 0;
        int lastSpawnTick = 0;
        var ultimatesThisTick = new HashSet<int>(); // Track who already got ult detected this tick

        // Track entity spawns per player (for entity-based ult detection)
        var entitySpawnsNearPlayer = new Dictionary<int, int>();

        // Process ticks to build ultimate charge and detect usage
        for (int i = 1; i < ticks.Count; i++)
        {
            var prev = ticks[i - 1];
            var curr = ticks[i];
            int currentTick = (int)curr.TickNumber;
            ultimatesThisTick.Clear();
            entitySpawnsNearPlayer.Clear();

            // Update ultimate charge for all players
            _ultimateTracker.ProcessTick(curr, _gameState);

            // Record kills and deaths from this tick
            if (killsByTick.TryGetValue(currentTick, out var tickKills))
            {
                foreach (var kill in tickKills)
                {
                    if (kill.SourcePlayer != null)
                    {
                        _ultimateTracker.RecordKill(kill.SourcePlayer.PlayerId, currentTick);
                        recentKillsByPlayer[kill.SourcePlayer.PlayerId].Add(currentTick);
                        Log($"    [Tick {currentTick}] Kill by Player {kill.SourcePlayer.PlayerId}");
                    }
                }
            }

            if (deathsByTick.TryGetValue(currentTick, out var tickDeaths))
            {
                foreach (var death in tickDeaths)
                {
                    if (death.TargetPlayer != null)
                    {
                        _ultimateTracker.RecordDeath(death.TargetPlayer.PlayerId, currentTick);
                    }
                }
            }

            // Count entity spawns in this tick (for ultimate detection)
            var prevEntities = new HashSet<int>(prev.Entities.Select(e => e.Index));
            var currEntities = new HashSet<int>(curr.Entities.Select(e => e.Index));
            var newEntities = currEntities.Except(prevEntities).Where(e => e >= MAX_PLAYER_ENTITIES).ToList();

            // Log entity spawn details for debugging
            if (newEntities.Count > 0)
            {
                Log($"    [Tick {currentTick}] Entity spawn: {newEntities.Count} new entities (indices: {string.Join(", ", newEntities.Take(5))}{(newEntities.Count > 5 ? "..." : "")})");
            }

            if (newEntities.Count > 0)
            {
                if (currentTick - lastSpawnTick <= 3)
                {
                    entitySpawnBurst += newEntities.Count;
                }
                else
                {
                    entitySpawnBurst = newEntities.Count;
                }
                lastSpawnTick = currentTick;

                // Try to attribute entity spawns to nearby players
                foreach (var entityIdx in newEntities)
                {
                    var entity = curr.Entities.FirstOrDefault(e => e.Index == entityIdx);
                    if (entity?.Position.HasValue == true)
                    {
                        var nearestPlayer = FindNearestPlayer(curr, entity.Position.Value, 50f);
                        if (nearestPlayer != null)
                        {
                            if (!entitySpawnsNearPlayer.ContainsKey(nearestPlayer.PlayerId))
                                entitySpawnsNearPlayer[nearestPlayer.PlayerId] = 0;
                            entitySpawnsNearPlayer[nearestPlayer.PlayerId]++;
                        }
                    }
                }
            }
            else if (currentTick - lastSpawnTick > 30)
            {
                entitySpawnBurst = 0;
            }

            // Check each player for potential ultimate usage
            foreach (var player in _gameState.Players)
            {
                if (ultimatesThisTick.Contains(player.PlayerId)) continue;

                // Clean up old kills from window (keep last 3 seconds = ~188 ticks)
                recentKillsByPlayer[player.PlayerId].RemoveAll(t => currentTick - t > 188);

                var recentKills = recentKillsByPlayer[player.PlayerId];
                var profile = _behaviorTracker.GetProfile((uint)player.EntityId);
                int playerEntitySpawns = entitySpawnsNearPlayer.GetValueOrDefault(player.PlayerId, 0);
                var ultState = _ultimateTracker.GetPlayerState(player.PlayerId);

                // Log detection attempt for debugging (only when there's something to detect)
                if (recentKills.Count > 0 || playerEntitySpawns >= 2)
                {
                    Log($"    [Tick {currentTick}] Checking ult for {player.DisplayName}: {recentKills.Count} kills, {playerEntitySpawns} entity spawns, est charge: {ultState?.EstimatedCharge:F0}%");
                }

                // Try detection: multi-kill, single kill with high charge, or entity spawns
                DetectedUltimate? detected = null;

                // Method 1: Multi-kill detection (strongest signal)
                if (recentKills.Count >= 2)
                {
                    detected = _ultimateTracker.TryDetectUltimate(
                        player.PlayerId,
                        currentTick,
                        profile,
                        recentKills,
                        entitySpawnBurst
                    );
                }

                // Method 2: Single kill with entity spawns near player
                if (detected == null && recentKills.Count >= 1 && playerEntitySpawns >= 1)
                {
                    detected = _ultimateTracker.TryDetectUltimate(
                        player.PlayerId,
                        currentTick,
                        profile,
                        recentKills,
                        playerEntitySpawns
                    );
                }

                // Method 3: POV player with a kill - highlights capture player moments
                // Use more lenient detection for POV since the highlight system selected this clip
                if (detected == null && recentKills.Count >= 1 && player.IsPov)
                {
                    detected = _ultimateTracker.TryDetectUltimate(
                        player.PlayerId,
                        currentTick,
                        profile,
                        recentKills,
                        entitySpawnBurst,
                        isPovPlayer: true  // Be more lenient for POV
                    );
                }

                // Method 4: Any kill by a damage ult hero - for highlights, single kills are often ult-related
                if (detected == null && recentKills.Count >= 1)
                {
                    // Check if this hero's ult typically causes kills
                    var signature = UltimateTracker.GetSignature(player.CurrentHero?.Name ?? "");
                    if (signature != null && signature.CanCauseMultiKill)
                    {
                        // This hero has a kill-capable ult, and they just got a kill
                        detected = _ultimateTracker.TryDetectUltimate(
                            player.PlayerId,
                            currentTick,
                            profile,
                            recentKills,
                            entitySpawnBurst,
                            isPovPlayer: true  // Treat as POV-level priority for any kill in highlight
                        );
                    }
                }

                // Method 5: Entity spawn burst alone (for entity-heavy ults like Minefield, Blizzard)
                if (detected == null && playerEntitySpawns >= 3)
                {
                    detected = _ultimateTracker.TryDetectFromEntitySpawn(
                        player.PlayerId,
                        currentTick,
                        playerEntitySpawns
                    );
                }

                if (detected != null)
                {
                    ultimatesThisTick.Add(player.PlayerId);
                    Log($"  [{currentTick}] {detected.Description} detected (confidence: {detected.Confidence:P0})");
                    Log($"    Reasons: {string.Join(", ", detected.DetectionReasons)}");

                    _gameState.RecordUltimateUse(player.PlayerId, detected.UltimateName);

                    // Create interaction for the ultimate
                    var interaction = new GameInteraction
                    {
                        Tick = currentTick,
                        Type = InteractionType.UltimateUse,
                        SourcePlayer = player,
                        AbilityName = detected.UltimateName,
                        Description = detected.Description
                    };
                    _gameState.Interactions.Add(interaction);

                    // Clear kills after detecting ult
                    recentKills.Clear();
                }
            }
        }

        var detectedUlts = _ultimateTracker.GetDetectedUltimates();
        Log($"  Detected {detectedUlts.Count} ultimate uses");
    }

    /// <summary>
    /// Find the nearest player to a position
    /// </summary>
    private PlayerTracker? FindNearestPlayer(TickData tick, Vector3 position, float maxDistance)
    {
        PlayerTracker? nearest = null;
        float nearestDist = maxDistance;

        foreach (var entity in tick.Entities)
        {
            if (entity.Index >= MAX_PLAYER_ENTITIES) continue;
            if (!entity.Position.HasValue) continue;

            var player = _gameState.GetPlayerByEntity(entity.Index);
            if (player == null) continue;

            var dist = (entity.Position.Value - position).Magnitude;
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = player;
            }
        }

        return nearest;
    }

    private void TrackMovement(TickData prev, TickData curr)
    {
        int commonEntities = Math.Min(prev.Entities.Count, curr.Entities.Count);

        for (int i = 0; i < commonEntities && i < MAX_PLAYER_ENTITIES; i++)
        {
            var prevEntity = prev.Entities[i];
            var currEntity = curr.Entities[i];

            if (!prevEntity.Position.HasValue || !currEntity.Position.HasValue)
                continue;

            var delta = currEntity.Position.Value - prevEntity.Position.Value;
            float distance = delta.Magnitude;
            float speed = distance / curr.DeltaTime;

            // Detect teleports (movement abilities)
            if (distance > TELEPORT_DISTANCE)
            {
                var player = _gameState.GetPlayerByEntity(i);
                if (player != null)
                {
                    // Could be blink, recall, translocator, etc.
                    DetectTeleportAbility(player, distance, prevEntity.Position.Value, currEntity.Position.Value, curr.TickNumber);
                }
            }

            // Detect death by falling
            if (currEntity.Position.Value.Y < DEATH_HEIGHT_THRESHOLD && prevEntity.Position.Value.Y > DEATH_HEIGHT_THRESHOLD)
            {
                var player = _gameState.GetPlayerByEntity(i);
                if (player != null)
                {
                    _gameState.RecordDeath(i, null, "Environmental (Fall)");
                    Log($"  [{curr.TickNumber}] {player.DisplayName} died to environmental");
                }
            }
        }
    }

    private void DetectTeleportAbility(PlayerTracker player, float distance, Vector3 from, Vector3 to, uint tick)
    {
        if (player.CurrentHero == null) return;

        var hero = player.CurrentHero;

        // Try to match teleport to known abilities
        string abilityName = hero.Name switch
        {
            "Tracer" when distance < 10 => "Blink",
            "Tracer" => "Recall",
            "Sombra" => "Translocator",
            "Reaper" => "Shadow Step",
            "Moira" => "Fade",
            "Kiriko" => "Swift Step",
            "Echo" when to.Y > from.Y + 5 => "Flight",
            "Mercy" => "Guardian Angel",
            "Pharah" when to.Y > from.Y => "Jump Jet",
            "Lucio" when to.Y > from.Y + 3 => "Wall Ride",
            "Genji" when distance > 10 => "Swift Strike",
            "Doomfist" => distance switch
            {
                > 20 => "Meteor Strike",
                > 10 => "Rocket Punch",
                _ => "Seismic Slam"
            },
            "Winston" => "Jump Pack",
            "D.Va" => "Boosters",
            "Wrecking Ball" => "Grappling Claw",
            _ => "Movement Ability"
        };

        _gameState.RecordAbilityUse(player.PlayerId, abilityName);
    }

    /// <summary>
    /// Phase 3: Detect game events from tick patterns
    /// </summary>
    private void DetectGameEvents(List<TickData> ticks)
    {
        Log("Phase 3: Detecting game events...");

        for (int i = 1; i < ticks.Count; i++)
        {
            var prev = ticks[i - 1];
            var curr = ticks[i];

            // Entity count changes often indicate spawns/deaths
            int entityDiff = curr.Entities.Count - prev.Entities.Count;

            if (entityDiff < -1)
            {
                // Multiple entities disappeared - could be death or despawn
                DetectPossibleDeaths(prev, curr, -entityDiff);
            }
            else if (entityDiff > 1)
            {
                // Multiple entities appeared - could be spawn or ultimate effects
                DetectPossibleSpawns(prev, curr, entityDiff);
            }

            // Look for sudden position resets (respawn)
            DetectRespawns(prev, curr);
        }
    }

    private void DetectPossibleDeaths(TickData prev, TickData curr, int count)
    {
        // Find which entities disappeared
        var prevEntities = new HashSet<int>(prev.Entities.Select(e => e.Index));
        var currEntities = new HashSet<int>(curr.Entities.Select(e => e.Index));

        var disappeared = prevEntities.Except(currEntities);

        foreach (var entityIndex in disappeared)
        {
            if (entityIndex >= MAX_PLAYER_ENTITIES) continue; // Not a player

            var victim = _gameState.GetPlayerByEntity(entityIndex);
            if (victim != null)
            {
                // Check if this looks like a death
                var lastEntity = prev.Entities.FirstOrDefault(e => e.Index == entityIndex);
                if (lastEntity != null && lastEntity.Position.HasValue)
                {
                    // Try to attribute the kill to an enemy
                    var killer = FindLikelyKiller(prev, lastEntity, victim);

                    if (killer != null)
                    {
                        var killInteraction = _gameState.RecordKill(killer.PlayerId, victim.PlayerId, null, false, false);
                        if (killInteraction != null)
                        {
                            Log($"  [{curr.TickNumber}] {killer.DisplayName} killed {victim.DisplayName}");
                        }
                    }
                    else
                    {
                        // Unknown killer - still record the death
                        _gameState.RecordDeath(entityIndex, null, "Combat");
                        Log($"  [{curr.TickNumber}] {victim.DisplayName} possibly died (unknown killer)");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Find the most likely killer for a death based on proximity and team
    /// </summary>
    private PlayerTracker? FindLikelyKiller(TickData tick, EntityState victimEntity, PlayerTracker victim)
    {
        if (!victimEntity.Position.HasValue) return null;

        var victimPos = victimEntity.Position.Value;
        PlayerTracker? bestKiller = null;
        float bestScore = float.MaxValue;

        // Get POV player - highlights usually capture the POV player's actions
        var pov = _gameState.GetPovPlayer();

        foreach (var entity in tick.Entities)
        {
            if (entity.Index >= MAX_PLAYER_ENTITIES) continue;
            if (entity.Index == victimEntity.Index) continue;
            if (!entity.Position.HasValue) continue;

            var player = _gameState.GetPlayerByEntity(entity.Index);
            if (player == null) continue;

            // Must be on opposite team
            if (player.Team == victim.Team) continue;

            var distance = (entity.Position.Value - victimPos).Magnitude;

            // Score based on distance, with bonus for POV player
            float score = distance;
            if (player == pov)
            {
                // POV player gets strong preference (highlights are usually of player kills)
                score *= 0.3f;
            }

            // Only consider enemies within reasonable range (100 units)
            if (distance < 100 && score < bestScore)
            {
                bestScore = score;
                bestKiller = player;
            }
        }

        return bestKiller;
    }

    private void DetectPossibleSpawns(TickData prev, TickData curr, int count)
    {
        // Find which entities appeared
        var prevEntities = new HashSet<int>(prev.Entities.Select(e => e.Index));
        var currEntities = new HashSet<int>(curr.Entities.Select(e => e.Index));

        var appeared = currEntities.Except(prevEntities);

        foreach (var entityIndex in appeared)
        {
            var entity = curr.Entities.FirstOrDefault(e => e.Index == entityIndex);
            if (entity == null) continue;

            // Classify new entity
            if (entityIndex < MAX_PLAYER_ENTITIES)
            {
                // Player respawn
                var player = _gameState.GetPlayerByEntity(entityIndex);
                if (player != null)
                {
                    Log($"  [{curr.TickNumber}] {player.DisplayName} respawned");
                }
            }
            else
            {
                // Could be projectile, deployable, or ultimate object
                Log($"  [{curr.TickNumber}] New entity spawned (index {entityIndex})");
            }
        }
    }

    private void DetectRespawns(TickData prev, TickData curr)
    {
        int commonEntities = Math.Min(prev.Entities.Count, curr.Entities.Count);

        for (int i = 0; i < commonEntities && i < MAX_PLAYER_ENTITIES; i++)
        {
            var prevEntity = prev.Entities[i];
            var currEntity = curr.Entities[i];

            if (!prevEntity.Position.HasValue || !currEntity.Position.HasValue)
                continue;

            // Large Y change upward could be respawn
            float yDelta = currEntity.Position.Value.Y - prevEntity.Position.Value.Y;
            float xzDistance = (float)Math.Sqrt(
                Math.Pow(currEntity.Position.Value.X - prevEntity.Position.Value.X, 2) +
                Math.Pow(currEntity.Position.Value.Z - prevEntity.Position.Value.Z, 2)
            );

            // Respawns typically involve large position changes
            if (yDelta > 20 && xzDistance > 30)
            {
                var player = _gameState.GetPlayerByEntity(i);
                if (player != null)
                {
                    // Check if player was recently dead
                    if (player.Deaths > 0)
                    {
                        Log($"  [{curr.TickNumber}] {player.DisplayName} respawned");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Phase 4: Build human-readable descriptions for all events
    /// </summary>
    private void BuildEventDescriptions(List<TickData> ticks)
    {
        Log("Phase 4: Building event descriptions...");

        foreach (var tick in ticks)
        {
            foreach (var evt in tick.Events)
            {
                EnrichEventDescription(evt);
            }
        }
    }

    private void EnrichEventDescription(GameEvent evt)
    {
        // Add player names to entity-related events
        if (evt.SourceEntityIndex.HasValue)
        {
            var player = _gameState.GetPlayerByEntity((int)evt.SourceEntityIndex.Value);
            if (player != null)
            {
                evt.Detail = evt.Detail?.Replace($"Entity {evt.SourceEntityIndex}", player.DisplayName);
            }
        }

        // Add game-specific context
        evt.Detail = evt.Type switch
        {
            GameEventType.EntitySpawn => $"👤 {evt.Detail}",
            GameEventType.EntityDespawn => $"💀 {evt.Detail}",
            GameEventType.MassSpawn => $"⚔️ {evt.Detail} (possible ultimate or team spawn)",
            GameEventType.MassDespawn => $"☠️ {evt.Detail} (possible team wipe)",
            GameEventType.Teleport => $"✨ {evt.Detail}",
            GameEventType.TimingAnomaly => $"⏸️ {evt.Detail}",
            _ => evt.Detail
        };
    }

    /// <summary>
    /// Build summaries for each player
    /// </summary>
    private List<PlayerSummary> BuildPlayerSummaries()
    {
        return _gameState.Players.Select(p => new PlayerSummary
        {
            PlayerId = p.PlayerId,
            PlayerName = p.PlayerName,
            Team = p.Team,
            HeroName = p.CurrentHero != null
                ? (p.IsHeroUncertain ? $"{p.CurrentHero.Name}?" : p.CurrentHero.Name)
                : "Unknown",
            Role = p.CurrentHero?.Role.ToString() ?? "Unknown",
            Eliminations = p.Eliminations,
            Deaths = p.Deaths,
            UltimatesUsed = p.UltimatesUsed,
            HeroSwaps = p.HeroHistory.Select(h => h.HeroName).Distinct().Count()
        }).ToList();
    }

    /// <summary>
    /// Generate a narrative summary of the match
    /// </summary>
    private string BuildNarrativeSummary()
    {
        var lines = new List<string>();

        lines.Add("=== Match Narrative ===\n");

        // Map and mode
        if (_gameState.CurrentMap != null)
        {
            lines.Add($"📍 {_gameState.CurrentMap.Name} - {_gameState.CurrentMap.GameModeDescription}");
        }

        // Player list with heroes
        lines.Add("\n👥 Players:");
        foreach (var player in _gameState.Players.OrderBy(p => p.Team).ThenBy(p => p.PlayerId))
        {
            var teamIcon = player.Team == 1 ? "🔵" : "🔴";
            var roleIcon = player.RoleIcon;
            var heroDisplay = player.CurrentHero != null
                ? (player.IsHeroUncertain ? $"{player.CurrentHero.Name}?" : player.CurrentHero.Name)
                : "Unknown";
            lines.Add($"  {teamIcon} {player.PlayerName} - {roleIcon} {heroDisplay}");
        }

        // Key moments
        lines.Add("\n⭐ Key Moments:");
        var keyMoments = _gameState.Interactions
            .Where(i => i.Type == InteractionType.Kill || i.Type == InteractionType.UltimateUse || i.Type == InteractionType.Resurrection)
            .Take(10);

        foreach (var moment in keyMoments)
        {
            lines.Add($"  {moment.Icon} [{moment.Tick}] {moment.Description}");
        }

        // Final stats
        lines.Add("\n📊 Final Stats:");
        foreach (var player in _gameState.Players.OrderByDescending(p => p.Eliminations))
        {
            lines.Add($"  {player.DisplayName}: {player.Eliminations}E / {player.Deaths}D");
        }

        return string.Join("\n", lines);
    }
}

/// <summary>
/// Result of enhanced analysis
/// </summary>
public class EnhancedAnalysisResult
{
    public ReplaySegmentInfo SegmentInfo { get; set; }
    public List<TickData> OriginalTicks { get; set; }
    public GameStateTracker GameState { get; set; }
    public List<GameEvent> EnhancedEvents { get; set; }
    public List<PlayerSummary> PlayerSummaries { get; set; }
    public string NarrativeSummary { get; set; }
}

public class PlayerSummary
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; }
    public int Team { get; set; }
    public string HeroName { get; set; }
    public string Role { get; set; }
    public int Eliminations { get; set; }
    public int Deaths { get; set; }
    public int UltimatesUsed { get; set; }
    public int HeroSwaps { get; set; }
}
