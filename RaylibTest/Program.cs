using Raylib_cs;
using GameCore;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using System.Collections.Generic;

namespace RaylibTest
{
    internal class Program
    {

        static void Main(string[] args)
        {
            const int screenWidth = 800;
            const int screenHeight = 600;
            GameState state = new GameState(); //fire and forget... hopefully.
            Raylib.InitWindow(screenWidth, screenHeight, "Raylib Test");
            Raylib.SetTargetFPS(60);

            RaylibThings.Actor player = new RaylibThings.Actor("Player", 400, 300)
            {
                Speed = 5f,
                Radius = 8f,
                DrawColor = Color.Blue,
                
            };

            RaylibThings.Actor enemy = new RaylibThings.Actor("Enemy", 550, 300)
            {
                Speed = 0f,
                Radius = 8f,
                DrawColor = Color.Red
            };
            Random rand = new Random();
            //for (int i = 0; i < 100; i++)
            //{
                
            //    Vector2 pos = new Vector2(rand.Next(screenWidth), rand.Next(screenHeight));
            //    GameState.Instance.CreateCharacter(character, 1, new PrimaryStats(1, 1, 1, 1, 1), new JobClass(), pos, 5f);
            //}
            ResourceState char1HP = new ResourceState() { ResourceType = ResourceType.Health, Current = player.BaseStats.Endurance * 10, Maximum = player.BaseStats.Endurance * 10 };
            ResourceState char1Heat = new ResourceState() { ResourceType = ResourceType.Heat, Current = 0, Maximum = 100 };
            ResourceState char1ComboPoints = new ResourceState() { ResourceType = ResourceType.ComboPoints, Current = 0, Maximum = 5 };
            ResourceState char2HP = new ResourceState() { ResourceType = ResourceType.Health, Current = enemy.BaseStats.Endurance * 10, Maximum = player.BaseStats.Endurance * 10 };
            SortieState char1State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char1HP }, { ResourceType.Heat, char1Heat }, { ResourceType.ComboPoints, char1ComboPoints }, } };
            SortieState char2State = new SortieState() { Resources = new Dictionary<ResourceType, ResourceState>() { { ResourceType.Health, char2HP } } };
            Entity playerE = GameState.Instance.CreateCharacter(player.Character, 0, player.BaseStats, new JobClass(), new Vector2(400, 300), 5f, char1State.Resources);
            Entity enemyE = GameState.Instance.CreateCharacter(enemy.Character, 0, enemy.BaseStats, new JobClass(), new Vector2(550, 300),0f, char2State.Resources);

            Entity? selectedTarget = null;
            List<RaylibThings.SpellBlock> spellBlocks = new();
            GameImpurities.InitializeGame();
            GameState.Instance.InitializeTime(null);
            while (!Raylib.WindowShouldClose())
            {
                GameImpurities.StartCycle();
                // Movement
                float tempY = player.Y;
                float tempX = player.X;
                if (Raylib.IsKeyDown(KeyboardKey.W)) player.Y -= player.Speed;
                if (Raylib.IsKeyDown(KeyboardKey.S)) player.Y += player.Speed;
                if (Raylib.IsKeyDown(KeyboardKey.A)) player.X -= player.Speed;
                if (Raylib.IsKeyDown(KeyboardKey.D)) player.X += player.Speed;
                if (Raylib.IsKeyDown(KeyboardKey.R))
                {
                    Regenerate();
                }
                if (tempY != player.Y || tempX != player.X)
                {
                    if (!playerE.Has<MovingFlag>()) 
                        playerE.Add(playerE,new MovingFlag());
                }
                else
                {
                    if (playerE.Has<MovingFlag>())
                        playerE.Remove<MovingFlag>();
                }
                player.X = Math.Clamp(player.X, 0, screenWidth);
                player.Y = Math.Clamp(player.Y, 0, screenHeight);

                // Click detection for red circle
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    Vector2 mousePos = Raylib.GetMousePosition();

                    if (Raylib.CheckCollisionPointCircle(mousePos, new Vector2(enemy.X, enemy.Y), enemy.Radius))
                    {
                        selectedTarget = enemyE;
                        Console.WriteLine($"Clicked enemy GUID: {enemy.Guid}");
                    }
                }

                // Fire spell at red circle with key 1
                if (Raylib.IsKeyPressed(KeyboardKey.One))
                {
                    RaylibThings.ShootSpell(playerE, player.TestSpell1, selectedTarget);

                }
                if (Raylib.IsKeyPressed(KeyboardKey.Two))
                {
                    RaylibThings.ShootSpell(playerE, player.TestSpell2, selectedTarget);

                }
                var query = new QueryDescription().WithAll<Dictionary<ResourceType, ResourceState>, Character>();
                GameState.Instance.GameWorld.Query(in query, (Entity entity, ref Dictionary<ResourceType, ResourceState> newResources, ref Character chara) => {
                    if (chara == enemyE.Get<Character>())
                    {
                        enemyE.Set<Dictionary<ResourceType, ResourceState>>(newResources);
                    }
                    else if (chara == playerE.Get<Character>())
                    {
                        playerE.Set<Dictionary<ResourceType, ResourceState>>(newResources);
                        playerE.Set<Vector2> (new Vector2(player.X, player.Y));
                    }
                });

                GameState.Instance.Update();

                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.DarkGray);

                Raylib.DrawCircle((int)player.X, (int)player.Y, player.Radius, player.DrawColor);
                Dictionary<ResourceType, ResourceState> enemyResource = enemyE.Get<Dictionary<ResourceType, ResourceState>>();
                Dictionary<ResourceType, ResourceState> playerResource = playerE.Get<Dictionary<ResourceType, ResourceState>>();
                string health = $"{enemyResource[ResourceType.Health].Current} / {enemyResource[ResourceType.Health].Maximum}";
                string playerHealth = $"{playerResource[ResourceType.Health].Current} / {playerResource[ResourceType.Health].Maximum}";
                string playerHeat = $"{playerResource[ResourceType.Heat].Current} / {playerResource[ResourceType.Heat].Maximum}";
                Raylib.DrawText(health, 525, 275, 12, Color.White);
                Raylib.DrawText(playerHealth, (int)player.X-25, (int)player.Y-25, 12, Color.White);
                Raylib.DrawText(playerHeat, (int)player.X - 25, (int)player.Y + 25, 12, Color.Orange);
                Raylib.DrawCircle((int)enemy.X, (int)enemy.Y, enemy.Radius, enemy.DrawColor);
                for (int i = 0; i < spellBlocks.Count; i++)
                {
                    Raylib.DrawCircle((int)spellBlocks[i].X, (int)spellBlocks[i].Y, 4f, spellBlocks[i].SpellColor);
                }
                if (selectedTarget != null)
                {
                    Raylib.DrawText($"Selected: {selectedTarget.ToString()}", 20, 20, 20, Color.White);
                }
                var drawQuery = new QueryDescription().WithAll<Vector2>();
                GameState.Instance.GameWorld.Query(in drawQuery, (ref Vector2 pos) => { Raylib.DrawCircleV(pos, 4, Color.Red); });

                Raylib.EndDrawing();                
            }
            Raylib.CloseWindow();
        }
        public static void Regenerate()
        {
            const int screenWidth = 800;
            const int screenHeight = 600;
            Random rand = new Random();
            Character character = new Character();
            var removeal = new QueryDescription().WithAll<Vector2>();
            GameState.Instance.GameWorld.Destroy(in removeal);
            for (int i = 0; i < 100; i++)
            {
                
                Vector2 pos = new Vector2(rand.Next(screenWidth), rand.Next(screenHeight));
                GameState.Instance.CreateCharacter(character, 1, new PrimaryStats(1, 1, 1, 1, 1), new JobClass(), pos, 5f, new Dictionary<ResourceType, ResourceState>());
            }
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
            public SpellDefinition TestSpell1 { get; set; }
            public SpellDefinition TestSpell2 {get; set; }
            public float Speed { get; set; } = 0.0f;
            public Character Character { get; set; }

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
                Character = new Character(id, name, BaseStats);
                //GameImpurities.Characters.Add(id, new Character(id, name, BaseStats));
                TestSpell1 = rapidCycle;
                TestSpell2 = chargeCycle;
            }
        }
        

        public static void ShootSpell(Entity source, SpellDefinition spell, Entity? target)
        {
            if (target == null) return;
            SpellCastIntent intent = Spells.CreateIntent(spell, source, target);

            GameState.Instance!.GameWorld.Create(intent);
        }

        public class SpellBlock
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float DirectionX { get; set; }
            public float DirectionY { get; set; }
            public float Speed { get; set; } = 0.0f;
            public Color SpellColor {get; set; }
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
                    BaseValue = 1,
                    RequiredForValidation = false
                },
                new SpellEffectDefinition
                {
                    TargetKind = TargetKind.Self,
                    EffectKind = EffectKind.AddResource,
                    ResourceType = ResourceType.Heat,
                    BaseValue = 20,
                    RequiredForValidation = true
                }
            },
            Cooldown = TimeSpan.FromSeconds(5),
        };

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
            BaseValue = 1,
            RequiredForValidation = false
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

    }
}