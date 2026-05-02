using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Arch;
using Arch.Core;
using Arch.Core.Extensions;
using System.Reflection.Metadata.Ecma335;
using Arch.Buffer;
using System.Diagnostics;

namespace GameCore
{
    public sealed class GameState
    {
        private TimeSpan _lastTime { get; set; } = TimeSpan.Zero;
        private TimeSpan _currentTime { get; set; } = TimeSpan.Zero;
        public TimeSpan CurrentTime { get => _currentTime; }
        public ISimulationTimeAdvance TimeAdvance;
        private World gameWorld { get; }
        public World GameWorld => gameWorld;
        public static GameState? Instance { get; private set; }
        public void InitializeTime(ISimulationTimeAdvance? timeAdvance)
        {
            if (timeAdvance != null)
            {
                TimeAdvance = timeAdvance;
            }
            else
                TimeAdvance = new GameImpurities.ImpuritiesSimulationTick();
            if (!TimeAdvance.Initialized)
                TimeAdvance.AdvanceTime();
        }
        private void SpellUpdate()
        {
            var query = new QueryDescription().WithExclusive<SpellCastIntent>();
            gameWorld.Query(in query,
                static (Entity requestEntity, ref SpellCastIntent intent) => SpellRequests(requestEntity, ref intent));

            gameWorld.Destroy(query); //destroy all queries for cast requests after working on them.

            query = new QueryDescription().WithAll<SpellEventComponent>().WithNone<SpellInstantFlag>();
            gameWorld.Query(in query, (Entity source, ref SpellEventComponent component) => {
                Entity spellSource = component.Source;
                if (spellSource.Has<MovingFlag>())
                {
                    source.Add(new SpellCancelFlag());
                }
            });
            query = new QueryDescription().WithAll<SpellCancelFlag>();
            gameWorld.Destroy(query);
            query = new QueryDescription().WithAll<SpellEventComponent>();
            gameWorld.Query(in query, (Entity entity, ref SpellEventComponent spell) => {
                if(entity.Has<SpellInstantFlag>())
                {
                    SpellInstant(ref spell); 
                }
                
                SpellCleanup(ref spell); 
                });
            gameWorld.Destroy(query);

            query = new QueryDescription().WithAll<ResourceChange>();
            gameWorld.Query(in query, (Entity source, ref ResourceChange change) => {
                AffectedEntity affected = source.Get<AffectedEntity>();
                Entity entity = affected.Affected;
                Dictionary<ResourceType,ResourceState> keyValuePairs = entity.Get<Dictionary<ResourceType,ResourceState>>();
                ResourceState state = keyValuePairs[change.ResourceType];
                int newState = state.Current + change.Amount;
                ResourceState newResource = new ResourceState() { Current = newState, ResourceType = state.ResourceType, Maximum = state.Maximum };
                keyValuePairs[change.ResourceType] = newResource;
                entity.Set<Dictionary<ResourceType,ResourceState>>(keyValuePairs);
            }
            );
            gameWorld.Destroy(query);
            
        }
        public void Update()
        {

            ResourceUpdate();
            SpellUpdate();
            var query = new QueryDescription().WithAll<SpellLocks>();
            gameWorld.Query(in query, (Entity entity, ref SpellLocks locks) =>
            {
                Queue<int> expiredSpells = new Queue<int>();
                foreach(var kvp in locks.SpellsOnCooldown)
                {
                    if(kvp.Value < Instance.CurrentTime)
                    {
                        expiredSpells.Enqueue(kvp.Key);
                    }
                }
                foreach (int value in expiredSpells)
                {
                    locks.SpellsOnCooldown.Remove(value);
                }
            });
            query = new QueryDescription().WithAny<GCDLock>();
            gameWorld.Query(in query, (Entity entity, ref GCDLock gcd) =>
            {
                if (gcd.ExpireAt < Instance.CurrentTime)
                {
                    entity.Remove<GCDLock>();
                }
            });
            End();
        }

        private void ResourceUpdate()
        {
            TimeSpan check = _currentTime - _lastTime;
            if ( check > TimeSpan.FromSeconds(1) )
            {
                _lastTime = _currentTime;
                var query = new QueryDescription().WithAll<Dictionary<ResourceType,ResourceState>>();
                gameWorld.Query(in query, (Entity entity, ref Dictionary<ResourceType, ResourceState> resources) =>
                {
                    if (resources.ContainsKey(ResourceType.Heat))
                    {
                        ResourceState state = resources[ResourceType.Heat];
                        int newState = state.Current - 5;
                        newState = Math.Clamp(newState, 0, state.Maximum);
                        ResourceState newResource = new ResourceState() { Current = newState, ResourceType = state.ResourceType, Maximum = state.Maximum };
                        resources[ResourceType.Heat] = newResource;
                        entity.Set<Dictionary<ResourceType, ResourceState>>(resources);
                    }
                });
            }
        }
        public void End()
        {
            _currentTime = new TimeSpan(_currentTime.Ticks + TimeAdvance!.AdvanceTime().Ticks);
        }
        public GameState()
        {
            gameWorld = World.Create();
            Instance = this;
        }
        public void Dispose()
        {
            gameWorld.Dispose();
        }

        public Entity CreateCharacter(Character character, int experience, PrimaryStats stats, JobClass job, Vector2 location, float speed, Dictionary<ResourceType, ResourceState> resources)
        {
            SpellLocks spellLocks = new SpellLocks();
            return gameWorld.Create(character, experience, stats, job, location, speed, spellLocks, resources);
        }

        private static void SpellRequests(Entity requester, ref SpellCastIntent intent) //handles the incoming spell requests.
        {
            Entity sourceEntity = intent.OwnerId;
            Entity? targetEntity = intent.PrimaryTargetId;
            bool check = false;
            if (sourceEntity.TryGet<GCDLock>(out GCDLock gcd))
            {
                check = Spells.SpellRequestCheck(intent, sourceEntity.Get<SpellLocks>(), gcd);
            }
            else
            {
                check = Spells.SpellRequestCheck(intent, sourceEntity.Get<SpellLocks>(), null);
            }
            if (!check)
            {
                return;
            }
            int validEffects = 0;
            foreach (SpellEffectDefinition effect in intent.Spell.Effects)
            {
                check = Spells.TargetSpellEffectValidation(effect, sourceEntity.Get<PrimaryStats>(), sourceEntity.Get<Dictionary<ResourceType, ResourceState>>(), targetEntity!.Value.Get<Dictionary<ResourceType, ResourceState>>());
                if (check)
                    validEffects++;
            }
            if (validEffects == intent.Spell.Effects.Count)
            {
                SpellEvent spellEvent = new SpellEvent()
                {
                    SourceId = sourceEntity.Get<Character>(),
                    PrimaryTargetId = targetEntity.HasValue ? targetEntity.Value.Get<Character>() : null,
                    Spell = intent.Spell,
                    RandomSeed = GameImpurities.GetRandomInt(),
                };
                SpellEventComponent spellEntity = new SpellEventComponent() { SpellEvent = spellEvent, Source = intent.OwnerId, Target = targetEntity };
                switch (intent.Spell.CastType)
                {
                    case CastType.Instant:
                        GameState.Instance.GameWorld.Create(spellEntity, new SpellInstantFlag());
                        break;
                    case CastType.Channeled:
                        GameState.Instance.GameWorld.Create(spellEntity, new SpellInstantFlag());
                        break;
                    case CastType.Charged:
                        GameState.Instance.GameWorld.Create(spellEntity, new SpellCastComponent() { ExpireAt = GameState.Instance.CurrentTime + spellEvent.Spell.Duration!.Value});
                        break;
                }
            }
        }
        private static void SpellInstant(ref SpellEventComponent spell)
        {
            List<ResourceChange> change = SpellMath.ResolveEffects(spell.SpellEvent);
            Entity caster = spell.Source;
            Entity? target = spell.Target;
            Character casterC = caster.Get<Character>();
            Character? targetC;
            if (target.HasValue)
                targetC = target.Value.Get<Character>();
            for (int i = 0; i < change.Count; i++)
            {
                if (change[i].CharacterId == casterC)
                {
                    Instance.GameWorld.Create(change[i], new AffectedEntity() { Affected = caster});
                }
                else
                {
                    Instance.GameWorld.Create(change[i], new AffectedEntity() { Affected = target.Value });
                }
            }
        }

        public readonly record struct AffectedEntity
        {
            public Entity Affected { get; init; }
        }
        private static void SpellCasting(ref SpellEventComponent spell)
        {
            
        }
        private static void SpellCleanup(ref SpellEventComponent spell)
        {
            SpellEvent spellEvent = spell.SpellEvent;
            spell.Source.Add<GCDLock>(new GCDLock(){ ExpireAt = Instance.CurrentTime + TimeSpan.FromSeconds(1.5)});
            if (spellEvent.Spell.Cooldown != null && spellEvent.Spell.Cooldown != TimeSpan.FromSeconds(0))
            {
                SpellLocks locks = spell.Source.Get<SpellLocks>();
                if (!locks.SpellsOnCooldown.ContainsKey(spellEvent.Spell.Id))
                {
                    //locks.SpellsOnCooldown.Add(spellEvent.Spell.Id, Instance.CurrentTime + spellEvent.Spell.Cooldown.Value);
                }
            }
        }
        public class SimulationTick : ISimulationTimeAdvance
        {
            private bool firstCycle = true;
            public bool Initialized => firstCycle;
            private Stopwatch _stopwatch = new Stopwatch();
            private TimeSpan _previousElapsed = TimeSpan.Zero;
            public TimeSpan AdvanceTime()
            {
                if (firstCycle)
                {
                    _stopwatch.Start();
                    firstCycle = false;
                    return TimeSpan.Zero;
                }

                TimeSpan currentElapsed = _stopwatch.Elapsed;
                TimeSpan delta = currentElapsed - _previousElapsed;
                _previousElapsed = currentElapsed;
                return delta;
            }
        }
    }

    #region Component Library
    #endregion
}
