using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        
    }
    public readonly record struct ResourceChange
    {
        public Guid CharacterId { get; init; }
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
    #region Spell Lifecycle
    public readonly record struct SpellCastRequest
    {
        public Guid SourceId { get; init; }
        public Guid? PrimaryTargetId { get; init; }
        public int SpellId { get; init; }
        public int RandomSeed { get; init; }
    }
    public readonly record struct SpellEvent
    {
        public Character SourceId { get; init; }
        public Character? PrimaryTargetId { get; init; }
        public WeaponView? WeaponView { get; init; }
        public SpellDefinition Spell { get; init; }
        public int RandomSeed { get; init; }
    }
    public readonly record struct PendingSpellCast
    {
        public Guid OwnerId { get; init; }
        public int SpellId { get; init; }
        public SpellEvent SpellEvent { get; init; }
        public WeaponView? WeaponView { get; init; }
    }
    public readonly record struct SpellCastResult
    {
        public bool Success { get; init; }
        public SpellFailReason FailureReason { get; init; }
        public SpellEffectResult? InstantCastResult { get; init; } //specifically 
        }
        public readonly record struct SpellEffectResult
    {
        public bool Success { get; init; }
        public SpellFailReason FailureReason { get; init; }
        public IReadOnlyList<ResourceChange>? ResourceChanges { get; init; }
    }
    public enum SpellFailReason
    {
        None,
        OutOfRange,
        NoLineOfSight,
        InsufficientResources,
        InvalidTarget,
        OnCooldown
    }
    #endregion
}
