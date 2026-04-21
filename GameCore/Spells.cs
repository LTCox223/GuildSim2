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
        public static Spell AutoAttack;
        static DamageSpells()
        {
            AutoAttack = new Spell() { Id = 0, Name = "Auto Attack", MinimumDistance = 0, MaximumDistance = 1.5, RequiresLineOfSight = false };
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
    }
    public struct DamageResolve
    {
        public int? Damage;
        public int? CriticalHitMult;
        public Guid? Source;
        public Guid? Target;
    }
}
