using System.Collections.Generic;
using System.Linq;

namespace ReplayAnalyzerTool.GameData;

/// <summary>
/// Database of Overwatch 2 maps with their properties.
/// </summary>
public static class MapDatabase
{
    private static readonly Dictionary<string, MapDefinition> _mapByGuid = new();
    private static readonly Dictionary<string, MapDefinition> _mapByName = new();
    private static readonly List<MapDefinition> _allMaps = new();

    static MapDatabase()
    {
        InitializeMaps();
    }

    private static void InitializeMaps()
    {
        // Control Maps (KOTH)
        AddMap(new MapDefinition { Name = "Antarctic Peninsula", Type = MapType.Control, Location = "Antarctica" });
        AddMap(new MapDefinition { Name = "Busan", Type = MapType.Control, Location = "South Korea" });
        AddMap(new MapDefinition { Name = "Ilios", Type = MapType.Control, Location = "Greece" });
        AddMap(new MapDefinition { Name = "Lijiang Tower", Type = MapType.Control, Location = "China" });
        AddMap(new MapDefinition { Name = "Nepal", Type = MapType.Control, Location = "Nepal" });
        AddMap(new MapDefinition { Name = "Oasis", Type = MapType.Control, Location = "Iraq" });
        AddMap(new MapDefinition { Name = "Samoa", Type = MapType.Control, Location = "Samoa" });

        // Escort Maps
        AddMap(new MapDefinition { Name = "Circuit Royal", Type = MapType.Escort, Location = "Monaco" });
        AddMap(new MapDefinition { Name = "Dorado", Type = MapType.Escort, Location = "Mexico" });
        AddMap(new MapDefinition { Name = "Havana", Type = MapType.Escort, Location = "Cuba" });
        AddMap(new MapDefinition { Name = "Junkertown", Type = MapType.Escort, Location = "Australia" });
        AddMap(new MapDefinition { Name = "Rialto", Type = MapType.Escort, Location = "Italy" });
        AddMap(new MapDefinition { Name = "Route 66", Type = MapType.Escort, Location = "United States" });
        AddMap(new MapDefinition { Name = "Shambali Monastery", Type = MapType.Escort, Location = "Nepal" });
        AddMap(new MapDefinition { Name = "Watchpoint: Gibraltar", Type = MapType.Escort, Location = "Gibraltar" });

        // Hybrid Maps
        AddMap(new MapDefinition { Name = "Blizzard World", Type = MapType.Hybrid, Location = "United States" });
        AddMap(new MapDefinition { Name = "Eichenwalde", Type = MapType.Hybrid, Location = "Germany" });
        AddMap(new MapDefinition { Name = "Hollywood", Type = MapType.Hybrid, Location = "United States" });
        AddMap(new MapDefinition { Name = "King's Row", Type = MapType.Hybrid, Location = "England" });
        AddMap(new MapDefinition { Name = "Midtown", Type = MapType.Hybrid, Location = "United States" });
        AddMap(new MapDefinition { Name = "Numbani", Type = MapType.Hybrid, Location = "Nigeria" });
        AddMap(new MapDefinition { Name = "Paraíso", Type = MapType.Hybrid, Location = "Brazil" });

        // Push Maps
        AddMap(new MapDefinition { Name = "Colosseo", Type = MapType.Push, Location = "Italy" });
        AddMap(new MapDefinition { Name = "Esperança", Type = MapType.Push, Location = "Portugal" });
        AddMap(new MapDefinition { Name = "New Queen Street", Type = MapType.Push, Location = "Canada" });
        AddMap(new MapDefinition { Name = "Runasapi", Type = MapType.Push, Location = "Peru" });

        // Flashpoint Maps
        AddMap(new MapDefinition { Name = "New Junk City", Type = MapType.Flashpoint, Location = "Australia" });
        AddMap(new MapDefinition { Name = "Suravasa", Type = MapType.Flashpoint, Location = "India" });

        // Clash Maps
        AddMap(new MapDefinition { Name = "Hanaoka", Type = MapType.Clash, Location = "Japan" });
        AddMap(new MapDefinition { Name = "Throne of Anubis", Type = MapType.Clash, Location = "Egypt" });

        // Deathmatch Maps
        AddMap(new MapDefinition { Name = "Château Guillard", Type = MapType.Deathmatch, Location = "France" });
        AddMap(new MapDefinition { Name = "Kanezaka", Type = MapType.Deathmatch, Location = "Japan" });
        AddMap(new MapDefinition { Name = "Malevento", Type = MapType.Deathmatch, Location = "Italy" });
        AddMap(new MapDefinition { Name = "Petra", Type = MapType.Deathmatch, Location = "Jordan" });
    }

    private static void AddMap(MapDefinition map)
    {
        _allMaps.Add(map);
        _mapByName[map.Name.ToLowerInvariant()] = map;
    }

    /// <summary>
    /// Register a GUID mapping for a map
    /// </summary>
    public static void RegisterGuid(string guid, string mapName)
    {
        if (_mapByName.TryGetValue(mapName.ToLowerInvariant(), out var map))
        {
            _mapByGuid[guid.ToLowerInvariant()] = map;
            map.KnownGuids.Add(guid);
        }
    }

    /// <summary>
    /// Get map by GUID
    /// </summary>
    public static MapDefinition GetByGuid(string guid)
    {
        return _mapByGuid.TryGetValue(guid?.ToLowerInvariant() ?? "", out var map) ? map : null;
    }

    /// <summary>
    /// Get map by name
    /// </summary>
    public static MapDefinition GetByName(string name)
    {
        return _mapByName.TryGetValue(name?.ToLowerInvariant() ?? "", out var map) ? map : null;
    }

    /// <summary>
    /// Try to identify map from a partial GUID
    /// </summary>
    public static MapDefinition TryIdentifyFromGuid(string guidPart)
    {
        // Look for any registered GUID containing this part
        foreach (var kvp in _mapByGuid)
        {
            if (kvp.Key.Contains(guidPart.ToLowerInvariant()))
                return kvp.Value;
        }
        return null;
    }

    /// <summary>
    /// Get all maps
    /// </summary>
    public static IReadOnlyList<MapDefinition> GetAllMaps() => _allMaps;

    /// <summary>
    /// Get maps by type
    /// </summary>
    public static IEnumerable<MapDefinition> GetByType(MapType type) => _allMaps.Where(m => m.Type == type);
}

public class MapDefinition
{
    public string Name { get; set; }
    public MapType Type { get; set; }
    public string Location { get; set; }
    public List<string> KnownGuids { get; set; } = new();
    public List<string> Submaps { get; set; } = new(); // For control maps with multiple stages

    public string GameModeDescription => Type switch
    {
        MapType.Control => "King of the Hill - First to 2 wins",
        MapType.Escort => "Payload - Escort the payload to the destination",
        MapType.Hybrid => "Hybrid - Capture the point, then escort the payload",
        MapType.Push => "Push - Push the robot to the enemy spawn",
        MapType.Flashpoint => "Flashpoint - Capture rotating points",
        MapType.Clash => "Clash - 5 points, first to 5 wins",
        MapType.Deathmatch => "Deathmatch - Free-for-all elimination",
        _ => "Unknown game mode"
    };
}

public enum MapType
{
    Control,      // KOTH
    Escort,       // Payload
    Hybrid,       // Point + Payload
    Push,         // Robot push
    Flashpoint,   // Multi-point capture
    Clash,        // Tug-of-war points
    Deathmatch    // FFA
}
