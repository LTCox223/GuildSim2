using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCore
{
    public struct PrimaryStats
    {
        public enum StatType
        {
            Endurance,
            Strength,
            Agility,
            Willpower
        }

        public int Endurance; //used to calculate HP
        public int Strength;  //used as part of attack power
        public int Agility;   //used for things just not sure what yet.
        public int Willpower; //used for magic regeneration

        public PrimaryStats(int endurance, int strength, int agility, int intellect, int willpower)
        {
            Endurance = endurance;
            Strength = strength;
            Agility = agility;
            Willpower = willpower;
        }
        public static PrimaryStats operator +(PrimaryStats a, PrimaryStats b)
        {
            return new PrimaryStats
            {
                Endurance = a.Endurance + b.Endurance,
                Strength = a.Strength + b.Strength,
                Agility = a.Agility + b.Agility,
                Willpower = a.Willpower + b.Willpower
            };
        }
        public static PrimaryStats operator -(PrimaryStats a, PrimaryStats b)
        {
            return new PrimaryStats
            {
                Endurance = a.Endurance - b.Endurance,
                Strength = a.Strength - b.Strength,
                Agility = a.Agility - b.Agility,
                Willpower = a.Willpower - b.Willpower
            };
        }

        public static PrimaryStats operator +(PrimaryStats a, int bonus)
        {
            return new PrimaryStats
            {
                Endurance = a.Endurance + bonus,
                Strength = a.Strength + bonus,
                Agility = a.Agility + bonus,
                Willpower = a.Willpower + bonus
            };
        }
        public static PrimaryStats operator -(PrimaryStats a, int penalty)
        {
            return new PrimaryStats
            {
                Endurance = a.Endurance - penalty,
                Strength = a.Strength - penalty,
                Agility = a.Agility - penalty,
                Willpower = a.Willpower - penalty
            };
        }
        public static PrimaryStats operator *(PrimaryStats a, double multiplier)
        {
            return new PrimaryStats
            {
                Endurance = (int)Math.Round(a.Endurance * multiplier),
                Strength = (int)Math.Round(a.Strength * multiplier),
                Agility = (int)Math.Round(a.Agility * multiplier),
                Willpower = (int)Math.Round(a.Willpower * multiplier)
            };
        }
        public static PrimaryStats ModifyStat(PrimaryStats primaryStats, StatType statToModify, int value)
        {
            switch (statToModify)
            {
                case StatType.Endurance:
                    primaryStats.Endurance += value;
                    break;
                case StatType.Strength:
                    primaryStats.Strength += value;
                    break;
                case StatType.Agility:
                    primaryStats.Agility += value;
                    break;
                case StatType.Willpower:
                    primaryStats.Willpower += value;
                    break;
            }

            return primaryStats;
        }
        public static PrimaryStats ModifyStatScalar(PrimaryStats primaryStats, StatType statToModify, double value)
        {
            //modify a single stat with a scalar value. For example, multiply Agility by 1.2.
            switch (statToModify)
            {
                case StatType.Endurance:
                    primaryStats.Endurance = (int)Math.Round(primaryStats.Endurance * value);
                    break;
                case StatType.Strength:
                    primaryStats.Strength = (int)Math.Round(primaryStats.Strength * value);
                    break;
                case StatType.Agility:
                    primaryStats.Agility = (int)Math.Round(primaryStats.Agility * value);
                    break;
                    break;
                case StatType.Willpower:
                    primaryStats.Willpower = (int)Math.Round(primaryStats.Willpower * value);
                    break;
            }

            return primaryStats;
        }
        public static PrimaryStats ModifyStats(PrimaryStats primaryStats, IReadOnlyList<StatType> statsToModify, int value)
        {
            for (int i = 0; i < statsToModify.Count; i++)
            {
                switch (statsToModify[i])
                {
                    case StatType.Endurance:
                        primaryStats.Endurance += value;
                        break;
                    case StatType.Strength:
                        primaryStats.Strength += value;
                        break;
                    case StatType.Agility:
                        primaryStats.Agility += value;
                        break;
                    case StatType.Willpower:
                        primaryStats.Willpower += value;
                        break;
                }
            }
            return primaryStats;
        }
        public static PrimaryStats ModifyStatsScalar(PrimaryStats primaryStats, IReadOnlyList<StatType> statsToModify, double value)
        {
            for (int i = 0; i < statsToModify.Count; i++)
            {
                switch (statsToModify[i])
                {
                    case StatType.Endurance:
                        primaryStats.Endurance = (int)Math.Round(value * primaryStats.Endurance);
                        break;
                    case StatType.Strength:
                        primaryStats.Strength = (int)Math.Round(value * primaryStats.Strength);
                        break;
                    case StatType.Agility:
                        primaryStats.Agility = (int)Math.Round(value * primaryStats.Agility);
                        break;
                    case StatType.Willpower:
                        primaryStats.Willpower = (int)Math.Round(value * primaryStats.Willpower);
                        break;
                }
            }
            return primaryStats;
        }

        public static PrimaryStats Sum(IEnumerable<PrimaryStats> statsList)
        {
            PrimaryStats total = new PrimaryStats();
            foreach (var stats in statsList)
            {
                total += stats;
            }
            return total;
        }

        public IEnumerable<StatType> GetNonZeroStats()
        {
            if (Endurance > 0) yield return StatType.Endurance;
            if (Strength > 0) yield return StatType.Strength;
            if (Agility > 0) yield return StatType.Agility;
            if (Willpower > 0) yield return StatType.Willpower;
        }
        public int GetStat(StatType statType)
        {
            return statType switch
            {
                StatType.Endurance => Endurance,
                StatType.Strength => Strength,
                StatType.Agility => Agility,
                StatType.Willpower => Willpower,
                _ => throw new ArgumentException("Invalid stat type")
            };
        }

        public static PrimaryStats Zero => new PrimaryStats //zeroed out stats for use as a base when modifying stats
        {
            Endurance = 0,
            Strength = 0,
            Agility = 0,
            Willpower = 0
        };
    }
}
