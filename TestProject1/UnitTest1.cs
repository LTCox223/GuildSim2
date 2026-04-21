using GameCore;

namespace TestProject1
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
            GameImpurities.Characters.Clear();
            GameImpurities.CurrentHealth.Clear();
        }

        [Test]
        public void AutoAttack_Unarmed_Test()
        {
            Character character1 = new Character(Guid.NewGuid(), "Test Character 1", new PrimaryStats(10, 10, 10, 10, 10));
            Character character2 = new Character(Guid.NewGuid(), "Test Character 2", new PrimaryStats(15, 10, 10, 10, 10));
            GameImpurities.Characters.Add(character1.Id, character1);
            GameImpurities.Characters.Add(character2.Id, character2);
            GameImpurities.CurrentHealth.Add(character1.Id, character1.BaseStats.Endurance * 10);
            GameImpurities.CurrentHealth.Add(character2.Id, character2.BaseStats.Endurance * 10);
            int rng = 42;
            int expectedDamage = character1.BaseStats.Strength / 4; // No weapon, so damage is based on strength only
            int expectedHealth = (character2.BaseStats.Endurance * 10) - expectedDamage;
            Assert.AreEqual(expectedHealth, GameImpurities.CurrentHealth[character2.Id]);
        }
        [Test]
        public void AutoAttack_Sword_Test()
        {
            Character character1 = new Character(Guid.NewGuid(), "Test Character 1", new PrimaryStats(10, 10, 10, 10, 10));
            Character character2 = new Character(Guid.NewGuid(), "Test Character 2", new PrimaryStats(15, 10, 10, 10, 10));
            GameImpurities.Characters.Add(character1.Id, character1);
            GameImpurities.Characters.Add(character2.Id, character2);
            GameImpurities.CurrentHealth.Add(character1.Id, character1.BaseStats.Endurance * 10);
            GameImpurities.CurrentHealth.Add(character2.Id, character2.BaseStats.Endurance * 10);
            EquipmentDefinition swordDefinition = new EquipmentDefinition() { ID = 1, BaseName = "Test Sword", Slot = EquipmentSlot.MainHand, AttackMin = 5, AttackMax = 10, CanRollModifiers = false, StatBonuses = new PrimaryStats(0,0,0,0,0) };
            EquipmentInstance actualSword = EquipmentGenerator.GenerateInstance(Guid.NewGuid(), swordDefinition, null);
            character1.EquippedItems.Add(actualSword);
            int rng = 42;
            int expectedDamage = 5 + (rng % (10 - 5 + 1)) + character1.BaseStats.Strength / 2; // Weapon damage plus strength bonus
            int expectedHealth = (character2.BaseStats.Endurance * 10) - expectedDamage;
            Assert.AreEqual(expectedHealth, GameImpurities.CurrentHealth[character2.Id]);
        }
    }
}