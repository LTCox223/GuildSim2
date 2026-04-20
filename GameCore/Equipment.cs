using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCore
{
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
        Cloth,
        Leather,
        Mail,
        Plate,
        Shield
    }

    public enum WeaponType
    {
        Sword,
        Axe,
        Mace,
        Staff,
        Bow,
        Dagger
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

    public struct EquipmentInstance
    {
        public Guid InstanceId { get; init; }
        public EquipmentDefinition Definition { get; init; }
        public int Durability { get; init; }
        public string DisplayName { get; init; }
        public PrimaryStats Stats { get; init; }
    }


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
                        Intellect = dto.Intellect,
                        Spirit = dto.Spirit
                    },
                    RequiredLevel = dto.RequiredLevel
                };
                AddDefinition(definition);
            }
        }
    }

    public static class ModifierDatabase
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
                        Intellect = dto.Intellect,
                        Spirit = dto.Spirit
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
    public static class EquipmentGenerator
    {

        public static EquipmentInstance GenerateInstance(Guid id, EquipmentDefinition definition, ItemModifierTemplate? modifier)
        {            
            if (definition.CanRollModifiers && modifier != null)
            {
                PrimaryStats scaledStats = ScaledBonuses(definition.Rarity, definition.ItemLevel, modifier.StatBonuses);
                string displayName = ModifierDatabase.ApplyModifierName(definition.BaseName, modifier);
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
        public int Spirit { get; init; }
    }

    public sealed record ItemModifierTemplateDto
    {
        public int Id { get; init; }
        public required string NamePattern { get; init; }
        public int Strength { get; init; }
        public int Agility { get; init; }
        public int Endurance { get; init; }
        public int Intellect { get; init; }
        public int Spirit { get; init; }
        public bool IsPrefix { get; init; }
        public bool IsSuffix { get; init; }
    }
}
