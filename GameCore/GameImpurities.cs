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

        public static void ApplyResourceChange(SpellCastResult result)
        {
            if (result.ResourceChanges == null || result.ResourceChanges.Count == 0)
            {
                return; // No resource changes to apply
            }
            List<ResourceChange> changes = result.ResourceChanges.ToList();
            for (int i = 0; i < changes.Count; i++)
            {
                ResourceChange change = changes[i];
                resourceChanges.Enqueue(change);
            }
        }

        private static SpellCastEvent CreateSpellCastEvent(Guid sourceId, Guid? primaryTargetId, SpellDefinition spell, int randomSeed)
        {
            SpellCastEvent newEvent = new SpellCastEvent()
            {
                SourceId = Characters[sourceId],
                PrimaryTargetId = primaryTargetId.HasValue ? Characters[primaryTargetId.Value] : null,
                Spell = spell,
                RandomSeed = randomSeed
            };

            return newEvent;
        }

        public static SpellCastResult ResolveSpell(SpellCastEvent request, WeaponView? weapon)
        {
            if (!request.PrimaryTargetId.HasValue)
            {
                return new SpellCastResult() { Success = false, FailureReason = SpellFailReason.InvalidTarget };
            }
            SortieState target = SortieStates[request.PrimaryTargetId.Value.Id];
            if (!target.Resources.TryGetValue(ResourceType.Health, out ResourceState targetHealth))
            {
                return new SpellCastResult() { Success = false, FailureReason = SpellFailReason.InvalidTarget }; //its... cant hit it.
            }
            List<ResourceChange> changes = new List<ResourceChange>();
            foreach (SpellEffectDefinition effect in request.Spell.Effects)
            {
                switch (effect.EffectKind)
                {
                    case EffectKind.WeaponDamage:
                        int damage = SpellMath.CalculateWeaponDamage(weapon, Characters[request.SourceId.Id].BaseStats.Strength, request.RandomSeed)*-1;
                        changes.Add(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value.Id, ResourceType = ResourceType.Health, Amount = damage });
                        break;
                    case EffectKind.TechDamage:
                        Character character = Characters[request.SourceId.Id];
                        int techDamage = SpellMath.CalculateScaledValue(effect, character.BaseStats)*-1;
                        changes.Add(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value.Id, ResourceType = ResourceType.Health, Amount = techDamage });
                        break;
                    case EffectKind.AddResource:
                        Character sourceCharacter = Characters[request.SourceId.Id];
                        int resourceAmount = SpellMath.CalculateScaledValue(effect, sourceCharacter.BaseStats);
                        
                        if (effect.TargetKind != TargetKind.Self)
                        {
                            changes.Add(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value.Id, ResourceType = effect.ResourceType!.Value, Amount = resourceAmount });
                        }
                        else
                            changes.Add(new ResourceChange() { CharacterId = request.SourceId.Id, ResourceType = effect.ResourceType!.Value, Amount = resourceAmount });
                        break;
                }
            }
            return new SpellCastResult() { Success = true, ResourceChanges = changes};
        }
        public static void EndCycle()
        {
            UpdateResources();
        }
        private static void UpdateResources()
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
