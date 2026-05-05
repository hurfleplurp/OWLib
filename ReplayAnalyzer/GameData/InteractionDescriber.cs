using System;
using System.Collections.Generic;

namespace ReplayAnalyzerTool.GameData;

/// <summary>
/// Describes game interactions in human-readable format.
/// Provides rich descriptions for kills, deaths, abilities, etc.
/// </summary>
public static class InteractionDescriber
{
    private static readonly Random _random = new();

    /// <summary>
    /// Describe a kill event
    /// </summary>
    public static string DescribeKill(
        string killerName, string? killerHero,
        string victimName, string? victimHero,
        string? ability = null, bool wasHeadshot = false, bool wasEnvironmental = false)
    {
        var killer = !string.IsNullOrEmpty(killerHero) ? $"{killerName} ({killerHero})" : killerName;
        var victim = !string.IsNullOrEmpty(victimHero) ? $"{victimName} ({victimHero})" : victimName;

        if (wasEnvironmental)
        {
            return $"{killer} booped {victim} off the map!";
        }

        if (wasHeadshot)
        {
            var templates = new[]
            {
                $"{killer} headshot {victim}",
                $"{killer} landed a critical hit on {victim}",
            };
            return templates[_random.Next(templates.Length)];
        }

        if (!string.IsNullOrEmpty(ability))
        {
            // Check for special ultimate descriptions
            var special = GetSpecialKillDescription(killerHero, ability, victim);
            if (special != null) return special;

            return $"{killer} eliminated {victim} with {ability}";
        }

        return $"{killer} eliminated {victim}";
    }

    private static string? GetSpecialKillDescription(string? killerHero, string ability, string victim)
    {
        if (string.IsNullOrEmpty(killerHero)) return null;

        var key = $"{killerHero.ToLowerInvariant()}:{ability.ToLowerInvariant()}";
        return key switch
        {
            "reinhardt:earthshatter" => $"HAMMER DOWN! {victim} got shattered!",
            "pharah:barrage" => $"Justice rains from above! {victim} got baraged",
            "reaper:death blossom" => $"DIE DIE DIE! {victim} was caught in Death Blossom",
            "junkrat:rip-tire" => $"{victim} was run over by RIP-Tire!",
            "d.va:self-destruct" => $"NERF THIS! {victim} got bombed!",
            "tracer:pulse bomb" => $"Pulse Bomb stuck! {victim} exploded",
            "cassidy:deadeye" => $"It's high noon for {victim}...",
            "hanzo:dragonstrike" => $"The dragon consumed {victim}!",
            "genji:dragonblade" => $"The dragon struck down {victim}!",
            "roadhog:chain hook" => $"Get over here! {victim} got hooked",
            _ => null
        };
    }

    /// <summary>
    /// Describe an ability usage
    /// </summary>
    public static string DescribeAbilityUse(string playerName, string? heroName, string abilityName, string? targetName = null)
    {
        var actor = !string.IsNullOrEmpty(heroName) ? $"{playerName} ({heroName})" : playerName;

        // Check for special ability descriptions
        if (!string.IsNullOrEmpty(heroName))
        {
            var special = GetSpecialAbilityDescription(heroName, abilityName, targetName);
            if (special != null) return special;
        }

        if (!string.IsNullOrEmpty(targetName))
        {
            return $"{actor} used {abilityName} on {targetName}";
        }

        return $"{actor} used {abilityName}";
    }

    /// <summary>
    /// Describe an ultimate ability usage
    /// </summary>
    public static string DescribeUltimateUse(string playerName, string? heroName, string ultimateName)
    {
        var actor = !string.IsNullOrEmpty(heroName) ? $"{playerName} ({heroName})" : playerName;

        // Check for special ultimate voice lines
        if (!string.IsNullOrEmpty(heroName))
        {
            var special = GetUltimateVoiceLine(heroName, ultimateName);
            if (special != null) return $"{actor}: {special}";
        }

        return $"{actor} activated {ultimateName}!";
    }

    /// <summary>
    /// Get the iconic voice line for an ultimate
    /// </summary>
    private static string? GetUltimateVoiceLine(string heroName, string ultimateName)
    {
        var key = $"{heroName.ToLowerInvariant()}:{ultimateName.ToLowerInvariant()}";
        return key switch
        {
            // Tank ultimates
            "reinhardt:earthshatter" => "HAMMER DOWN!",
            "zarya:graviton surge" => "Ogon' po gotovnosti!",
            "d.va:self-destruct" => "NERF THIS!",
            "winston:primal rage" => "*ROAR* (Primal Rage)",
            "roadhog:whole hog" => "*maniacal laughter*",
            "doomfist:meteor strike" => "Meteor Strike!",
            "sigma:gravitic flux" => "Het universum zingt voor mij!",
            "orisa:terra surge" => "Surrender to your better!",
            "junker queen:rampage" => "Bow to your queen!",
            "ramattra:annihilation" => "Embrace oblivion!",
            "wrecking ball:minefield" => "*robot beeping*",
            "mauga:cage fight" => "Let's have some FUN!",

            // Damage ultimates
            "genji:dragonblade" => "Ryūjin no ken wo kurae!",
            "hanzo:dragonstrike" => "Ryū ga waga teki wo kurau!",
            "pharah:barrage" => "Justice rains from above!",
            "reaper:death blossom" => "DIE, DIE, DIE!",
            "soldier: 76:tactical visor" => "I've got you in my sights!",
            "tracer:pulse bomb" => "Here you go!",
            "cassidy:deadeye" => "It's HIGH NOON!",
            "junkrat:rip-tire" => "Fire in the hole!",
            "mei:blizzard" => "Dòng zhù! Bùxǔ zǒu!",
            "sombra:emp" => "¡Apagando las luces!",
            "bastion:configuration: artillery" => "*artillery mode activated*",
            "echo:duplicate" => "Adapting!",
            "widowmaker:infra-sight" => "Personne n'échappe à mon regard!",
            "torbjorn:overcharge" => "Molten Core!",
            "sojourn:overclock" => "Time to take charge!",
            "ashe:b.o.b." => "B.O.B., do something!",

            // Support ultimates
            "mercy:valkyrie" => "Heroes never die!",
            "lucio:sound barrier" => "Let's break it DOWN!",
            "ana:nano boost" => "You're powered up, get in there!",
            "zenyatta:transcendence" => "Experience tranquility.",
            "moira:coalescence" => "Surrender to my will!",
            "brigitte:rally" => "Rally to me!",
            "baptiste:amplification matrix" => "Make them count!",
            "kiriko:kitsune rush" => "Let the Kitsune guide you!",
            "lifeweaver:tree of life" => "Let life blossom!",
            "illari:captive sun" => "Burn!",
            "juno:orbital ray" => "Let's go orbital!",

            _ => null
        };
    }

    private static string? GetSpecialAbilityDescription(string heroName, string abilityName, string? target)
    {
        var key = $"{heroName.ToLowerInvariant()}:{abilityName.ToLowerInvariant()}";
        return key switch
        {
            // Tank abilities
            "reinhardt:earthshatter" => "HAMMER DOWN!",
            "reinhardt:charge" => target != null ? $"Reinhardt pinned {target}!" : "Reinhardt is charging!",
            "zarya:graviton surge" => "Ogon' po gotovnosti! (Graviton Surge)",
            "d.va:self-destruct" => "NERF THIS! (Self-Destruct)",
            "winston:primal rage" => "Winston is ANGRY! (Primal Rage)",
            "roadhog:chain hook" => target != null ? $"Roadhog hooked {target}!" : "Roadhog threw a hook!",
            "doomfist:meteor strike" => "Meteor Strike incoming!",

            // Damage abilities
            "genji:dragonblade" => "Ryūjin no ken wo kurae! (Dragonblade)",
            "hanzo:dragonstrike" => "Ryū ga waga teki wo kurau! (Dragonstrike)",
            "pharah:barrage" => "Justice rains from above! (Barrage)",
            "reaper:death blossom" => "DIE, DIE, DIE! (Death Blossom)",
            "soldier: 76:tactical visor" => "I've got you in my sights (Tactical Visor)",
            "tracer:pulse bomb" => "Tracer threw Pulse Bomb!",
            "cassidy:deadeye" => "It's high noon... (Deadeye)",
            "junkrat:rip-tire" => "Fire in the hole! (RIP-Tire)",
            "mei:blizzard" => "Freeze! Don't move! (Blizzard)",
            "sombra:emp" => "¡Apagando las luces! (EMP)",
            "sombra:hack" => target != null ? $"Sombra hacked {target}!" : "Sombra is hacking!",
            "ashe:b.o.b." => "B.O.B., do something! (B.O.B.)",

            // Support abilities
            "mercy:resurrect" => target != null ? $"Heroes never die! Mercy resurrected {target}!" : "Heroes never die! (Resurrect)",
            "mercy:valkyrie" => "Heroes never die! (Valkyrie)",
            "ana:nano boost" => target != null ? $"You're powered up! Ana nano-boosted {target}!" : "You're powered up, get in there! (Nano Boost)",
            "ana:sleep dart" => target != null ? $"Ana slept {target}!" : "Ana threw a sleep dart!",
            "lúcio:sound barrier" => "Let's break it DOWN! (Sound Barrier)",
            "zenyatta:transcendence" => "Experience tranquility (Transcendence)",
            "moira:coalescence" => "Surrender to my will! (Coalescence)",
            "kiriko:kitsune rush" => "Kitsune Rush!",
            "kiriko:protection suzu" => target != null ? $"Kiriko cleansed {target}!" : "Kiriko used Protection Suzu!",
            "baptiste:immortality field" => "Baptiste deployed Immortality Field!",
            "brigitte:rally" => "Rally to me! (Rally)",
            "lifeweaver:tree of life" => "Tree of Life planted!",
            "lifeweaver:life grip" => target != null ? $"Lifeweaver pulled {target} to safety!" : "Lifeweaver used Life Grip!",

            _ => null
        };
    }

    /// <summary>
    /// Describe a death
    /// </summary>
    public static string DescribeDeath(
        string victimName, string? victimHero,
        string? killerName = null, string? killerHero = null,
        string? cause = null)
    {
        var victim = !string.IsNullOrEmpty(victimHero) ? $"{victimName} ({victimHero})" : victimName;

        if (!string.IsNullOrEmpty(killerName))
        {
            var killer = !string.IsNullOrEmpty(killerHero) ? $"{killerName} ({killerHero})" : killerName;
            if (!string.IsNullOrEmpty(cause))
            {
                return $"{victim} was killed by {killer} ({cause})";
            }
            return $"{victim} was killed by {killer}";
        }

        if (!string.IsNullOrEmpty(cause))
        {
            if (cause.Contains("Environmental") || cause.Contains("Fall"))
            {
                return $"{victim} fell to their death";
            }
            return $"{victim} died ({cause})";
        }

        return $"{victim} died";
    }

    /// <summary>
    /// Describe a resurrection
    /// </summary>
    public static string DescribeResurrection(
        string healerName, string? healerHero,
        string targetName, string? targetHero)
    {
        var healer = !string.IsNullOrEmpty(healerHero) ? $"{healerName} ({healerHero})" : healerName;
        var target = !string.IsNullOrEmpty(targetHero) ? $"{targetName} ({targetHero})" : targetName;

        if (healerHero?.ToLowerInvariant() == "mercy")
        {
            return $"Heroes never die! Mercy resurrected {target}!";
        }

        return $"{healer} revived {target}";
    }

    /// <summary>
    /// Describe an ultimate usage
    /// </summary>
    public static string DescribeUltimate(string playerName, string? heroName, string ultimateName)
    {
        if (!string.IsNullOrEmpty(heroName))
        {
            var special = GetSpecialAbilityDescription(heroName, ultimateName, null);
            if (special != null) return special;
        }

        var actor = !string.IsNullOrEmpty(heroName) ? $"{playerName} ({heroName})" : playerName;
        return $"{actor} activated {ultimateName}!";
    }

    /// <summary>
    /// Describe objective progress
    /// </summary>
    public static string DescribeObjective(ObjectiveEventType eventType, string? playerName = null, string? heroName = null)
    {
        var actor = playerName != null
            ? (!string.IsNullOrEmpty(heroName) ? $"{playerName} ({heroName})" : playerName)
            : null;

        return eventType switch
        {
            ObjectiveEventType.PointCapturing => actor != null ? $"{actor} is capturing the point" : "Point is being captured",
            ObjectiveEventType.PointCaptured => "Point captured!",
            ObjectiveEventType.PointLost => "Point lost!",
            ObjectiveEventType.PayloadMoving => actor != null ? $"{actor} is pushing the payload" : "Payload is moving",
            ObjectiveEventType.PayloadStalled => "Payload stopped!",
            ObjectiveEventType.PayloadCheckpoint => "Payload reached checkpoint!",
            ObjectiveEventType.PayloadDelivered => "Payload delivered!",
            ObjectiveEventType.RobotContested => "Robot is contested!",
            ObjectiveEventType.FlashpointCapturing => "Flashpoint capturing...",
            ObjectiveEventType.ClashPointFlipped => "Point flipped!",
            ObjectiveEventType.Overtime => "OVERTIME!",
            ObjectiveEventType.RoundEnd => "Round ended!",
            ObjectiveEventType.MatchEnd => "Match ended!",
            _ => "Objective event"
        };
    }

    /// <summary>
    /// Describe a status effect application
    /// </summary>
    public static string DescribeStatusEffect(string targetName, string effectName, float duration)
    {
        var durationStr = duration > 0 ? $" ({duration:F1}s)" : "";
        return $"{targetName} is {effectName.ToLowerInvariant()}{durationStr}";
    }

    /// <summary>
    /// Describe ability cooldown state
    /// </summary>
    public static string DescribeCooldown(string heroName, string abilityName, float remaining, float total)
    {
        if (remaining <= 0)
        {
            return $"{heroName}'s {abilityName} is ready!";
        }
        return $"{heroName}'s {abilityName}: {remaining:F1}s / {total:F1}s";
    }

    /// <summary>
    /// Describe ultimate charge
    /// </summary>
    public static string DescribeUltCharge(string heroName, float chargePercent)
    {
        if (chargePercent >= 1f)
        {
            return $"{heroName}'s ultimate is READY!";
        }
        return $"{heroName}'s ultimate: {chargePercent:P0}";
    }
}
