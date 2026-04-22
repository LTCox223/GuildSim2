using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCore
{
    #region Enums for Equipment
    public enum EquipmentSlot
    {
        Head,
        Chest,
        Legs,
        Feet,
        Hands,
        MainHand,
        Offhand
    }
    public enum EquipmentCategory
    {
        Armor,
        Weapon,
        OffHand
    }

    public enum ArmorType
    {
        Light, // Cloth. Psyweave in my example.
        Medium, // Leather. Tactical in my example.
        Heavy, // Mail. Link Mail in my exmaple.
        UltraHeavy, // Plate. Powered Frame in my example. 
        Shield
    }

    public enum WeaponType
    {
        BeamSaber, //melee weapon for Galactic Knight (psionic tank)
        PileDriver, //melee weapon for Star Crusader (tech tank)
        Sidearm, //ranged weapon for Officer (tech support) and Adept (psionic support)
        Railgun, //ranged weapon for Psionic Sniper (psionic dps) and Gunhound (tech dps)
    }
    public enum Rarity
    {
        Junk,
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
    #endregion

    #region Definitions and Instances
    public sealed record EquipmentDefinition
    {
        public int ID { get; init; }
        public string BaseName { get; init; }
        public EquipmentCategory Category { get; init; }
        public EquipmentSlot Slot { get; init; }

        public WeaponType? WeaponType { get; init; }
        public ArmorType? ArmorType { get; init; }

        public int? Defense { get; init; }
        public int? AttackMin { get; init; }
        public int? AttackMax { get; init; }
        public int ItemLevel { get; init; }
        public int? RequiredLevel { get; init; } //not all gear needs a required level.
        public int MaxDurability { get; init; }
        public Rarity Rarity { get; init; }
        public PrimaryStats StatBonuses { get; init; }
        public bool CanRollModifiers { get; init; }
    }

    public sealed record ItemModifierTemplate
    {
        public int Id { get; init; }
        public string NamePattern { get; init; } = "";
        public PrimaryStats StatBonuses { get; init; }
        public bool IsPrefix { get; init; }
        public bool IsSuffix { get; init; }

    }

    public readonly record struct EquipmentInstance
    {
        public Guid InstanceId { get; init; }
        public EquipmentDefinition Definition { get; init; }
        public int Durability { get; init; }
        public string DisplayName { get; init; }
        public PrimaryStats Stats { get; init; }
    }

    public readonly struct WeaponView //specialized struct for weapons to make access easier.
    {
        private readonly EquipmentInstance _weapon;
        public int AttackMin => _weapon.Definition.AttackMin ?? 0;
        public int AttackMax => _weapon.Definition.AttackMax ?? 0;
        private WeaponView(EquipmentInstance weapon)
        {
            _weapon = weapon;
        }
        public static bool TryCreate(EquipmentInstance instance, out WeaponView weaponView)
        {
            if (instance.Definition.Category == EquipmentCategory.Weapon)
            {
                weaponView = new WeaponView(instance);
                return true;
            }
            weaponView = default;
            return false;
        }
        public static WeaponView? Create(EquipmentInstance instance)
        {
            if (instance.Definition.Category == EquipmentCategory.Weapon)
            {
                return new WeaponView(instance);
            }
            return null;
        }
    }
    #endregion

    #region Databases
    public static class EquipmentDatabase
    {
        private static Dictionary<int, EquipmentDefinition> _definitions = new Dictionary<int, EquipmentDefinition>();
        public static int DefinitionCount => _definitions.Count;
        private static void AddDefinition(EquipmentDefinition definition)
        {
            if (!_definitions.TryAdd(definition.ID, definition))
            {
                throw new InvalidOperationException(
                    $"Duplicate equipment definition ID {definition.ID}.");
            }
        }
        public static EquipmentDefinition GetDefinition(int id)
        {
            if (_definitions.TryGetValue(id, out var definition))
            {
                return definition;
            }
            throw new KeyNotFoundException($"No equipment definition found for ID {id}");
        }
        public static void ImportDatabase(List<EquipmentDefinitionDto> definitions)
        {
            foreach (var dto in definitions)
            {
                var definition = new EquipmentDefinition
                {
                    ID = dto.ID,
                    BaseName = dto.BaseName,
                    Category = dto.Category,
                    Slot = dto.Slot,
                    WeaponType = dto.WeaponType,
                    ArmorType = dto.ArmorType,
                    Defense = dto.Defense,
                    AttackMin = dto.AttackMin,
                    AttackMax = dto.AttackMax,
                    ItemLevel = dto.ItemLevel,
                    MaxDurability = dto.MaxDurability,
                    Rarity = dto.Rarity,
                    CanRollModifiers = dto.CanRollModifiers,
                    StatBonuses = new PrimaryStats
                    {
                        Strength = dto.Strength,
                        Agility = dto.Agility,
                        Endurance = dto.Endurance,
                        Willpower = dto.Willpower
                    },
                    RequiredLevel = dto.RequiredLevel
                };
                AddDefinition(definition);
            }
        }
    }

    public static class ItemModifierDatabase
    {
        private static List<ItemModifierTemplate> _modifiers = new List<ItemModifierTemplate>();
        public static int ModifierCount => _modifiers.Count;
        private static void AddModifier(ItemModifierTemplate modifier)
        {
            _modifiers.Add(modifier);
        }

        public static void ImportDatabase(List<ItemModifierTemplateDto> modifiers)
        {
            foreach (var dto in modifiers)
            {
                var modifier = new ItemModifierTemplate
                {
                    Id = dto.Id,
                    NamePattern = dto.NamePattern,
                    StatBonuses = new PrimaryStats
                    {
                        Strength = dto.Strength,
                        Agility = dto.Agility,
                        Endurance = dto.Endurance,
                        Willpower = dto.Willpower
                    },
                    IsPrefix = dto.IsPrefix,
                    IsSuffix = dto.IsSuffix
                };
                AddModifier(modifier);
            }

        }
        public static ItemModifierTemplate GetModifier(int id) //impure. relies on the state of the _modifiers list.
        {
            return _modifiers[id];
        }
        public static string ApplyModifierName(string baseName, ItemModifierTemplate modifier) //pure. Returns the modified name based on the modifier's pattern and whether it's a prefix or suffix.
        {
            string modifiedName;
            if (modifier.IsPrefix)
            {
                modifiedName = modifier.NamePattern + " " + baseName;
            }
            else if (modifier.IsSuffix)
            {
                modifiedName = baseName + " " + modifier.NamePattern;
            }
            else
            {
                modifiedName = baseName; // No change if neither prefix nor suffix
            }
            return modifiedName;
        }
    }
    #endregion
    /// <summary>
    /// The EquipmentGenerator class is responsible for creating instances of equipment based on their definitions and optional modifiers. 
    /// It takes into account the item's level, rarity, and the stat bonuses provided by the modifier to generate a final set of stats for 
    /// the equipment instance. 
    /// The generator also constructs the display name of the item by applying the modifier's name pattern to the base name of the equipment definition. 
    /// This class serves as a central point for all logic related to generating equipment instances in the game.
    /// The Generator requires everything an Equipment Instance needs to be generated.
    /// EquipmentGenerator is not responsible for determining what equipment to generate.
    /// </summary>
    public static class EquipmentGenerator
    {

        public static EquipmentInstance GenerateInstance(Guid id, EquipmentDefinition definition, ItemModifierTemplate? modifier)
        {            
            if (definition.CanRollModifiers && modifier != null)
            {
                PrimaryStats scaledStats = ScaledBonuses(definition.Rarity, definition.ItemLevel, modifier.StatBonuses);
                string displayName = ItemModifierDatabase.ApplyModifierName(definition.BaseName, modifier);
                //instance.DisplayName = definition.BaseName;
                PrimaryStats primaryStats = scaledStats;
                EquipmentInstance generated = new EquipmentInstance
                {
                    InstanceId = id,
                    Definition = definition,
                    Durability = definition.MaxDurability,
                    DisplayName = displayName,
                    Stats = primaryStats + definition.StatBonuses
                };
                return generated;
            }
            EquipmentInstance instance = new EquipmentInstance
            {
                InstanceId = id,
                Definition = definition,
                Durability = definition.MaxDurability,
                DisplayName = definition.BaseName,
                Stats = definition.StatBonuses
            };
            return instance;
        }
        private static PrimaryStats ScaledBonuses(Rarity rarity, int itemLevel, PrimaryStats baseStats)
        {
            //lets assume itemLevel is between 1 and 100, every 10 levels increases stats by 1. Rarity is a multiplier to the stat additions. Uncommon is 1x, Rare is 1.5x, and Legendary is 2x.
            double rarityMultiplier = rarity switch
            {
                Rarity.Uncommon => 1.0,
                Rarity.Rare => 1.5,
                Rarity.Epic => 2.0,
                Rarity.Legendary => 3, //shouldn't be able to roll for modifiers, but just in case...
                _ => 0
            };
            int levelBonus = itemLevel / 10;
            PrimaryStats newStats = PrimaryStats.ModifyStats(baseStats, baseStats.GetNonZeroStats().ToList(), levelBonus) * rarityMultiplier;
            return newStats;
        }
    }

    #region Equipment DTOs
    /// <summary>
    /// Data Transfer Objects for importing equipment definitions and item modifier templates from external sources (e.g., JSON files, databases). These DTOs are designed to be simple and serializable, without any behavior or logic. They serve as a bridge between the raw data and the internal representations used by the game.
    /// </summary>
    public record EquipmentDefinitionDto
    {
        public int ID { get; init; }
        public string BaseName { get; init; } = string.Empty;
        public EquipmentCategory Category { get; init; }
        public EquipmentSlot Slot { get; init; }
        public WeaponType? WeaponType { get; init; }
        public ArmorType? ArmorType { get; init; }
        public int? Defense { get; init; }
        public int? AttackMin { get; init; }
        public int? AttackMax { get; init; }
        public int ItemLevel { get; init; }
        public int MaxDurability { get; init; }
        public Rarity Rarity { get; init; }
        public bool CanRollModifiers { get; init; }
        public int? RequiredLevel { get; init; }

        // Flat stat fields
        public int Strength { get; init; }
        public int Agility { get; init; }
        public int Endurance { get; init; }
        public int Intellect { get; init; }
        public int Willpower { get; init; }
    }

    public sealed record ItemModifierTemplateDto
    {
        public int Id { get; init; }
        public required string NamePattern { get; init; }
        public int Strength { get; init; }
        public int Agility { get; init; }
        public int Endurance { get; init; }
        public int Intellect { get; init; }
        public int Willpower { get; init; }
        public bool IsPrefix { get; init; }
        public bool IsSuffix { get; init; }
    }
    #endregion
}
