using System;
using System.Collections.Generic;
using System.Linq;

namespace ReplayAnalyzerTool.GameData;

/// <summary>
/// Static database of Overwatch 2 heroes with their abilities and stats.
/// This is used when we don't have access to game data files.
/// </summary>
public static class HeroDatabase
{
    private static readonly Dictionary<string, HeroDefinition> _heroByGuid = new();
    private static readonly Dictionary<string, HeroDefinition> _heroByName = new();
    private static readonly List<HeroDefinition> _allHeroes = new();

    static HeroDatabase()
    {
        InitializeHeroes();
    }

    private static void InitializeHeroes()
    {
        // Tank Heroes
        AddHero(new HeroDefinition
        {
            Name = "D.Va",
            Role = HeroRole.Tank,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Fusion Cannons", Category = AbilityCategory.Primary, Description = "Short-range automatic weapons" },
                new() { Name = "Light Gun", Category = AbilityCategory.Primary, Description = "Mid-range automatic pistol (Pilot)" },
                new() { Name = "Boosters", Category = AbilityCategory.Ability, Cooldown = 4f, Duration = 2f, Description = "Fly in direction of movement" },
                new() { Name = "Defense Matrix", Category = AbilityCategory.Ability, Resource = 100, ResourceRegen = 12.5f, Duration = 3f, Description = "Block projectiles in an area" },
                new() { Name = "Micro Missiles", Category = AbilityCategory.Ability, Cooldown = 8f, Description = "Launch a volley of missiles" },
                new() { Name = "Self-Destruct", Category = AbilityCategory.Ultimate, UltCost = 1540, Duration = 3f, Description = "Eject and make mech explode" },
                new() { Name = "Call Mech", Category = AbilityCategory.Ultimate, UltCost = 900, Description = "Call a new mech" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Doomfist",
            Role = HeroRole.Tank,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Hand Cannon", Category = AbilityCategory.Primary, Ammo = 4, ReloadTime = 0.4f, Description = "Short-range shotgun" },
                new() { Name = "Rocket Punch", Category = AbilityCategory.Ability, Cooldown = 4f, MaxCharge = 1.4f, Description = "Charge forward and punch" },
                new() { Name = "Seismic Slam", Category = AbilityCategory.Ability, Cooldown = 7f, Description = "Leap and create shockwave" },
                new() { Name = "Power Block", Category = AbilityCategory.Ability, Cooldown = 7f, Duration = 2.5f, Description = "Block and absorb damage" },
                new() { Name = "Meteor Strike", Category = AbilityCategory.Ultimate, UltCost = 1540, Description = "Leap up and crash down" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Junker Queen",
            Role = HeroRole.Tank,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Scattergun", Category = AbilityCategory.Primary, Ammo = 6, ReloadTime = 1.5f, Description = "Pump-action shotgun" },
                new() { Name = "Jagged Blade", Category = AbilityCategory.Secondary, Cooldown = 6f, Description = "Throw and recall blade" },
                new() { Name = "Commanding Shout", Category = AbilityCategory.Ability, Cooldown = 11f, Duration = 5f, Description = "Grant health and speed to allies" },
                new() { Name = "Carnage", Category = AbilityCategory.Ability, Cooldown = 8f, Description = "Swing axe for damage" },
                new() { Name = "Rampage", Category = AbilityCategory.Ultimate, UltCost = 2200, Description = "Charge forward, wound enemies" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Mauga",
            Role = HeroRole.Tank,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Incendiary Chaingun", Category = AbilityCategory.Primary, Ammo = 300, Description = "Left chaingun, ignites enemies" },
                new() { Name = "Volatile Chaingun", Category = AbilityCategory.Primary, Ammo = 300, Description = "Right chaingun, crits burning enemies" },
                new() { Name = "Overrun", Category = AbilityCategory.Ability, Cooldown = 8f, Description = "Charge and stomp" },
                new() { Name = "Cardiac Overdrive", Category = AbilityCategory.Ability, Cooldown = 12f, Duration = 4f, Description = "Damage dealt heals nearby allies" },
                new() { Name = "Cage Fight", Category = AbilityCategory.Ultimate, UltCost = 2100, Duration = 8f, Description = "Trap enemies in barrier cage" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Orisa",
            Role = HeroRole.Tank,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Augmented Fusion Driver", Category = AbilityCategory.Primary, Ammo = 125, Description = "Automatic heat-based weapon" },
                new() { Name = "Energy Javelin", Category = AbilityCategory.Ability, Cooldown = 6f, Description = "Throw javelin to stun" },
                new() { Name = "Fortify", Category = AbilityCategory.Ability, Cooldown = 10f, Duration = 4.5f, Description = "Reduce damage, immune to CC" },
                new() { Name = "Javelin Spin", Category = AbilityCategory.Ability, Cooldown = 7f, Duration = 1.75f, Description = "Spin javelin to block and push" },
                new() { Name = "Terra Surge", Category = AbilityCategory.Ultimate, UltCost = 2380, MaxCharge = 4f, Description = "Pull enemies and charge damage" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Ramattra",
            Role = HeroRole.Tank,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Void Accelerator", Category = AbilityCategory.Primary, Ammo = 100, Description = "Projectile-based weapon (Omnic)" },
                new() { Name = "Pummel", Category = AbilityCategory.Primary, Description = "Melee punches (Nemesis)" },
                new() { Name = "Void Barrier", Category = AbilityCategory.Secondary, Cooldown = 13f, Duration = 4f, Description = "Deploy a barrier" },
                new() { Name = "Nemesis Form", Category = AbilityCategory.Ability, Cooldown = 8f, Duration = 8f, Description = "Transform into armored form" },
                new() { Name = "Ravenous Vortex", Category = AbilityCategory.Ability, Cooldown = 11f, Duration = 3f, Description = "Create slowing vortex" },
                new() { Name = "Annihilation", Category = AbilityCategory.Ultimate, UltCost = 2100, Description = "Create damaging swarm" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Reinhardt",
            Role = HeroRole.Tank,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Rocket Hammer", Category = AbilityCategory.Primary, Description = "Melee swing" },
                new() { Name = "Barrier Field", Category = AbilityCategory.Secondary, Resource = 1200, ResourceRegen = 144f, Description = "Hold shield" },
                new() { Name = "Charge", Category = AbilityCategory.Ability, Cooldown = 8f, Charges = 2, Description = "Charge forward, pin enemy" },
                new() { Name = "Fire Strike", Category = AbilityCategory.Ability, Cooldown = 6f, Charges = 2, Description = "Launch fiery projectile" },
                new() { Name = "Earthshatter", Category = AbilityCategory.Ultimate, UltCost = 1540, Description = "Knock down enemies in cone" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Roadhog",
            Role = HeroRole.Tank,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Scrap Gun", Category = AbilityCategory.Primary, Ammo = 6, ReloadTime = 2f, Description = "Short-range shotgun" },
                new() { Name = "Chain Hook", Category = AbilityCategory.Ability, Cooldown = 6f, Description = "Pull enemy to you" },
                new() { Name = "Take a Breather", Category = AbilityCategory.Ability, Cooldown = 1f, Resource = 450, Description = "Heal yourself, reduce damage" },
                new() { Name = "Pig Pen", Category = AbilityCategory.Ability, Cooldown = 14f, Duration = 6f, Description = "Deploy trap that slows" },
                new() { Name = "Whole Hog", Category = AbilityCategory.Ultimate, UltCost = 2100, Duration = 5.5f, Description = "Knockback with crank gun" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Sigma",
            Role = HeroRole.Tank,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Hyperspheres", Category = AbilityCategory.Primary, Ammo = 2, ReloadTime = 1.5f, Description = "Bouncing projectiles" },
                new() { Name = "Kinetic Grasp", Category = AbilityCategory.Ability, Cooldown = 10f, Duration = 2.5f, Description = "Absorb projectiles for shields" },
                new() { Name = "Accretion", Category = AbilityCategory.Ability, Cooldown = 10f, Description = "Throw rock to knockdown" },
                new() { Name = "Experimental Barrier", Category = AbilityCategory.Secondary, Resource = 700, ResourceRegen = 80f, Description = "Deployable barrier" },
                new() { Name = "Gravitic Flux", Category = AbilityCategory.Ultimate, UltCost = 2100, Description = "Lift and slam enemies" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Winston",
            Role = HeroRole.Tank,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Tesla Cannon", Category = AbilityCategory.Primary, Ammo = 100, ReloadTime = 1.5f, Description = "Electric weapon, chains to enemies" },
                new() { Name = "Jump Pack", Category = AbilityCategory.Ability, Cooldown = 5f, Description = "Leap and damage on landing" },
                new() { Name = "Barrier Projector", Category = AbilityCategory.Ability, Cooldown = 12f, Duration = 8f, Description = "Deploy dome shield" },
                new() { Name = "Primal Rage", Category = AbilityCategory.Ultimate, UltCost = 1540, Duration = 10f, Description = "Enhanced melee and health" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Wrecking Ball",
            Role = HeroRole.Tank,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Quad Cannons", Category = AbilityCategory.Primary, Ammo = 80, ReloadTime = 2f, Description = "Automatic assault weapon" },
                new() { Name = "Roll", Category = AbilityCategory.Ability, Cooldown = 0f, Description = "Transform into ball mode" },
                new() { Name = "Grappling Claw", Category = AbilityCategory.Ability, Cooldown = 5f, Description = "Grapple to surface and swing" },
                new() { Name = "Adaptive Shield", Category = AbilityCategory.Ability, Cooldown = 13f, Duration = 9f, Description = "Gain shields based on nearby enemies" },
                new() { Name = "Piledriver", Category = AbilityCategory.Ability, Cooldown = 8f, Description = "Slam down and launch enemies" },
                new() { Name = "Minefield", Category = AbilityCategory.Ultimate, UltCost = 2100, Duration = 20f, Description = "Deploy proximity mines" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Zarya",
            Role = HeroRole.Tank,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Particle Cannon", Category = AbilityCategory.Primary, Ammo = 100, Description = "Beam weapon (scales with charge)" },
                new() { Name = "Particle Cannon Alt", Category = AbilityCategory.Secondary, Description = "Explosive projectile" },
                new() { Name = "Particle Barrier", Category = AbilityCategory.Ability, Cooldown = 10f, Charges = 2, Duration = 2.5f, Description = "Personal bubble shield" },
                new() { Name = "Projected Barrier", Category = AbilityCategory.Ability, Cooldown = 10f, Charges = 2, Duration = 2.5f, Description = "Ally bubble shield" },
                new() { Name = "Graviton Surge", Category = AbilityCategory.Ultimate, UltCost = 2100, Duration = 3.5f, Description = "Pull enemies into gravity well" }
            }
        });

        // Damage Heroes
        AddHero(new HeroDefinition
        {
            Name = "Ashe",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "The Viper", Category = AbilityCategory.Primary, Ammo = 15, ReloadTime = 3.5f, Description = "Lever-action rifle" },
                new() { Name = "Coach Gun", Category = AbilityCategory.Ability, Cooldown = 10f, Description = "Knockback shotgun" },
                new() { Name = "Dynamite", Category = AbilityCategory.Ability, Cooldown = 10f, Description = "Throwable explosive" },
                new() { Name = "B.O.B.", Category = AbilityCategory.Ultimate, UltCost = 2240, Duration = 10f, Description = "Summon B.O.B. to fight" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Bastion",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Configuration: Assault", Category = AbilityCategory.Primary, Ammo = 25, ReloadTime = 1.5f, Description = "Submachine gun" },
                new() { Name = "Reconfigure", Category = AbilityCategory.Ability, Cooldown = 10f, Duration = 6f, Description = "Enter assault mode" },
                new() { Name = "A-36 Tactical Grenade", Category = AbilityCategory.Ability, Cooldown = 8f, Description = "Bouncing grenade" },
                new() { Name = "Configuration: Artillery", Category = AbilityCategory.Ultimate, UltCost = 2310, Description = "Launch artillery strikes" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Cassidy",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Peacekeeper", Category = AbilityCategory.Primary, Ammo = 6, ReloadTime = 1.5f, Description = "Revolver" },
                new() { Name = "Fan the Hammer", Category = AbilityCategory.Secondary, Description = "Rapid fire remaining ammo" },
                new() { Name = "Combat Roll", Category = AbilityCategory.Ability, Cooldown = 6f, Description = "Roll and reload" },
                new() { Name = "Magnetic Grenade", Category = AbilityCategory.Ability, Cooldown = 10f, Description = "Homing sticky grenade" },
                new() { Name = "Deadeye", Category = AbilityCategory.Ultimate, UltCost = 1680, MaxCharge = 6f, Description = "Lock on and fire" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Echo",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Tri-Shot", Category = AbilityCategory.Primary, Ammo = 15, ReloadTime = 1.5f, Description = "Triangle pattern projectiles" },
                new() { Name = "Sticky Bombs", Category = AbilityCategory.Ability, Cooldown = 6f, Description = "Launch sticky explosives" },
                new() { Name = "Flight", Category = AbilityCategory.Ability, Cooldown = 6f, Duration = 3f, Description = "Fly freely" },
                new() { Name = "Focusing Beam", Category = AbilityCategory.Ability, Cooldown = 8f, Duration = 2.5f, Description = "High damage beam on low health targets" },
                new() { Name = "Duplicate", Category = AbilityCategory.Ultimate, UltCost = 1960, Duration = 15f, Description = "Copy enemy hero" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Genji",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Shuriken", Category = AbilityCategory.Primary, Ammo = 30, ReloadTime = 1.5f, Description = "Throwing stars" },
                new() { Name = "Shuriken Fan", Category = AbilityCategory.Secondary, Description = "Throw 3 in spread" },
                new() { Name = "Swift Strike", Category = AbilityCategory.Ability, Cooldown = 7f, Description = "Dash forward, reset on elim" },
                new() { Name = "Deflect", Category = AbilityCategory.Ability, Cooldown = 7f, Duration = 2f, Description = "Reflect projectiles" },
                new() { Name = "Dragonblade", Category = AbilityCategory.Ultimate, UltCost = 1680, Duration = 6f, Description = "Melee sword attacks" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Hanzo",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Storm Bow", Category = AbilityCategory.Primary, Ammo = 25, Description = "Chargeable bow" },
                new() { Name = "Storm Arrows", Category = AbilityCategory.Ability, Cooldown = 10f, Charges = 5, Description = "Rapid fire arrows" },
                new() { Name = "Sonic Arrow", Category = AbilityCategory.Ability, Cooldown = 12f, Duration = 6f, Description = "Reveals enemies in area" },
                new() { Name = "Lunge", Category = AbilityCategory.Ability, Cooldown = 4f, Description = "Horizontal leap" },
                new() { Name = "Dragonstrike", Category = AbilityCategory.Ultimate, UltCost = 1680, Description = "Launch spirit dragons" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Junkrat",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Frag Launcher", Category = AbilityCategory.Primary, Ammo = 6, ReloadTime = 1.5f, Description = "Bouncing grenades" },
                new() { Name = "Concussion Mine", Category = AbilityCategory.Ability, Cooldown = 8f, Charges = 2, Description = "Remote explosive" },
                new() { Name = "Steel Trap", Category = AbilityCategory.Ability, Cooldown = 10f, Description = "Rooting trap" },
                new() { Name = "RIP-Tire", Category = AbilityCategory.Ultimate, UltCost = 1820, Duration = 10f, Description = "Remote controlled bomb" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Mei",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Endothermic Blaster", Category = AbilityCategory.Primary, Ammo = 120, Description = "Freezing spray" },
                new() { Name = "Icicle", Category = AbilityCategory.Secondary, Description = "Chargeable icicle shot" },
                new() { Name = "Cryo-Freeze", Category = AbilityCategory.Ability, Cooldown = 12f, Duration = 3f, Description = "Invulnerable ice block, heals" },
                new() { Name = "Ice Wall", Category = AbilityCategory.Ability, Cooldown = 11f, Duration = 4.5f, Description = "Create ice barrier" },
                new() { Name = "Blizzard", Category = AbilityCategory.Ultimate, UltCost = 1540, Duration = 5f, Description = "Freeze enemies in area" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Pharah",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Rocket Launcher", Category = AbilityCategory.Primary, Ammo = 6, ReloadTime = 1.5f, Description = "Explosive rockets" },
                new() { Name = "Hover Jets", Category = AbilityCategory.Passive, Description = "Hold jump to hover" },
                new() { Name = "Jump Jet", Category = AbilityCategory.Ability, Cooldown = 10f, Description = "Fly upward" },
                new() { Name = "Concussive Blast", Category = AbilityCategory.Ability, Cooldown = 7f, Description = "Knockback blast" },
                new() { Name = "Barrage", Category = AbilityCategory.Ultimate, UltCost = 2100, Duration = 3f, Description = "Rapid fire mini-rockets" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Reaper",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Hellfire Shotguns", Category = AbilityCategory.Primary, Ammo = 8, ReloadTime = 1.5f, Description = "Dual shotguns" },
                new() { Name = "Wraith Form", Category = AbilityCategory.Ability, Cooldown = 7f, Duration = 3f, Description = "Invulnerable, move faster" },
                new() { Name = "Shadow Step", Category = AbilityCategory.Ability, Cooldown = 10f, Description = "Teleport to location" },
                new() { Name = "Death Blossom", Category = AbilityCategory.Ultimate, UltCost = 2100, Duration = 3f, Description = "Spin and fire in all directions" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Sojourn",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Railgun", Category = AbilityCategory.Primary, Ammo = 45, ReloadTime = 1.2f, Description = "Rapid fire, builds charge" },
                new() { Name = "Railgun Alt", Category = AbilityCategory.Secondary, Description = "High-damage charged shot" },
                new() { Name = "Power Slide", Category = AbilityCategory.Ability, Cooldown = 6f, Description = "Slide and cancel into jump" },
                new() { Name = "Disruptor Shot", Category = AbilityCategory.Ability, Cooldown = 15f, Duration = 4f, Description = "Energy field that slows and damages" },
                new() { Name = "Overclock", Category = AbilityCategory.Ultimate, UltCost = 2100, Duration = 8f, Description = "Auto-charge railgun, piercing shots" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Soldier: 76",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Heavy Pulse Rifle", Category = AbilityCategory.Primary, Ammo = 30, ReloadTime = 1.5f, Description = "Automatic rifle" },
                new() { Name = "Helix Rockets", Category = AbilityCategory.Secondary, Cooldown = 6f, Description = "Explosive rockets" },
                new() { Name = "Sprint", Category = AbilityCategory.Ability, Cooldown = 0f, Description = "Run faster" },
                new() { Name = "Biotic Field", Category = AbilityCategory.Ability, Cooldown = 15f, Duration = 5f, Description = "Healing field" },
                new() { Name = "Tactical Visor", Category = AbilityCategory.Ultimate, UltCost = 2310, Duration = 6f, Description = "Auto-aim" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Sombra",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Machine Pistol", Category = AbilityCategory.Primary, Ammo = 60, ReloadTime = 1.4f, Description = "Automatic weapon" },
                new() { Name = "Hack", Category = AbilityCategory.Ability, Cooldown = 4f, Duration = 1.75f, Description = "Disable abilities, reveal" },
                new() { Name = "Stealth", Category = AbilityCategory.Ability, Cooldown = 6f, Description = "Turn invisible" },
                new() { Name = "Translocator", Category = AbilityCategory.Ability, Cooldown = 5f, Description = "Throw beacon, teleport to it" },
                new() { Name = "EMP", Category = AbilityCategory.Ultimate, UltCost = 1400, Description = "Hack all nearby enemies" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Symmetra",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Photon Projector", Category = AbilityCategory.Primary, Ammo = 70, Description = "Ramping beam" },
                new() { Name = "Photon Orb", Category = AbilityCategory.Secondary, Description = "Chargeable energy ball" },
                new() { Name = "Sentry Turret", Category = AbilityCategory.Ability, Cooldown = 10f, Charges = 3, Description = "Deploy slowing turret" },
                new() { Name = "Teleporter", Category = AbilityCategory.Ability, Cooldown = 12f, Duration = 10f, Description = "Create two-way teleporter" },
                new() { Name = "Photon Barrier", Category = AbilityCategory.Ultimate, UltCost = 1820, Duration = 12f, Description = "Infinite barrier wall" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Torbjörn",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Rivet Gun", Category = AbilityCategory.Primary, Ammo = 18, ReloadTime = 2f, Description = "Arcing projectile" },
                new() { Name = "Rivet Gun Alt", Category = AbilityCategory.Secondary, Description = "Shotgun blast" },
                new() { Name = "Deploy Turret", Category = AbilityCategory.Ability, Cooldown = 10f, Description = "Build auto-turret" },
                new() { Name = "Overload", Category = AbilityCategory.Ability, Cooldown = 10f, Duration = 5f, Description = "Gain speed, armor, fire rate" },
                new() { Name = "Molten Core", Category = AbilityCategory.Ultimate, UltCost = 2310, Duration = 10f, Description = "Launch molten slag pools" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Tracer",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Pulse Pistols", Category = AbilityCategory.Primary, Ammo = 40, ReloadTime = 1f, Description = "Dual automatic pistols" },
                new() { Name = "Blink", Category = AbilityCategory.Ability, Cooldown = 3f, Charges = 3, Description = "Teleport forward" },
                new() { Name = "Recall", Category = AbilityCategory.Ability, Cooldown = 11f, Description = "Return to previous state" },
                new() { Name = "Pulse Bomb", Category = AbilityCategory.Ultimate, UltCost = 1260, Description = "Sticky explosive" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Venture",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Smart Excavator", Category = AbilityCategory.Primary, Ammo = 6, Description = "Chargeable pickaxe projectile" },
                new() { Name = "Clobber", Category = AbilityCategory.Secondary, Description = "Melee combo" },
                new() { Name = "Burrow", Category = AbilityCategory.Ability, Cooldown = 8f, Description = "Dig underground, invulnerable" },
                new() { Name = "Drill Dash", Category = AbilityCategory.Ability, Cooldown = 5f, Description = "Drill forward" },
                new() { Name = "Tectonic Shock", Category = AbilityCategory.Ultimate, UltCost = 1820, Description = "Send shockwaves through ground" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Widowmaker",
            Role = HeroRole.Damage,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Widow's Kiss", Category = AbilityCategory.Primary, Ammo = 35, ReloadTime = 1.5f, Description = "Automatic rifle / Scoped sniper" },
                new() { Name = "Grappling Hook", Category = AbilityCategory.Ability, Cooldown = 10f, Description = "Grapple to ledge" },
                new() { Name = "Venom Mine", Category = AbilityCategory.Ability, Cooldown = 15f, Description = "Proximity poison trap" },
                new() { Name = "Infra-Sight", Category = AbilityCategory.Ultimate, UltCost = 1540, Duration = 15f, Description = "Reveal all enemies" }
            }
        });

        // Support Heroes
        AddHero(new HeroDefinition
        {
            Name = "Ana",
            Role = HeroRole.Support,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Biotic Rifle", Category = AbilityCategory.Primary, Ammo = 14, ReloadTime = 1.5f, Description = "Heal allies, damage enemies" },
                new() { Name = "Sleep Dart", Category = AbilityCategory.Ability, Cooldown = 15f, Duration = 5f, Description = "Tranquilize enemy" },
                new() { Name = "Biotic Grenade", Category = AbilityCategory.Ability, Cooldown = 10f, Duration = 3f, Description = "Heal boost or anti-heal" },
                new() { Name = "Nano Boost", Category = AbilityCategory.Ultimate, UltCost = 2100, Duration = 8f, Description = "Empower ally" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Baptiste",
            Role = HeroRole.Support,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Biotic Launcher", Category = AbilityCategory.Primary, Ammo = 45, ReloadTime = 1.5f, Description = "Burst fire weapon" },
                new() { Name = "Biotic Launcher Alt", Category = AbilityCategory.Secondary, Ammo = 13, Description = "Healing grenades" },
                new() { Name = "Regenerative Burst", Category = AbilityCategory.Ability, Cooldown = 15f, Duration = 5f, Description = "Heal over time in area" },
                new() { Name = "Immortality Field", Category = AbilityCategory.Ability, Cooldown = 25f, Duration = 5f, Description = "Prevent lethal damage" },
                new() { Name = "Amplification Matrix", Category = AbilityCategory.Ultimate, UltCost = 2310, Duration = 10f, Description = "Double damage and healing through window" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Brigitte",
            Role = HeroRole.Support,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Rocket Flail", Category = AbilityCategory.Primary, Description = "Melee weapon" },
                new() { Name = "Barrier Shield", Category = AbilityCategory.Secondary, Resource = 300, ResourceRegen = 85f, Description = "Personal shield" },
                new() { Name = "Shield Bash", Category = AbilityCategory.Ability, Cooldown = 5f, Description = "Dash and stun" },
                new() { Name = "Repair Pack", Category = AbilityCategory.Ability, Cooldown = 6f, Charges = 3, Description = "Heal ally" },
                new() { Name = "Whip Shot", Category = AbilityCategory.Ability, Cooldown = 4f, Description = "Knockback ranged attack" },
                new() { Name = "Rally", Category = AbilityCategory.Ultimate, UltCost = 2420, Duration = 10f, Description = "Aura armor and speed" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Illari",
            Role = HeroRole.Support,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Solar Rifle", Category = AbilityCategory.Primary, Resource = 14, Description = "Chargeable long-range shot" },
                new() { Name = "Solar Rifle Alt", Category = AbilityCategory.Secondary, Description = "Healing beam" },
                new() { Name = "Outburst", Category = AbilityCategory.Ability, Cooldown = 7f, Description = "Knock back and launch yourself" },
                new() { Name = "Healing Pylon", Category = AbilityCategory.Ability, Cooldown = 8f, Description = "Deploy healing turret" },
                new() { Name = "Captive Sun", Category = AbilityCategory.Ultimate, UltCost = 2100, Description = "Sunstruck enemies explode on damage" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Juno",
            Role = HeroRole.Support,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Mediblaster", Category = AbilityCategory.Primary, Ammo = 25, Description = "Heal allies, damage enemies" },
                new() { Name = "Pulsar Torpedoes", Category = AbilityCategory.Secondary, Cooldown = 6f, Description = "Seeking torpedoes heal and damage" },
                new() { Name = "Hyper Ring", Category = AbilityCategory.Ability, Cooldown = 12f, Duration = 6f, Description = "Speed boost ring" },
                new() { Name = "Glide Boost", Category = AbilityCategory.Ability, Cooldown = 0f, Description = "Horizontal glide" },
                new() { Name = "Orbital Ray", Category = AbilityCategory.Ultimate, UltCost = 2100, Duration = 4f, Description = "Heal and damage boost beam" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Kiriko",
            Role = HeroRole.Support,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Healing Ofuda", Category = AbilityCategory.Primary, Ammo = 10, Description = "Homing healing talismans" },
                new() { Name = "Kunai", Category = AbilityCategory.Secondary, Ammo = 12, Description = "Throwing knives (headshot crit)" },
                new() { Name = "Swift Step", Category = AbilityCategory.Ability, Cooldown = 7f, Description = "Teleport to ally" },
                new() { Name = "Protection Suzu", Category = AbilityCategory.Ability, Cooldown = 14f, Duration = 0.85f, Description = "Brief invulnerability, cleanse" },
                new() { Name = "Kitsune Rush", Category = AbilityCategory.Ultimate, UltCost = 1680, Duration = 10.5f, Description = "Path of speed/cooldown/attack boost" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Lifeweaver",
            Role = HeroRole.Support,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Healing Blossom", Category = AbilityCategory.Primary, Resource = 1.2f, Description = "Chargeable heal burst" },
                new() { Name = "Thorn Volley", Category = AbilityCategory.Secondary, Ammo = 60, Description = "Rapid fire projectiles" },
                new() { Name = "Petal Platform", Category = AbilityCategory.Ability, Cooldown = 12f, Description = "Create rising platform" },
                new() { Name = "Rejuvenating Dash", Category = AbilityCategory.Ability, Cooldown = 5f, Description = "Dash and heal self" },
                new() { Name = "Life Grip", Category = AbilityCategory.Ability, Cooldown = 20f, Description = "Pull ally to you, invuln them" },
                new() { Name = "Tree of Life", Category = AbilityCategory.Ultimate, UltCost = 2100, Duration = 15f, Description = "Deploy healing tree" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Lúcio",
            Role = HeroRole.Support,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Sonic Amplifier", Category = AbilityCategory.Primary, Ammo = 20, ReloadTime = 1.5f, Description = "Projectile weapon" },
                new() { Name = "Soundwave", Category = AbilityCategory.Secondary, Cooldown = 4f, Description = "Knockback boop" },
                new() { Name = "Crossfade", Category = AbilityCategory.Ability, Cooldown = 0f, Description = "Switch healing/speed song" },
                new() { Name = "Amp It Up", Category = AbilityCategory.Ability, Cooldown = 11f, Duration = 3f, Description = "Boost active aura" },
                new() { Name = "Sound Barrier", Category = AbilityCategory.Ultimate, UltCost = 2940, Description = "Grant overhealth shield" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Mercy",
            Role = HeroRole.Support,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Caduceus Staff", Category = AbilityCategory.Primary, Description = "Healing beam" },
                new() { Name = "Caduceus Staff Alt", Category = AbilityCategory.Secondary, Description = "Damage boost beam" },
                new() { Name = "Caduceus Blaster", Category = AbilityCategory.Primary, Ammo = 25, ReloadTime = 1.4f, Description = "Pistol" },
                new() { Name = "Guardian Angel", Category = AbilityCategory.Ability, Cooldown = 1.5f, Description = "Fly to ally" },
                new() { Name = "Resurrect", Category = AbilityCategory.Ability, Cooldown = 30f, Description = "Revive dead ally" },
                new() { Name = "Valkyrie", Category = AbilityCategory.Ultimate, UltCost = 1820, Duration = 15f, Description = "Enhanced flight and chain beams" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Moira",
            Role = HeroRole.Support,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Biotic Grasp", Category = AbilityCategory.Primary, Resource = 100, ResourceRegen = 2.4f, Description = "Healing spray" },
                new() { Name = "Biotic Grasp Alt", Category = AbilityCategory.Secondary, Description = "Damage beam, restores resource" },
                new() { Name = "Biotic Orb", Category = AbilityCategory.Ability, Cooldown = 8f, Description = "Healing or damage orb" },
                new() { Name = "Fade", Category = AbilityCategory.Ability, Cooldown = 6f, Duration = 0.8f, Description = "Invulnerable teleport" },
                new() { Name = "Coalescence", Category = AbilityCategory.Ultimate, UltCost = 2380, Duration = 8f, Description = "Heal and damage beam" }
            }
        });

        AddHero(new HeroDefinition
        {
            Name = "Zenyatta",
            Role = HeroRole.Support,
            Abilities = new List<AbilityDefinition>
            {
                new() { Name = "Orb of Destruction", Category = AbilityCategory.Primary, Ammo = 25, Description = "Projectile orbs" },
                new() { Name = "Orb Volley", Category = AbilityCategory.Secondary, Description = "Chargeable orb burst" },
                new() { Name = "Orb of Harmony", Category = AbilityCategory.Ability, Cooldown = 0f, Description = "Heal over time on ally" },
                new() { Name = "Orb of Discord", Category = AbilityCategory.Ability, Cooldown = 0f, Description = "Damage amp on enemy" },
                new() { Name = "Snap Kick", Category = AbilityCategory.Passive, Description = "Increased melee damage and knockback" },
                new() { Name = "Transcendence", Category = AbilityCategory.Ultimate, UltCost = 2310, Duration = 6f, Description = "Invulnerable mass healing" }
            }
        });
    }

    private static void AddHero(HeroDefinition hero)
    {
        _allHeroes.Add(hero);
        _heroByName[hero.Name.ToLowerInvariant()] = hero;
    }

    /// <summary>
    /// Register a GUID mapping for a hero (from game data)
    /// </summary>
    public static void RegisterGuid(string guid, string heroName)
    {
        if (_heroByName.TryGetValue(heroName.ToLowerInvariant(), out var hero))
        {
            _heroByGuid[guid.ToLowerInvariant()] = hero;
            hero.KnownGuids.Add(guid);
        }
    }

    /// <summary>
    /// Get hero by GUID
    /// </summary>
    public static HeroDefinition GetByGuid(string guid)
    {
        return _heroByGuid.TryGetValue(guid.ToLowerInvariant(), out var hero) ? hero : null;
    }

    /// <summary>
    /// Get hero by name
    /// </summary>
    public static HeroDefinition GetByName(string name)
    {
        return _heroByName.TryGetValue(name.ToLowerInvariant(), out var hero) ? hero : null;
    }

    /// <summary>
    /// Get all heroes
    /// </summary>
    public static IReadOnlyList<HeroDefinition> GetAllHeroes() => _allHeroes;

    /// <summary>
    /// Get heroes by role
    /// </summary>
    public static IEnumerable<HeroDefinition> GetByRole(HeroRole role) => _allHeroes.Where(h => h.Role == role);
}

public class HeroDefinition
{
    public string Name { get; set; }
    public HeroRole Role { get; set; }
    public List<AbilityDefinition> Abilities { get; set; } = new();
    public List<string> KnownGuids { get; set; } = new();

    /// <summary>
    /// Get an ability by name
    /// </summary>
    public AbilityDefinition? GetAbility(string name)
    {
        return Abilities.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get the primary weapon/ability
    /// </summary>
    public AbilityDefinition? GetPrimary()
    {
        return Abilities.FirstOrDefault(a => a.Category == AbilityCategory.Primary);
    }

    /// <summary>
    /// Get the ultimate ability
    /// </summary>
    public AbilityDefinition? GetUltimate()
    {
        return Abilities.FirstOrDefault(a => a.Category == AbilityCategory.Ultimate);
    }
}

public class AbilityDefinition
{
    public string Name { get; set; }
    public string Description { get; set; }
    public AbilityCategory Category { get; set; }
    public float Cooldown { get; set; }
    public int Charges { get; set; } = 1;
    public float Duration { get; set; }
    public float Resource { get; set; }
    public float ResourceRegen { get; set; }
    public int Ammo { get; set; }
    public float ReloadTime { get; set; }
    public float MaxCharge { get; set; }
    public float UltCost { get; set; }

    public bool HasCooldown => Cooldown > 0;
    public bool HasCharges => Charges > 1;
    public bool HasResource => Resource > 0;
    public bool IsUltimate => Category == AbilityCategory.Ultimate;
}

public enum HeroRole
{
    Tank,
    Damage,
    Support
}

public enum AbilityCategory
{
    Primary,
    Secondary,
    Ability,
    Ultimate,
    Passive
}
