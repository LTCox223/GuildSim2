using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCore
{
    public record struct Character
    {
        public Guid Id { get; init; }
        public string Name { get; init; }
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
