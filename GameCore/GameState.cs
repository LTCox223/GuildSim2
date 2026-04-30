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

namespace GameCore
{
    public sealed class GameState
    {
        private World gameWorld { get; }
        public World GameWorld => gameWorld;
        public static GameState? Instance { get; private set; }
        public void Update()
        {
            var query = new QueryDescription().WithExclusive<SpellCastIntent>();
            gameWorld.Query(
    in query,
    (Entity requestEntity, ref SpellCastIntent intent) =>
    {
        Entity sourceEntity = intent.OwnerId;
        Entity? targetEntity = intent.PrimaryTargetId;
        sourceEntity.TryGet<GCDLock?>(out GCDLock? gcd);
        bool check = Spells.SpellRequestCheck(intent, sourceEntity.Get<SpellLocks>(), gcd);
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
                PrimaryTargetId = targetEntity.HasValue? targetEntity.Value.Get<Character>() : null,
                Spell = intent.Spell,
                RandomSeed = GameImpurities.GetRandomInt(),
            };
            SpellEventEntity spellEntity = new SpellEventEntity() { Source = sourceEntity, SpellEvent = spellEvent};
        }
        
    });
            gameWorld.Destroy(query); //destroy all queries for cast requests after working on them.
            
        }
        public void End()
        {
            //systems write to the ECS here.
            //time cycles here.
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

        public Entity CreateCharacter(Character character, int experience, PrimaryStats stats, JobClass job, Vector2 location, float speed, Dictionary<ResourceType,ResourceState> resources)
        {
            SpellLocks spellLocks = new SpellLocks();
            return gameWorld.Create(character, experience, stats, job, location, speed, spellLocks, resources);
        }
    }

    #region Component Library
    #endregion
}
