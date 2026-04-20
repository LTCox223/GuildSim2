using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCore
{
    public struct Character
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public PrimaryStats BaseStats { get; private set; }
        public List<EquipmentInstance> EquippedItems { get; private set; }
        public Character(Guid id, string name, PrimaryStats baseStats)
        {
            Id = id;
            Name = name;
            BaseStats = baseStats;
            EquippedItems = new List<EquipmentInstance>();
        }

    }
}
