using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCore
{
    public struct Spell
    {
        public int Id;
        public string Name;
        public double MinimumDistance;
        public double MaximumDistance;
        public bool RequiresLineOfSight;
        public int? Value;
    }
    public readonly record struct SpellDefinition
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public TargetKind TargetKind { get; init; }
        public EffectKind EffectKind { get; init; }
        public ResourceType ResourceType { get; init; }
        public double MinimumDistance { get; init; }
        public double MaximumDistance { get; init; }
        public bool RequiresLineOfSight { get; init; }
        public IReadOnlyList<ResourceChange> Costs { get; init; }
        public IReadOnlyList<ResourceChange> Gains { get; init; }
        //public IReadOnlyList<SpellEffectDefinition> Effects { get; init; }
    }
    public readonly record struct ResourceState
    {
        public int Current { get; init; }
        public int Maximum { get; init; }
    }

    public readonly record struct SpellEffectDefinition
    {
        public EffectKind EffectKind { get; init; }
        public int BaseValue { get; init; }
        public PrimaryStats.StatType? ScalingStat { get; init; }
        public double ScalingFactor { get; init; }
        public ResourceType? ResourceType { get; init; }
    }
    public readonly record struct ResourceChange
    {
        public ResourceType ResourceType { get; init; }
        public int Amount { get; init; }
    }

    public enum ResourceType
    {
        PsiPoints,
        Heat,
        Grit,
        Focus,
        ComboPoints,
        Health
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
        Damage,
        Heal,
        AddResource,
        SpendResource,
        GeneratePoints,
        SpendPoints,
        ApplyStatus,
        ModifyCooldown,
        StanceChange
    }

    public static class DamageSpells
    {
        public static Spell AutoAttack;
        static DamageSpells()
        {
            AutoAttack = new Spell() { Id = 0, Name = "Auto Attack", MinimumDistance = 0, MaximumDistance = 1.5, RequiresLineOfSight = false };
        }
        public static int DealWeaponDamage(EquipmentInstance? weapon, int statModifier, int rndNumber)
        {
            if (weapon.HasValue && weapon.Value.Definition != null)
            {
                int? minDamage = weapon.Value.Definition.AttackMin;
                int? maxDamage = weapon.Value.Definition.AttackMax;
                int? baseDamage = minDamage + (rndNumber % (maxDamage - minDamage + 1));
                int damage = baseDamage!.Value + statModifier / 2; // Placeholder for actual damage calculation logic
                return damage;
            }
            
            return statModifier / 4;
        }
    }
    public readonly record struct SpellCastRequest
    {
        public Guid SourceId { get; init; }
        public Guid? PrimaryTargetId { get; init; }
        public int SpellId { get; init; }
        public int RandomSeed { get; init; }
    }

    public readonly record struct SpellCastResult
    {
        public bool Success { get; init; }
        public string? FailureReason { get; init; }
        public IReadOnlyList<StateChange> Changes { get; init; }
    }
}
