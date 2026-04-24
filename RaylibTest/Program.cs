using Raylib_cs;
using GameCore;
using System.Numerics;

namespace RaylibTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            const int screenWidth = 800;
            const int screenHeight = 600;

            Raylib.InitWindow(screenWidth, screenHeight, "Raylib Test");
            Raylib.SetTargetFPS(60);

            RaylibThings.Actor player = new RaylibThings.Actor("Player", 400, 300)
            {
                Speed = 5f,
                Radius = 8f,
                DrawColor = Color.Blue
            };

            RaylibThings.Actor enemy = new RaylibThings.Actor("Enemy", 550, 300)
            {
                Speed = 0f,
                Radius = 8f,
                DrawColor = Color.Red
            };
            ResourceState char1HP = new ResourceState() { ResourceType = ResourceType.Health, Current = player.BaseStats.Endurance * 10, Maximum = player.BaseStats.Endurance * 10 };
            ResourceState char1Heat = new ResourceState() { ResourceType = ResourceType.Heat, Current = 0, Maximum = 100 };
            ResourceState char1ComboPoints = new ResourceState() { ResourceType = ResourceType.ComboPoints, Current = 0, Maximum = 5 };
            ResourceState char2HP = new ResourceState() { ResourceType = ResourceType.Health, Current = enemy.BaseStats.Endurance * 10, Maximum = player.BaseStats.Endurance * 10 };
            SortieState char1State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char1HP }, { ResourceType.Heat, char1Heat }, { ResourceType.ComboPoints, char1ComboPoints }, } };
            SortieState char2State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char2HP } } };
            GameImpurities.ResourceStates.Add(player.Guid, char1State.Resources);
            GameImpurities.ResourceStates.Add(enemy.Guid, char2State.Resources);

            Guid? selectedGuid = null;
            List<RaylibThings.SpellBlock> spellBlocks = new();
            GameImpurities.InitializeGame();
            while (!Raylib.WindowShouldClose())
            {

                GameImpurities.StartCycle();
                // Movement
                if (Raylib.IsKeyDown(KeyboardKey.W)) player.Y -= player.Speed;
                if (Raylib.IsKeyDown(KeyboardKey.S)) player.Y += player.Speed;
                if (Raylib.IsKeyDown(KeyboardKey.A)) player.X -= player.Speed;
                if (Raylib.IsKeyDown(KeyboardKey.D)) player.X += player.Speed;

                player.X = Math.Clamp(player.X, 0, screenWidth);
                player.Y = Math.Clamp(player.Y, 0, screenHeight);

                // Click detection for red circle
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    Vector2 mousePos = Raylib.GetMousePosition();

                    if (Raylib.CheckCollisionPointCircle(mousePos, new Vector2(enemy.X, enemy.Y), enemy.Radius))
                    {
                        selectedGuid = enemy.Guid;
                        Console.WriteLine($"Clicked enemy GUID: {enemy.Guid}");
                    }
                }

                // Fire spell at red circle with key 1
                if (Raylib.IsKeyPressed(KeyboardKey.One))
                {
                    RaylibThings.SpellBlock spell = RaylibThings.ShootTestSpell(player, enemy);
                    spellBlocks.Add(spell);
                }

                // Update spell projectiles
                for (int i = spellBlocks.Count - 1; i >= 0; i--)
                {
                    RaylibThings.SpellBlock block = spellBlocks[i];

                    block.X += block.DirectionX * block.Speed;
                    block.Y += block.DirectionY * block.Speed;

                    float dx = enemy.X - block.X;
                    float dy = enemy.Y - block.Y;
                    float distanceToEnemy = MathF.Sqrt(dx * dx + dy * dy);

                    if (distanceToEnemy <= enemy.Radius)
                    {
                        Console.WriteLine($"Spell hit enemy {enemy.Guid}");
                        spellBlocks.RemoveAt(i);
                        continue;
                    }

                    // Remove if off screen
                    if (block.X < 0 || block.X > screenWidth || block.Y < 0 || block.Y > screenHeight)
                    {
                        spellBlocks.RemoveAt(i);
                        continue;
                    }

                    spellBlocks[i] = block;
                }
                GameImpurities.EndCycle();
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.DarkGray);

                Raylib.DrawCircle((int)player.X, (int)player.Y, player.Radius, player.DrawColor);
                Raylib.DrawText(
    $"{GameImpurities.ResourceStates[enemy.Guid][ResourceType.Health].Current}/{GameImpurities.ResourceStates[enemy.Guid][ResourceType.Health].Maximum}",
    (int)enemy.X - 20,
    (int)enemy.Y - 25,
    16,
    Color.White);
                Raylib.DrawCircle((int)enemy.X, (int)enemy.Y, enemy.Radius, enemy.DrawColor);

                for (int i = 0; i < spellBlocks.Count; i++)
                {
                    Raylib.DrawCircle((int)spellBlocks[i].X, (int)spellBlocks[i].Y, 4f, Color.Yellow);
                }

                if (selectedGuid.HasValue)
                {
                    Raylib.DrawText($"Selected: {selectedGuid.Value}", 20, 20, 20, Color.White);
                }

                Raylib.EndDrawing();
                
            }

            Raylib.CloseWindow();
        }
    }

    public static class RaylibThings
    {
        public class Actor
        {
            public Guid Guid { get; init; }
            public string Name { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Radius { get; set; } = 5f;
            public Color DrawColor { get; set; } = Color.Blue;
            public SpellDefinition TestSpell { get; set; }
            public float Speed { get; set; } = 0.0f;

            public const float MAX_SPEED = 100.0f;
            public PrimaryStats BaseStats { get; set; }
            public Actor(string name, float x, float y)
            {
                Name = name;
                X = x;
                Y = y;

                Guid id = Guid.NewGuid();
                Guid = id;
                BaseStats = new PrimaryStats(10, 10, 10, 10, 10);
                GameImpurities.Characters.Add(id, new Character(id, name, BaseStats));
                TestSpell = rapidCycle;
            }
        }
        

        public static SpellBlock ShootTestSpell(Actor source, Actor? target)
        {
            float dx = target.X - source.X;
            float dy = target.Y - source.Y;
            float length = MathF.Sqrt(dx * dx + dy * dy);
            SpellEvent result;
            SpellCastRequest request = new SpellCastRequest()
            {
                SourceId = source.Guid, PrimaryTargetId = target.Guid, SpellId = source.TestSpell.Id
            };
            if (!GameImpurities.CreateTestSpellEvent(request, source.TestSpell, out result))
            {
                Console.WriteLine("Fail. No Target.");

            }
            SpellCastResult spellResults = GameImpurities.ResolveSpell(result, result.WeaponView);
            GameImpurities.RequestResourceChange(spellResults.InstantCastResult!.Value);
            if (length == 0)
            {
                length = 1;
            }

            float dirX = dx / length;
            float dirY = dy / length;

            return new SpellBlock
            {
                X = source.X,
                Y = source.Y,
                DirectionX = dirX,
                DirectionY = dirY,
                Speed = 8f
            };
        }

        public class SpellBlock
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float DirectionX { get; set; }
            public float DirectionY { get; set; }
            public float Speed { get; set; } = 0.0f;
        }

        private static SpellDefinition rapidCycle { get; } = new SpellDefinition
        {
            Id = 100,
            Name = "Rapid Cycle",
            MinimumDistance = 0,
            MaximumDistance = 30,
            RequiresLineOfSight = true,
            AdhereToGlobalCooldown = true,
            CastType = CastType.Instant,
            Duration = null,
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
        };
    }
}