using System.Diagnostics;
using GameCore;

namespace TestProject1
{
    public class CombatTests
    {
        private SpellDefinition meleeAttack { get; init; } = new SpellDefinition()
        {
            Id = 1,
            Name = "Melee Attack",
            MinimumDistance = 0,
            MaximumDistance = 1.5,
            RequiresLineOfSight = true,
            Effects = new[]
            {
                new SpellEffectDefinition
                {
                    EffectKind = EffectKind.WeaponDamage,
                    BaseValue = 0,
                    ScalingStat = PrimaryStats.StatType.Strength,
                    ScalingFactor = 0.25
                }
            }
        };
        private SpellDefinition rangeAttack { get; init; } = new SpellDefinition()
        {
            Id = 2,
            Name = "Ranged Attack",
            MinimumDistance = 1.5,
            MaximumDistance = 30,
            RequiresLineOfSight = true,
            Effects = new[]
            {
                new SpellEffectDefinition
                {
                    EffectKind = EffectKind.WeaponDamage,
                    BaseValue = 0,
                    ScalingStat = PrimaryStats.StatType.Agility,
                    ScalingFactor = 0.25
                }
            }
        };
        private SpellDefinition rapidCycle { get; init; } = new SpellDefinition
        {
            Id = 100,
            Name = "Rapid Cycle",
            MinimumDistance = 0,
            MaximumDistance = 30,
            RequiresLineOfSight = true,
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
            Cooldown = TimeSpan.Zero
        };

        [SetUp]
        public void Setup()
        {
            GameImpurities.Characters.Clear();
            GameImpurities.SortieStates.Clear();
        }

        [Test]
        public void AutoAttack_Unarmed_Test()
        {
            Character character1 = new Character(Guid.NewGuid(), "Test Character 1", new PrimaryStats(10, 10, 10, 10, 10));
            Character character2 = new Character(Guid.NewGuid(), "Test Character 2", new PrimaryStats(15, 10, 10, 10, 10));
            GameImpurities.Characters.Add(character1.Id, character1);
            GameImpurities.Characters.Add(character2.Id, character2);
            ResourceState char1HP = new ResourceState() { ResourceType = ResourceType.Health, Current = character1.BaseStats.Endurance * 10, Maximum = character1.BaseStats.Endurance * 10 };
            ResourceState char2HP = new ResourceState() { ResourceType = ResourceType.Health, Current = character2.BaseStats.Endurance * 10, Maximum = character2.BaseStats.Endurance * 10 };
            SortieState char1State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char1HP } } };
            SortieState char2State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char2HP } } };
            GameImpurities.SortieStates.Add(character1.Id, char1State);
            GameImpurities.SortieStates.Add(character2.Id, char2State);
            int rng = 42;
            GameImpurities.ResolveSpell(new SpellCastRequest() { SourceId = character1.Id, PrimaryTargetId = character2.Id, Spell = meleeAttack, RandomSeed = rng }
            );
            GameImpurities.UpdateResources();
            int expectedDamage = character1.BaseStats.Strength / 4; // No weapon, so damage is based on strength only
            int expectedHealth = (character2.BaseStats.Endurance * 10) - expectedDamage;
            Assert.AreEqual(expectedHealth, GameImpurities.SortieStates[character2.Id].Resources[ResourceType.Health].Current);
        }
        [Test]
        public void AutoAttack_Sword_Test()
        {
            Character character1 = new Character(Guid.NewGuid(), "Test Character 1", new PrimaryStats(10, 10, 10, 10, 10));
            Character character2 = new Character(Guid.NewGuid(), "Test Character 2", new PrimaryStats(15, 10, 10, 10, 10));
            GameImpurities.Characters.Add(character1.Id, character1);
            GameImpurities.Characters.Add(character2.Id, character2);
            ResourceState char1HP = new ResourceState() { ResourceType = ResourceType.Health, Current = character1.BaseStats.Endurance * 10, Maximum = character1.BaseStats.Endurance * 10 };
            ResourceState char2HP = new ResourceState() { ResourceType = ResourceType.Health, Current = character2.BaseStats.Endurance * 10, Maximum = character2.BaseStats.Endurance * 10 };
            SortieState char1State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char1HP } } };
            SortieState char2State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char2HP } } };
            GameImpurities.SortieStates.Add(character1.Id, char1State);
            GameImpurities.SortieStates.Add(character2.Id, char2State);
            EquipmentDefinition swordDefinition = new EquipmentDefinition() { ID = 1, BaseName = "Test Sword", Slot = EquipmentSlot.MainHand, AttackMin = 5, AttackMax = 10, CanRollModifiers = false, StatBonuses = new PrimaryStats(0, 0, 0, 0, 0), Category = EquipmentCategory.Weapon };
            EquipmentInstance actualSword = EquipmentGenerator.GenerateInstance(Guid.NewGuid(), swordDefinition, null);
            WeaponView? swordView = WeaponView.Create(actualSword);
            GameImpurities.Weapons.Add(character1.Id, swordView!.Value); //literally made above...
            int rng = 42;
            var sw = new Stopwatch();
            sw.Start();
            GameImpurities.ResolveSpell(new SpellCastRequest() { SourceId = character1.Id, PrimaryTargetId = character2.Id, Spell = meleeAttack, RandomSeed = rng });
            GameImpurities.UpdateResources();
            sw.Stop();
            double seconds = (double)sw.ElapsedTicks / Stopwatch.Frequency;
            double nanosecondsPerCast = (seconds / 1000) * 1_000_000_000;
            Console.WriteLine($"Spell resolution took {nanosecondsPerCast} ns");
            int expectedDamage = 5 + (rng % (10 - 5 + 1)) + character1.BaseStats.Strength / 2; // Weapon damage plus strength bonus
            int expectedHealth = (character2.BaseStats.Endurance * 10) - expectedDamage;
            Assert.AreEqual(expectedHealth, GameImpurities.SortieStates[character2.Id].Resources[ResourceType.Health].Current);
        }
        [Test]
        public void A0_HotPath_Test()
        {
            Character character1 = new Character(Guid.NewGuid(), "Test Character 1", new PrimaryStats(10, 10, 10, 10, 10));
            Character character2 = new Character(Guid.NewGuid(), "Test Character 2", new PrimaryStats(15, 10, 10, 10, 10));
            GameImpurities.Characters.Add(character1.Id, character1);
            GameImpurities.Characters.Add(character2.Id, character2);
            ResourceState char1HP = new ResourceState() { ResourceType = ResourceType.Health, Current = character1.BaseStats.Endurance * 10, Maximum = character1.BaseStats.Endurance * 10 };
            ResourceState char2HP = new ResourceState() { ResourceType = ResourceType.Health, Current = character2.BaseStats.Endurance * 10, Maximum = character2.BaseStats.Endurance * 10 };
            SortieState char1State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char1HP } } };
            SortieState char2State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char2HP } } };
            GameImpurities.SortieStates.Add(character1.Id, char1State);
            GameImpurities.SortieStates.Add(character2.Id, char2State);
            EquipmentDefinition swordDefinition = new EquipmentDefinition() { ID = 1, BaseName = "Test Sword", Slot = EquipmentSlot.MainHand, AttackMin = 5, AttackMax = 10, CanRollModifiers = false, StatBonuses = new PrimaryStats(0, 0, 0, 0, 0), Category = EquipmentCategory.Weapon };
            EquipmentInstance actualSword = EquipmentGenerator.GenerateInstance(Guid.NewGuid(), swordDefinition, null);
            WeaponView? swordView = WeaponView.Create(actualSword);
            GameImpurities.Weapons.Add(character1.Id, swordView!.Value); //literally made above...
            int rng = 42;
            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < 1000; i++)
                GameImpurities.ResolveSpell(new SpellCastRequest() { SourceId = character1.Id, PrimaryTargetId = character2.Id, Spell = meleeAttack, RandomSeed = rng });
            GameImpurities.UpdateResources();
            sw.Stop();
            double seconds = (double)sw.ElapsedTicks / Stopwatch.Frequency;
            double nanosecondsPerCast = (seconds / 1000) * 1_000_000_000;
            Console.WriteLine($"Spell resolution took {nanosecondsPerCast} ns");
        }

        [Test]
        public void Spell_Test() //do me first.
        {
            Character character1 = new Character(Guid.NewGuid(), "Test Character 1", new PrimaryStats(10, 10, 10, 10, 10));
            Character character2 = new Character(Guid.NewGuid(), "Test Character 2", new PrimaryStats(15, 10, 10, 10, 10));
            GameImpurities.Characters.Add(character1.Id, character1);
            GameImpurities.Characters.Add(character2.Id, character2);
            ResourceState char1HP = new ResourceState() { ResourceType = ResourceType.Health, Current = character1.BaseStats.Endurance * 10, Maximum = character1.BaseStats.Endurance * 10 };
            ResourceState char1Heat = new ResourceState() { ResourceType = ResourceType.Heat, Current = 0, Maximum = 100 };
            ResourceState char1ComboPoints = new ResourceState() { ResourceType = ResourceType.ComboPoints, Current = 0, Maximum = 5 };
            ResourceState char2HP = new ResourceState() { ResourceType = ResourceType.Health, Current = character2.BaseStats.Endurance * 10, Maximum = character2.BaseStats.Endurance * 10 };
            SortieState char1State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char1HP },{ ResourceType.Heat, char1Heat},{ResourceType.ComboPoints, char1ComboPoints }, } };
            SortieState char2State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char2HP } } };
            GameImpurities.SortieStates.Add(character1.Id, char1State);
            GameImpurities.SortieStates.Add(character2.Id, char2State);
            EquipmentDefinition swordDefinition = new EquipmentDefinition() { ID = 1, BaseName = "Test Sword", Slot = EquipmentSlot.MainHand, AttackMin = 5, AttackMax = 10, CanRollModifiers = false, StatBonuses = new PrimaryStats(0, 0, 0, 0, 0), Category = EquipmentCategory.Weapon };

            EquipmentInstance actualSword = EquipmentGenerator.GenerateInstance(Guid.NewGuid(), swordDefinition, null);
            WeaponView? swordView = WeaponView.Create(actualSword);
            GameImpurities.Weapons.Add(character1.Id, swordView!.Value); //literally made above...

            int rng = 42;
            GameImpurities.ResolveSpell(new SpellCastRequest() { PrimaryTargetId = character2.Id, SourceId = character1.Id,Spell = rapidCycle, RandomSeed = rng });
            GameImpurities.UpdateResources();
            int expectedTechDamage = 8 + (int)(character1.BaseStats.Agility * 0.6);
            int expectedComboPoints = 1;
            int expectedHeat = 20;
            int expectedWeaponDamage = SpellMath.CalculateWeaponDamage(swordView, character1.BaseStats.Strength, rng); //feel confident in this.
            int expectedHealth = (character2.BaseStats.Endurance * 10) - expectedTechDamage - expectedWeaponDamage;

            Assert.AreEqual(expectedHealth, GameImpurities.SortieStates[character2.Id].Resources[ResourceType.Health].Current);
            Assert.IsTrue(GameImpurities.SortieStates[character1.Id].Resources[ResourceType.ComboPoints].Current == expectedComboPoints);
            Assert.IsTrue(GameImpurities.SortieStates[character1.Id].Resources[ResourceType.Heat].Current == expectedHeat);
        }
    }
}