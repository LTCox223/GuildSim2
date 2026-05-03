using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Arch.Core;

namespace GameCore
{
    #region Spell Data
    public readonly record struct SpellDefinition
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public double MinimumDistance { get; init; }
        public double MaximumDistance { get; init; }
        public bool RequiresLineOfSight { get; init; }
        public CastType CastType { get; init; }
        public SpellKind SpellKind { get; init; } //will make some things not possible.
        public bool AdhereToGlobalCooldown { get; init; }
        public TimeSpan? Duration { get; init; }
        public IReadOnlyList<SpellEffectDefinition> Effects { get; init; }
        public TimeSpan? Cooldown { get; init; }
    }
    public readonly record struct ResourceState
    {
        public ResourceType ResourceType { get; init; }
        public int Current { get; init; }
        public int Maximum { get; init; }
    }

    public readonly record struct SpellEffectDefinition
    {
        public EffectKind EffectKind { get; init; }
        public TargetKind TargetKind { get; init; }
        public int BaseValue { get; init; }
        public bool AllowScaling { get; init; }
        public PrimaryStats.StatType? ScalingStat { get; init; }
        public double ScalingFactor { get; init; }
        public ResourceType? ResourceType { get; init; }
        public bool RequiredForValidation { get; init; }
    }
    public readonly record struct ResourceChange
    {
        public Character CharacterId { get; init; }
        public ResourceType ResourceType { get; init; }
        public int Amount { get; init; }
    }
    #endregion

    #region Spell Enums
    public enum ResourceType
    {
        PsiPoints,
        Heat,
        Grit,
        Focus,
        ComboPoints,
        Health
    }
    public enum CastType
    {
        Instant,
        Channeled,
        Charged
    }
    public enum SpellKind
    {
        Single, //requires a single target clicked.
        AoE, //applies to an area.
        Self, //applies to self
        Focus //requires a focused target.
    }
    public enum TargetKind
    {
        Self,
        SingleEnemy,
        SingleAlly,
        AreaEnemy,
        AreaAlly
    }
    public enum EffectKind
    {
        WeaponDamage,
        TechDamage,
        PsiDamage,
        Heal,
        AddResource,
        SpendResource,
        GeneratePoints,
        SpendPoints,
        ApplyStatus,
        ModifyCooldown,
        StanceChange
    }
    #endregion

    public static class SpellDatabase
    {
        private static readonly Dictionary<int, SpellDefinition> _spells = new();

        public static void Add(SpellDefinition spell)
        {
            _spells.Add(spell.Id, spell);
        }

        public static SpellDefinition Get(int id)
        {
            return _spells[id];
        }
        public static SpellDefinition GCD = new SpellDefinition
        {
            Id = 0,
            Name = "Global Cooldown",
            MinimumDistance = 0,
            MaximumDistance = 0,
            RequiresLineOfSight = false,
            Effects = Array.Empty<SpellEffectDefinition>(),
            Cooldown = TimeSpan.FromSeconds(1.5)
        };
    }

    public static class SpellMath
    {
        static SpellMath()
        {
        }
        public static int CalculateScaledValue(
        SpellEffectDefinition effect,
        PrimaryStats stats
        )
        {
            if (effect.AllowScaling && effect.ScalingStat.HasValue)
            {
                int statValue = effect.ScalingStat.Value switch
                {
                    PrimaryStats.StatType.Endurance => stats.Endurance,
                    PrimaryStats.StatType.Strength => stats.Strength,
                    PrimaryStats.StatType.Agility => stats.Agility,
                    PrimaryStats.StatType.Willpower => stats.Willpower,
                    _ => 0
                };
                return (int)(effect.BaseValue + statValue * effect.ScalingFactor);
            }
            else if (effect.AllowScaling)
            {
                return (int)(effect.BaseValue * effect.ScalingFactor);
            }
            else
            {
                return effect.BaseValue;
            }
        }


        public static List<ResourceChange> ResolveEffects(SpellEvent request)
        {
            List<ResourceChange> changes = new List<ResourceChange>();
            foreach (SpellEffectDefinition effect in request.Spell.Effects)
            {
                switch (effect.EffectKind)
                {
                    case EffectKind.WeaponDamage:
                        int damage = SpellMath.CalculateWeaponDamage(request.WeaponView, request.SourceId.BaseStats.Strength, request.RandomSeed) * -1;
                        changes.Add(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value, ResourceType = ResourceType.Health, Amount = damage });
                        break;
                    case EffectKind.TechDamage:
                        Character character = request.SourceId;
                        int techDamage = SpellMath.CalculateScaledValue(effect, character.BaseStats) * -1;
                        changes.Add(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value, ResourceType = ResourceType.Health, Amount = techDamage });
                        break;
                    case EffectKind.AddResource:
                        Character sourceCharacter = request.SourceId;
                        int resourceAmount = SpellMath.CalculateScaledValue(effect, sourceCharacter.BaseStats);

                        if (effect.TargetKind != TargetKind.Self)
                        {
                            changes.Add(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value, ResourceType = effect.ResourceType!.Value, Amount = resourceAmount });
                        }
                        else
                            changes.Add(new ResourceChange() { CharacterId = request.SourceId, ResourceType = effect.ResourceType!.Value, Amount = resourceAmount });
                        break;
                }
            }
            return changes;
        }
        public static int CalculateWeaponDamage(WeaponView? weapon, int strengthModifier, int rndNumber)
        {
            if (weapon.HasValue)
            {
                int? minDamage = weapon.Value.AttackMin;
                int? maxDamage = weapon.Value.AttackMax;
                int? baseDamage = minDamage + (rndNumber % (maxDamage - minDamage + 1));
                int damage = baseDamage!.Value + strengthModifier / 2; // Placeholder for actual damage calculation logic
                return damage;
            }

            return strengthModifier / 4;
        }
    }

    public static class Spells
    {
        public static bool SpellRequestCheck(SpellCastIntent intent, SpellLocks locks, GCDLock? gcd, SpellCastingFlag? castFlag)
        {
            //determine if this spell can be casted at all. No validating of the effects just yet.
            if (locks.SpellsOnCooldown.TryGetValue(intent.SpellId, out _) || castFlag.HasValue || gcd.HasValue)
            {
                return false; //cant cast so don't bother.
            }
            
            if ((intent.Spell.SpellKind == SpellKind.Single | intent.Spell.SpellKind == SpellKind.Focus) && !intent.PrimaryTargetId.HasValue) //both of these kinds need target
            {
                return false;
            }
            return true;
        }

        public static bool TargetSpellEffectValidation(SpellEffectDefinition definition, PrimaryStats stats, Dictionary<ResourceType,ResourceState> sourceResource, Dictionary<ResourceType, ResourceState> targetResource)
        {
            targetResource.TryGetValue(ResourceType.Health, out ResourceState health);
            if (!definition.RequiredForValidation)
                return true;
            switch (definition.EffectKind)
            {
                case EffectKind.Heal:
                    if (health.Current > 0)
                    {
                        return true;
                    }
                    break;
                case EffectKind.WeaponDamage:
                    if (health.Current > 0)
                    {
                        return true;
                    }
                    break;
                case EffectKind.TechDamage:
                    if (health.Current > 0)
                    {
                        return true;
                    }
                    break;
                case EffectKind.PsiDamage:
                    if (health.Current > 0)
                    {
                        return true;
                    }
                    break;
            }
            switch (definition.EffectKind)
            {
                case EffectKind.AddResource:
                    if(!sourceResource.ContainsKey(definition.ResourceType!.Value)) { return false; }
                    int resourceAmount = sourceResource[definition.ResourceType!.Value].Current + SpellMath.CalculateScaledValue(definition, stats);
                    if (resourceAmount > sourceResource[definition.ResourceType!.Value].Maximum)
                        return false;
                    return true;
            }
            return false;
        }

        public static SpellCastIntent CreateIntent(SpellDefinition request, Entity source, Entity? target)
        {
            return new SpellCastIntent()
            {
                OwnerId = source,
                PrimaryTargetId = target,
                Spell = request,
                SpellId = request.Id
            };
        }
    }
    

    #region Spell Lifecycle
    //intent -> event -> spell object entity -> spell object entity event -> result -> end
    
    public readonly record struct SpellEvent
    {
        public Character SourceId { get; init; }
        public Character? PrimaryTargetId { get; init; }
        public WeaponView? WeaponView { get; init; }
        public SpellDefinition Spell { get; init; }
        public int RandomSeed { get; init; }
        public TimeSpan CompleteAt { get; init; }
    } //raw spell data
    
    public readonly record struct SpellEffectResult //raw results of a spell
    {
        public SpellCastIntent SpellCastIntent { get; init; }
        public IEnumerable<ResourceChange>? ResourceChanges { get; init; }
    }


    #endregion
    
    #region SpellEntities

    #endregion
    #region SpellComponents
    public readonly record struct SpellCastIntent //spell cast intent. Needs the actual entities.
    {
        public Entity OwnerId { get; init; }
        public Entity? PrimaryTargetId { get; init; }
        public int SpellId { get; init; }
        public SpellDefinition Spell { get; init; }
    }
    public readonly record struct SpellCastComponent
    {
        public TimeSpan ExpireAt { get; init; }
    }
    public readonly record struct SpellChannelComponent
    {
        public TimeSpan NextTickAt { get; init; }
        public TimeSpan ExpireAt { get; init; }
    }
    public readonly record struct SpellInstantFlag();
    public readonly record struct SpellCancelFlag();
    public readonly record struct SpellCastingFlag();
    public readonly record struct SpellEventComponent
    {
        public Entity Source { get; init; }
        public Entity? Target { get; init; }
        public SpellEvent SpellEvent { get; init; }
    }
    public readonly record struct SpellLocks
    {
        public Dictionary<int, TimeSpan> SpellsOnCooldown { get; init; }
        public SpellLocks()
        {
            SpellsOnCooldown = new Dictionary<int, TimeSpan>();
        }
        public SpellLocks(Dictionary<int, TimeSpan> spellsOnCooldown, bool casting)
        {
            SpellsOnCooldown = spellsOnCooldown;
        }
    }

    public readonly record struct GCDLock
    {
        public TimeSpan ExpireAt { get; init; }
    }
    #endregion
}
