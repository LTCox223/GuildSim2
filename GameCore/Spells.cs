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

    public static class DamageSpells
    {
        public static Spell Fireball;
        public static Spell Frostbolt;
        public static Spell ArcaneMissiles;
        public static Spell ShadowBolt;
        public static Spell HolySmite;
        public static Spell AutoAttack;
        static DamageSpells()
        {
            AutoAttack = new Spell() { Id = 0, Name = "Auto Attack", MinimumDistance = 0, MaximumDistance = 1.5, RequiresLineOfSight = false};
            Fireball = new Spell();
            Frostbolt = new Spell();
            ArcaneMissiles = new Spell();
            ShadowBolt = new Spell();
            HolySmite = new Spell() { Id = 5, Name = "Holy Smite", MinimumDistance = 0, MaximumDistance = 30, RequiresLineOfSight = true, Value = 20 };
        }
        public static int AutoAttackCalc(EquipmentInstance? weapon, PrimaryStats stats, int rndNumber)
        {
            if (weapon.HasValue && weapon.Value.Definition != null)
            {
                int? minDamage = weapon.Value.Definition.AttackMin;
                int? maxDamage = weapon.Value.Definition.AttackMax;
                int? baseDamage = minDamage + (rndNumber % (maxDamage - minDamage + 1));
                int damage = baseDamage!.Value + stats.Strength / 2; // Placeholder for actual damage calculation logic
                return damage;
            }

            return stats.Strength / 4;
        }

        public static int HolySmiteCalc(PrimaryStats stats, int rndNumber, Spell spell, int level)
        {
            int scaledDamage = spell.Value!.Value * level; // Scale damage based on spell level
            int minDamage = (int)(scaledDamage * .9); // Minimum damage is 90% of scaled damage
            int maxDamage = (int)(scaledDamage * 1.3); // Maximum damage is 110% of scaled damage
            int baseDamage = minDamage + (rndNumber % (maxDamage - minDamage + 1));
            int damage =  baseDamage + stats.Intellect / 2; 
            return damage;
        }
    }
    public struct DamageResolve
    {
        public int? Damage;
        public int? CriticalHitMult;
        public Guid? Source;
        public Guid? Target;
    }
}
