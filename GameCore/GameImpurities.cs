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
        public static Dictionary<Guid, Character> Characters { get; private set; } = new Dictionary<Guid, Character>(); //characters in the sortie, indexed by their unique ID. This is all characters.
        public static Dictionary<Guid, WeaponView> Weapons { get; private set; } = new Dictionary<Guid, WeaponView>(); //weapons from characters.
        public static Dictionary<Guid, SortieState> SortieStates { get; private set; } = new Dictionary<Guid, SortieState>();
        private static Queue<ResourceChange> resourceChanges { get; set; } = new Queue<ResourceChange>();
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
            return EquipmentGenerator.GenerateInstance(Guid.NewGuid(), randomDefinition, randomModifier);
        }
        private static ItemModifierTemplate GetTrueRandomModifier()
        {
            int randomTemplate = GetRandomInt(1, ItemModifierDatabase.ModifierCount + 1); // Placeholder for actual item modifier templates
            return ItemModifierDatabase.GetModifier(randomTemplate);
        }
        private static EquipmentDefinition GetTrueRandomEquipmentDefinition()
        {
            int randomDefinition = GetRandomInt(1, EquipmentDatabase.DefinitionCount + 1); // Placeholder for actual equipment definitions
            return EquipmentDatabase.GetDefinition(randomDefinition);
        }

        public static SpellCastResult ResolveSpell(SpellCastRequest request)
        {
            if (!request.PrimaryTargetId.HasValue)
            {
                return new SpellCastResult() { Success = false, FailureReason = SpellFailReason.InvalidTarget };
            }
            Guid targetId = request.PrimaryTargetId.Value;
            SortieState caster = SortieStates[request.SourceId];
            SortieState target = SortieStates[targetId];

            if (!target.Resources.TryGetValue(ResourceType.Health, out ResourceState targetHealth))
            {
                return new SpellCastResult() { Success = false, FailureReason = SpellFailReason.InvalidTarget }; //its... cant hit it.
            }




            foreach (SpellEffectDefinition effect in request.Spell.Effects)
            {
                switch (effect.EffectKind)
                {
                    case EffectKind.WeaponDamage:
                        WeaponView? weapon;
                        if (!Weapons.TryGetValue(request.SourceId, out WeaponView weaponCheck))
                        {
                            weapon = null; //unarmed attack
                        }
                        else
                        {
                            weapon = weaponCheck;
                        }
                        int damage = SpellMath.CalculateWeaponDamage(weapon, Characters[request.SourceId].BaseStats.Strength, request.RandomSeed)*-1;
                        resourceChanges.Enqueue(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value, ResourceType = ResourceType.Health, Amount = damage });
                        break;
                    case EffectKind.TechDamage:
                        Character character = Characters[request.SourceId];
                        int techDamage = SpellMath.CalculateScaledValue(effect, character.BaseStats)*-1;
                        resourceChanges.Enqueue(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value, ResourceType = ResourceType.Health, Amount = techDamage });
                        break;
                    case EffectKind.AddResource:
                        Character sourceCharacter = Characters[request.SourceId];
                        int resourceAmount = SpellMath.CalculateScaledValue(effect, sourceCharacter.BaseStats);
                        
                        if (effect.TargetKind != TargetKind.Self)
                        {
                            resourceChanges.Enqueue(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value, ResourceType = effect.ResourceType!.Value, Amount = resourceAmount });
                        }
                        else
                            resourceChanges.Enqueue(new ResourceChange() { CharacterId = request.SourceId, ResourceType = effect.ResourceType!.Value, Amount = resourceAmount });
                        break;
                }
            }
            return new SpellCastResult() { Success = true };
        }
        public static void UpdateResources()
        {

            if (resourceChanges.Count == 0)
            {
                return;
            }

            Dictionary<Guid, SortieState> updatedResources = new Dictionary<Guid, SortieState>();

            while (resourceChanges.Count > 0)
            {
                ResourceChange state = resourceChanges.Dequeue();

                if (!updatedResources.TryGetValue(state.CharacterId, out SortieState resourceState))
                {
                    resourceState = SortieStates[state.CharacterId];
                }

                Dictionary<ResourceType, ResourceState> newResources =
                    resourceState.Resources.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                ResourceState oldResourceState = newResources[state.ResourceType];

                ResourceState newResourceState = new ResourceState
                {
                    Current = oldResourceState.Current + state.Amount,
                    Maximum = oldResourceState.Maximum
                };

                newResources[state.ResourceType] = newResourceState;

                SortieState newSortieState = resourceState with
                {
                    Resources = newResources
                };

                updatedResources[state.CharacterId] = newSortieState;
            }

            foreach (var kvp in updatedResources)
            {
                SortieStates[kvp.Key] = kvp.Value;
            }
        }
    }

    public readonly record struct SortieState
    {
        public IReadOnlyDictionary<ResourceType, ResourceState> Resources { get; init; }
        public IReadOnlyDictionary<int, int> CooldownsBySpellId { get; init; }
        //public IReadOnlyDictionary<StatusType, int> StatusDurations { get; init; }
    }
}
