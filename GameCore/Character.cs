using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GameCore
{
    public record struct Character
    {
        public Guid Id { get; init; }
        public string Name { get; init; }
        public PrimaryStats BaseStats { get; private set; }
        public Character(Guid id, string name, PrimaryStats baseStats)
        {
            Id = id;
            Name = name;
            BaseStats = baseStats;
        }
    }

    public readonly record struct AiMemorySet
    {
        public AiTargetMemory TargetMemory { get; init; }
        public AiMoveMemory MoveMemory { get; init; }
        public AiCombatMemory CombatMemory { get; init; }
        public AiMode AiMode { get; init; }
    }

    public readonly record struct AiCombatMemory
    {
        public Guid? CurrentTarget { get; init; }
        public int LastSpellId { get; init; }
    }
    public readonly record struct AiTargetMemory
    {
        Guid? Target { get; init; }

    }
    public readonly record struct AiMoveMemory
    {
        public Vector2 ReturnPosition { get; init; }
    }

    public enum AiMode
    {
        Idle,
        Patrol,
        Attack,
    }
}
