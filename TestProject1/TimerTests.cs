using System.Collections.Immutable;
using System.Diagnostics;
using GameCore;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Payloads;

namespace TestProject1;

public class TimerTests
{
    private static SpellDefinition rapidCycle { get; } = new SpellDefinition
    {
        Id = 100,
        Name = "Rapid Cycle",
        MinimumDistance = 0,
        MaximumDistance = 30,
        RequiresLineOfSight = true,
        AdhereToGlobalCooldown = true,
        CastType = CastType.Instant,
        Duration = null, //just to test it. 
        Effects = new[]

    {
                new SpellEffectDefinition
                {
                    EffectKind = EffectKind.WeaponDamage,
                    TargetKind = TargetKind.SingleEnemy,
                    BaseValue = 0,
                    ScalingStat = PrimaryStats.StatType.Strength,
                    ScalingFactor = 0.25
                },
        new SpellEffectDefinition
        {
            EffectKind = EffectKind.TechDamage,
            TargetKind = TargetKind.SingleEnemy,
            BaseValue = 8,
            AllowScaling = true,
            ScalingStat = PrimaryStats.StatType.Agility,
            ScalingFactor = 0.6
        },
        new SpellEffectDefinition
        {
            TargetKind = TargetKind.Self,
            EffectKind = EffectKind.AddResource,
            ResourceType = ResourceType.ComboPoints,
            BaseValue = 1
        },
        new SpellEffectDefinition
        {
            TargetKind = TargetKind.Self,
            EffectKind = EffectKind.AddResource,
            ResourceType = ResourceType.Heat,
            BaseValue = 20
        }
    },
        Cooldown = TimeSpan.FromSeconds(5),
    }; //instant cast with cooldown. Does adhere to global CD.
    private static SpellDefinition chargeCycle = new SpellDefinition()
    {
        Id = 101,
        Name = "Charge Cycle",
        MinimumDistance = 0,
        MaximumDistance = 30,
        RequiresLineOfSight = true,
        AdhereToGlobalCooldown = true,
        CastType = CastType.Charged,
        Duration = TimeSpan.FromSeconds(3), //just to test it. 
        Effects = new[]

        {
                new SpellEffectDefinition
                {
                    EffectKind = EffectKind.WeaponDamage,
                    TargetKind = TargetKind.SingleEnemy,
                    BaseValue = 0,
                    ScalingStat = PrimaryStats.StatType.Strength,
                    ScalingFactor = 0.25
                },
        new SpellEffectDefinition
        {
            EffectKind = EffectKind.TechDamage,
            TargetKind = TargetKind.SingleEnemy,
            BaseValue = 8,
            AllowScaling = true,
            ScalingStat = PrimaryStats.StatType.Agility,
            ScalingFactor = 0.6
        },
        new SpellEffectDefinition
        {
            TargetKind = TargetKind.Self,
            EffectKind = EffectKind.AddResource,
            ResourceType = ResourceType.ComboPoints,
            BaseValue = 1
        },
        new SpellEffectDefinition
        {
            TargetKind = TargetKind.Self,
            EffectKind = EffectKind.AddResource,
            ResourceType = ResourceType.Heat,
            BaseValue = 20
        }
        }
    };
    public class TestTicker : ISimulationTimeAdvance
    {
        public bool Initialized => true;
        public TimeSpan timeSpan { get; set; } = TimeSpan.Zero;
        public TimeSpan AdvanceTime()
        {
            return timeSpan;
        }
    }
        [SetUp]
    public void Setup()
    {
        GameImpurities.Characters.Clear();
        GameImpurities.ResourceStates.Clear();
        GameImpurities.StartCycle();
    }

    [Test]
    public void Test1()
    {
        TestTicker t = new TestTicker();
        GameImpurities.SimulationTick = t;
        Character character1 = new Character(Guid.NewGuid(), "Test Character 1", new PrimaryStats(10, 10, 10, 10, 10));
        Character character2 = new Character(Guid.NewGuid(), "Test Character 2", new PrimaryStats(15, 10, 10, 10, 10));
        GameImpurities.Characters.Add(character1.Id, character1);
        GameImpurities.Characters.Add(character2.Id, character2);
        ResourceState char1HP = new ResourceState() { ResourceType = ResourceType.Health, Current = character1.BaseStats.Endurance * 10, Maximum = character1.BaseStats.Endurance * 10 };
        ResourceState char1Heat = new ResourceState() { ResourceType = ResourceType.Heat, Current = 0, Maximum = 100 };
        ResourceState char1ComboPoints = new ResourceState() { ResourceType = ResourceType.ComboPoints, Current = 0, Maximum = 5 };
        ResourceState char2HP = new ResourceState() { ResourceType = ResourceType.Health, Current = character2.BaseStats.Endurance * 10, Maximum = character2.BaseStats.Endurance * 10 };
        SortieState char1State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char1HP }, { ResourceType.Heat, char1Heat }, { ResourceType.ComboPoints, char1ComboPoints }, } };
        SortieState char2State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char2HP } } };
        GameImpurities.ResourceStates.Add(character1.Id, char1State.Resources);
        GameImpurities.ResourceStates.Add(character2.Id, char2State.Resources);
        
        int rng = 42;
        SpellCastResult result = GameImpurities.ResolveSpell(new SpellEvent() { PrimaryTargetId = character2, SourceId = character1, Spell = rapidCycle, RandomSeed = rng }, null);
        GameImpurities.RequestResourceChange(result.InstantCastResult.Value); //commit the edits.

        
        t.timeSpan = TimeSpan.FromSeconds(1); //simulating a full second of timing.
        GameImpurities.EndCycle();
        GameImpurities.StartCycle();

        result = GameImpurities.ResolveSpell(new SpellEvent() { PrimaryTargetId = character2, SourceId = character1, Spell = chargeCycle, RandomSeed = rng }, null);
        Assert.That(result.FailureReason, Is.EqualTo(SpellFailReason.OnCooldown)); //should demonstrate the GCD being incomplete.
        
        result = GameImpurities.ResolveSpell(new SpellEvent() { PrimaryTargetId = character2, SourceId = character1, Spell = chargeCycle, RandomSeed = rng }, null);

        t.timeSpan = TimeSpan.FromSeconds(2); //two seconds have passed since simulation start.
        GameImpurities.EndCycle();
        GameImpurities.StartCycle();
        SpellCastResult result2 = GameImpurities.ResolveSpell(new SpellEvent() { PrimaryTargetId = character2, SourceId = character1, Spell = rapidCycle, RandomSeed = rng }, null);
        Assert.That(result2.FailureReason, Is.EqualTo(SpellFailReason.OnCooldown)); //should assert that the spell failed because it is already on CD.

        result  = GameImpurities.ResolveSpell(new SpellEvent() { PrimaryTargetId = character2, SourceId = character1, Spell = chargeCycle, RandomSeed = rng }, null);
        Assert.That(result.Success);
        Assert.That(result.InstantCastResult, Is.Null); //this completed, but has to go into the timers to finish casting.
        t.timeSpan = TimeSpan.FromSeconds(4); //four seconds have passed since simulation start.

        GameImpurities.EndCycle();
        
        GameImpurities.StartCycle();
        t.timeSpan = TimeSpan.FromSeconds(5); //5 seconds have passed since simulation start.
        GameImpurities.EndCycle();
        HashSet<ActiveTimer> expiredTimers = GameImpurities.ExpiredTimers();
        GameImpurities.StartCycle();
        
        result2 = GameImpurities.ResolveSpell(new SpellEvent() { PrimaryTargetId = character2, SourceId = character1, Spell = rapidCycle, RandomSeed = rng }, null);
        Assert.That(result2.FailureReason, Is.EqualTo(SpellFailReason.None));
        
    }
}
