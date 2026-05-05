using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReplayAnalyzerTool.Models;

namespace ReplayAnalyzerTool;

/// <summary>
/// Parses raw replay frame data into standardized tick format.
/// Handles bit-unpacking and entity state extraction.
/// </summary>
public class FrameParser
{
    // Known marker that appears at byte 4 of every frame payload
    private const uint FRAME_MARKER = 0x00077A84;

    // Entity tracker for naming and continuity
    private readonly EntityTracker _entityTracker = new();

    /// <summary>
    /// Parse decompressed replay data into structured ticks
    /// </summary>
    public List<TickData> ParseTicks(byte[] data, ReplaySegmentInfo info)
    {
        var ticks = new List<TickData>();
        _entityTracker.Reset();

        // Find all marker positions to locate frames
        var markerOffsets = FindMarkerOffsets(data, FRAME_MARKER);
        if (markerOffsets.Count == 0)
        {
            Console.WriteLine("  [FrameParser] No frame markers found");
            return ticks;
        }

        // Frame header is 16 bytes before marker (12-byte header + 4 bytes of payload before marker)
        float cumulativeTime = 0f;

        foreach (var markerOffset in markerOffsets)
        {
            int headerOffset = markerOffset - 16;
            if (headerOffset < 0 || headerOffset + 12 > data.Length) continue;

            // Parse frame header
            uint tickNum = BitConverter.ToUInt32(data, headerOffset);
            uint dataLen = BitConverter.ToUInt32(data, headerOffset + 4);
            uint entityCount = BitConverter.ToUInt32(data, headerOffset + 8);

            // Validate header values
            if (tickNum < 1 || tickNum > 999999 || dataLen < 8 || dataLen > 10000 || entityCount < 1 || entityCount > 100)
                continue;

            if (headerOffset + 12 + dataLen > data.Length) continue;

            // Extract frame payload
            var frameData = new byte[dataLen];
            Array.Copy(data, headerOffset + 12, frameData, 0, (int)dataLen);

            // First 4 bytes of payload is delta time (float)
            float deltaTime = BitConverter.ToSingle(frameData, 0);
            if (deltaTime < 0 || deltaTime > 1.0f) deltaTime = 0.0167f; // Default to 60fps

            cumulativeTime += deltaTime;

            var tick = new TickData
            {
                TickNumber = tickNum,
                DeltaTime = deltaTime,
                CumulativeTime = cumulativeTime,
                RawData = frameData,
                Entities = ParseEntities(frameData, (int)entityCount, tickNum)
            };

            // Track entities across ticks for naming and analysis
            _entityTracker.UpdateTick(tick);

            // Try to detect events from frame data patterns
            tick.Events = DetectEvents(tick, ticks.LastOrDefault());

            ticks.Add(tick);
        }

        // Update segment info with analysis results
        if (ticks.Count > 0)
        {
            info.StartTick = ticks.First().TickNumber;
            info.EndTick = ticks.Last().TickNumber;
            info.TotalDuration = cumulativeTime;
            info.TickCount = ticks.Count;
            info.MinEntityCount = ticks.Min(t => t.Entities.Count);
            info.MaxEntityCount = ticks.Max(t => t.Entities.Count);
            info.TrackedEntities = _entityTracker.GetTrackedEntitySummaries();
            info.TotalEvents = ticks.Sum(t => t.Events.Count);
            info.EventSummary = GenerateEventSummary(ticks);
        }

        return ticks;
    }

    private Dictionary<string, int> GenerateEventSummary(List<TickData> ticks)
    {
        return ticks.SelectMany(t => t.Events)
                    .GroupBy(e => e.Type.ToString())
                    .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Find all occurrences of a 4-byte marker in data
    /// </summary>
    private List<int> FindMarkerOffsets(byte[] data, uint marker)
    {
        var offsets = new List<int>();
        byte[] markerBytes = BitConverter.GetBytes(marker);

        for (int i = 0; i < data.Length - 4; i++)
        {
            if (data[i] == markerBytes[0] && data[i+1] == markerBytes[1] &&
                data[i+2] == markerBytes[2] && data[i+3] == markerBytes[3])
            {
                offsets.Add(i);
            }
        }

        return offsets;
    }

    /// <summary>
    /// Parse entity states from frame payload.
    /// Entity data starts after the 8-byte header (deltaTime + marker).
    /// </summary>
    private List<EntityState> ParseEntities(byte[] frameData, int entityCount, uint tickNum)
    {
        var entities = new List<EntityState>();

        if (frameData.Length < 8) return entities;

        // Entity data starts at offset 8 (after deltaTime and marker)
        int entityDataStart = 8;
        int remainingBytes = frameData.Length - entityDataStart;

        // Estimate bytes per entity (varies based on state changes)
        int avgBytesPerEntity = remainingBytes / Math.Max(1, entityCount);

        using var ms = new MemoryStream(frameData, entityDataStart, remainingBytes);
        using var reader = new BinaryReader(ms);

        for (int i = 0; i < entityCount && ms.Position < ms.Length - 4; i++)
        {
            var entity = new EntityState
            {
                Index = i,
                TickNumber = tickNum
            };

            long startPos = ms.Position;

            try
            {
                // Read potential position data (looking for float triplets in reasonable range)
                if (ms.Length - ms.Position >= 12)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();

                    // Check if these look like valid coordinates
                    if (IsValidCoordinate(x) && IsValidCoordinate(y) && IsValidCoordinate(z))
                    {
                        entity.Position = new Vector3(x, y, z);
                    }
                    else
                    {
                        // Reset and try different interpretation
                        ms.Position = startPos;
                    }
                }

                // Analyze data patterns for entity classification
                int entityBytes = Math.Min(avgBytesPerEntity, (int)(ms.Length - startPos));
                entity.RawData = new byte[entityBytes];
                ms.Position = startPos;
                ms.Read(entity.RawData, 0, entityBytes);

                // Try to classify entity type based on data patterns
                entity.Type = ClassifyEntityType(entity.RawData, i, entityCount);

                // Extract any recognizable state flags from raw data
                entity.StateFlags = ExtractStateFlags(entity.RawData);

                entities.Add(entity);
            }
            catch
            {
                // Skip malformed entity
            }
        }

        return entities;
    }

    /// <summary>
    /// Attempt to classify entity type based on data patterns and position in entity list
    /// </summary>
    private EntityType ClassifyEntityType(byte[] rawData, int index, int totalCount)
    {
        // Heuristic: First 12 entities are typically players (6v6)
        // In highlights, first entity is usually the player POV
        if (index == 0)
            return EntityType.PlayerPOV;
        if (index < 12)
            return EntityType.Player;

        // Additional entities could be projectiles, deployables, etc.
        // Need more analysis to differentiate
        return EntityType.Unknown;
    }

    /// <summary>
    /// Extract state flags from entity raw data
    /// </summary>
    private uint ExtractStateFlags(byte[] rawData)
    {
        if (rawData.Length < 4) return 0;

        // Look for potential state flag patterns in the data
        // This is speculative - the actual format needs reverse engineering
        return 0;
    }

    private bool IsValidCoordinate(float val)
    {
        return !float.IsNaN(val) && !float.IsInfinity(val) && Math.Abs(val) < 500;
    }

    /// <summary>
    /// Detect game events by comparing consecutive ticks
    /// </summary>
    private List<GameEvent> DetectEvents(TickData current, TickData? previous)
    {
        var events = new List<GameEvent>();

        if (previous == null)
        {
            // First tick - mark as segment start
            events.Add(new GameEvent
            {
                Type = GameEventType.SegmentStart,
                TickNumber = current.TickNumber,
                Time = current.CumulativeTime,
                Detail = $"Segment started at tick {current.TickNumber}"
            });
            return events;
        }

        // Detect entity count changes (could indicate spawn/death)
        int entityDiff = current.Entities.Count - previous.Entities.Count;
        if (entityDiff > 2)
        {
            events.Add(new GameEvent
            {
                Type = GameEventType.MassSpawn,
                TickNumber = current.TickNumber,
                Time = current.CumulativeTime,
                Detail = $"Entity count increased by {entityDiff}",
                Value = entityDiff
            });
        }
        else if (entityDiff > 0)
        {
            events.Add(new GameEvent
            {
                Type = GameEventType.EntitySpawn,
                TickNumber = current.TickNumber,
                Time = current.CumulativeTime,
                Detail = $"Entity spawned (+{entityDiff})",
                Value = entityDiff
            });
        }
        else if (entityDiff < -2)
        {
            events.Add(new GameEvent
            {
                Type = GameEventType.MassDespawn,
                TickNumber = current.TickNumber,
                Time = current.CumulativeTime,
                Detail = $"Entity count decreased by {-entityDiff}",
                Value = entityDiff
            });
        }
        else if (entityDiff < 0)
        {
            events.Add(new GameEvent
            {
                Type = GameEventType.EntityDespawn,
                TickNumber = current.TickNumber,
                Time = current.CumulativeTime,
                Detail = $"Entity despawned ({entityDiff})",
                Value = entityDiff
            });
        }

        // Detect timing anomalies (could indicate pause, lag, or scene transition)
        float expectedDt = 0.0167f; // 60fps
        if (current.DeltaTime > expectedDt * 3)
        {
            events.Add(new GameEvent
            {
                Type = GameEventType.TimingAnomaly,
                TickNumber = current.TickNumber,
                Time = current.CumulativeTime,
                Detail = $"Large time gap: {current.DeltaTime * 1000:F1}ms (expected ~16.7ms)",
                Value = current.DeltaTime
            });
        }

        // Detect large tick number jumps (could indicate cut/edit)
        uint tickGap = current.TickNumber - previous.TickNumber;
        if (tickGap > 5)
        {
            events.Add(new GameEvent
            {
                Type = GameEventType.TickJump,
                TickNumber = current.TickNumber,
                Time = current.CumulativeTime,
                Detail = $"Tick jump: {previous.TickNumber} -> {current.TickNumber} (gap of {tickGap})",
                Value = tickGap
            });
        }

        // Detect significant data size changes (could indicate state changes)
        int sizeDiff = current.RawData.Length - previous.RawData.Length;
        if (Math.Abs(sizeDiff) > 100)
        {
            events.Add(new GameEvent
            {
                Type = GameEventType.DataSizeChange,
                TickNumber = current.TickNumber,
                Time = current.CumulativeTime,
                Detail = $"Frame size changed by {sizeDiff} bytes",
                Value = sizeDiff
            });
        }

        // Detect position changes for tracked entities (future: after entity tracking is complete)
        DetectEntityMovementEvents(current, previous, events);

        return events;
    }

    /// <summary>
    /// Detect significant entity movement events
    /// </summary>
    private void DetectEntityMovementEvents(TickData current, TickData? previous, List<GameEvent> events)
    {
        if (previous == null) return;

        // Compare matching entities by index
        int commonCount = Math.Min(current.Entities.Count, previous.Entities.Count);
        for (int i = 0; i < commonCount; i++)
        {
            var curr = current.Entities[i];
            var prev = previous.Entities[i];

            if (curr.Position.HasValue && prev.Position.HasValue)
            {
                var delta = curr.Position.Value - prev.Position.Value;
                float distance = delta.Magnitude;

                // Teleport threshold (very large instant movement)
                if (distance > 20.0f)
                {
                    events.Add(new GameEvent
                    {
                        Type = GameEventType.Teleport,
                        TickNumber = current.TickNumber,
                        Time = current.CumulativeTime,
                        SourceEntityIndex = (uint)i,
                        Detail = $"Entity {i} teleported {distance:F1} units",
                        Value = distance
                    });
                }
            }
        }
    }
}

/// <summary>
/// Bit-level reader for packed data
/// </summary>
public class BitReader
{
    private readonly byte[] _data;
    private int _bitPosition;

    public BitReader(byte[] data)
    {
        _data = data;
        _bitPosition = 0;
    }

    public int BitPosition => _bitPosition;
    public int BytePosition => _bitPosition / 8;
    public int RemainingBits => (_data.Length * 8) - _bitPosition;

    public uint ReadBits(int count)
    {
        if (count > 32 || count < 1)
            throw new ArgumentException("Can only read 1-32 bits at a time");

        uint result = 0;
        for (int i = 0; i < count; i++)
        {
            if (_bitPosition >= _data.Length * 8)
                throw new EndOfStreamException();

            int byteIndex = _bitPosition / 8;
            int bitIndex = _bitPosition % 8;

            if ((_data[byteIndex] & (1 << bitIndex)) != 0)
                result |= (uint)(1 << i);

            _bitPosition++;
        }
        return result;
    }

    public float ReadFloat()
    {
        uint bits = ReadBits(32);
        return BitConverter.Int32BitsToSingle((int)bits);
    }

    public void SkipBits(int count) => _bitPosition += count;
    public void AlignToByte() => _bitPosition = ((_bitPosition + 7) / 8) * 8;
}

/// <summary>
/// Tracks entities across ticks for naming and continuity analysis
/// </summary>
public class EntityTracker
{
    private readonly Dictionary<int, TrackedEntity> _entities = new();
    private uint _lastTickNumber;

    public void Reset()
    {
        _entities.Clear();
        _lastTickNumber = 0;
    }

    public void UpdateTick(TickData tick)
    {
        foreach (var entity in tick.Entities)
        {
            if (!_entities.TryGetValue(entity.Index, out var tracked))
            {
                tracked = new TrackedEntity
                {
                    Index = entity.Index,
                    FirstSeenTick = tick.TickNumber,
                    Name = GenerateEntityName(entity),
                    Type = entity.Type
                };
                _entities[entity.Index] = tracked;
            }

            tracked.LastSeenTick = tick.TickNumber;
            tracked.TicksPresent++;

            if (entity.Position.HasValue)
            {
                tracked.LastPosition = entity.Position.Value;
                tracked.PositionSamples++;
            }

            // Update entity with tracked name
            entity.Name = tracked.Name;
            entity.TrackedId = tracked.Index;
        }

        _lastTickNumber = tick.TickNumber;
    }

    private string GenerateEntityName(EntityState entity)
    {
        return entity.Type switch
        {
            EntityType.PlayerPOV => "Player (POV)",
            EntityType.Player => $"Player-{entity.Index}",
            EntityType.Projectile => $"Projectile-{entity.Index}",
            EntityType.Deployable => $"Deployable-{entity.Index}",
            _ => $"Entity-{entity.Index}"
        };
    }

    public List<TrackedEntitySummary> GetTrackedEntitySummaries()
    {
        return _entities.Values.Select(e => new TrackedEntitySummary
        {
            Index = e.Index,
            Name = e.Name,
            Type = e.Type.ToString(),
            FirstSeenTick = e.FirstSeenTick,
            LastSeenTick = e.LastSeenTick,
            TicksPresent = e.TicksPresent,
            PositionSamples = e.PositionSamples
        }).OrderBy(e => e.Index).ToList();
    }

    private class TrackedEntity
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public EntityType Type { get; set; }
        public uint FirstSeenTick { get; set; }
        public uint LastSeenTick { get; set; }
        public int TicksPresent { get; set; }
        public int PositionSamples { get; set; }
        public Vector3? LastPosition { get; set; }
    }
}
