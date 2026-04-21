using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GameCore.PrimaryStats;

namespace GameCore
{
    public static class GameImpurities
    {
        public static Dictionary<Guid,Character> Characters { get; private set; } = new Dictionary<Guid, Character>();
        public static Dictionary<Guid, int> CurrentHealth { get; private set; } = new Dictionary<Guid, int>();
        public static Random Random { get; private set; } = new Random();
        public static int GetRandomInt(int min, int max)
        {
            return Random.Next(min, max);
        }
        private static int SetNewRandomSeed()
        {
            int newSeed = Random.Next();
            Random = new Random(newSeed);
            return newSeed;
        }

        public static EquipmentInstance GenerateRandomEquipment()
        {
            EquipmentDefinition randomDefinition = GetTrueRandomEquipmentDefinition();
            ItemModifierTemplate randomModifier = GetTrueRandomModifier();
            return EquipmentGenerator.GenerateInstance(Guid.NewGuid(),randomDefinition, randomModifier);
        }
        private static ItemModifierTemplate GetTrueRandomModifier()
        {
            int randomTemplate = GetRandomInt(1, ModifierDatabase.ModifierCount + 1); // Placeholder for actual item modifier templates
            return ModifierDatabase.GetModifier(randomTemplate);
        }
        private static EquipmentDefinition GetTrueRandomEquipmentDefinition()
        {
            int randomDefinition = GetRandomInt(1, EquipmentDatabase.DefinitionCount + 1); // Placeholder for actual equipment definitions
            return EquipmentDatabase.GetDefinition(randomDefinition);
        }

        public static SpellCastResult ResolveSpell(SpellCastRequest request)
        {

        }
    }

    public readonly record struct SortieState
    {
        public Guid CharacterId { get; init; }
        public IReadOnlyDictionary<ResourceType, ResourceState> Resources { get; init; }
        public IReadOnlyDictionary<int, int> CooldownsBySpellId { get; init; }
        //public IReadOnlyDictionary<StatusType, int> StatusDurations { get; init; }
    }
    public static class SortieStates
    {
        public static Dictionary<Guid, SortieState> ByCharacterId { get; private set; } = new Dictionary<Guid, SortieState>();
    }
}
