using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static GameCore.PrimaryStats;

namespace GameCore
{
    public static class GameImpurities
    {
        #region Fields
        private static TimeSpan CurrentTime { get; set; } = TimeSpan.Zero; //the current time of the sortie according to the simulation clock. This is the definitive Time GameImpurities uses. SimulationClock only advances time.
        public static ISimulationTimeAdvance? SimulationTick;
        public static Dictionary<Guid, Character> Characters { get; private set; } = new Dictionary<Guid, Character>(); //characters in the sortie, indexed by their unique ID. This is all characters.
        public static Dictionary<Guid, WeaponView> Weapons { get; private set; } = new Dictionary<Guid, WeaponView>(); //weapons from characters.
        public static Dictionary<Guid, Dictionary<ResourceType, ResourceState>> ResourceStates { get; private set; } = new Dictionary<Guid, Dictionary<ResourceType, ResourceState>>();
        private static Dictionary<Guid,PendingSpellCast> pendingSpellCasts { get; set; } = new Dictionary<Guid, PendingSpellCast>();
        private static Queue<ResourceChange> resourceChanges { get; set; } = new Queue<ResourceChange>();
        private static Queue<TimerRequest> timerRequests { get; set; } = new Queue<TimerRequest>();
        private static HashSet<ActiveTimer> activeTimers { get; set; } = new HashSet<ActiveTimer>();
        private static HashSet<Guid> activeGcdOwners { get; set; } = new();
        private static HashSet<(Guid OwnerId, int SpellId)> activeSpellLocks { get; set; } = new();
        public static Random Random { get; private set; } = new Random();
        #endregion

        #region Randomization Functions
        public static int GetRandomInt(int min, int max)
        {
            return Random.Next(min, max);
        }
        private static int SetNewRandomSeed()
        {
            int newSeed = Random.Next();
            Random = new Random(newSeed);
            return newSeed;
        }
        #endregion

        #region Equipment Generation
        public static EquipmentInstance GenerateRandomEquipment()
        {
            EquipmentDefinition randomDefinition = GetTrueRandomEquipmentDefinition();
            ItemModifierTemplate randomModifier = GetTrueRandomModifier();
            return EquipmentGenerator.GenerateInstance(Guid.NewGuid(), randomDefinition, randomModifier);
        }
        private static ItemModifierTemplate GetTrueRandomModifier()
        {
            int randomTemplate = GetRandomInt(1, ItemModifierDatabase.ModifierCount + 1); // Placeholder for actual item modifier templates
            return ItemModifierDatabase.GetModifier(randomTemplate);
        }
        private static EquipmentDefinition GetTrueRandomEquipmentDefinition()
        {
            int randomDefinition = GetRandomInt(1, EquipmentDatabase.DefinitionCount + 1); // Placeholder for actual equipment definitions
            return EquipmentDatabase.GetDefinition(randomDefinition);
        }
        #endregion

        #region GameLogic Functions

        

        public static SpellCastResult ResolveSpell(SpellEvent request, WeaponView? weapon)
        {
            //Target validation.
            if (!request.PrimaryTargetId.HasValue)
            {
                return new SpellCastResult() { Success = false, FailureReason = SpellFailReason.InvalidTarget };
            }
            IReadOnlyDictionary<ResourceType, ResourceState> target = ResourceStates[request.PrimaryTargetId.Value.Id];
            if (!target.TryGetValue(ResourceType.Health, out ResourceState targetHealth))
            {
                return new SpellCastResult() { Success = false, FailureReason = SpellFailReason.InvalidTarget }; //its... cant hit it.
            }
            /*
             * Validate timers. 
             * If there is a timer with the GUID of the character and the spell at all, fail. Spell is either on cooldown, or is being cast.
             * If there is a timer with the GCD for the character, fail. Character is on GCD.
             * Else timers are valid.
             */
            bool hasSpellTimer = activeSpellLocks.Contains((request.SourceId.Id, request.Spell.Id));
            bool hasGcdTimer = request.Spell.AdhereToGlobalCooldown &&
                               activeGcdOwners.Contains(request.SourceId.Id);
            if (hasSpellTimer)
            {
                return new SpellCastResult() { Success = false, FailureReason = SpellFailReason.OnCooldown };
            }

            if (hasGcdTimer && request.Spell.AdhereToGlobalCooldown)
            {
                return new SpellCastResult() { Success = false, FailureReason = SpellFailReason.OnCooldown };
            }

            //Handle GCD if applicable. If the spell adheres to GCD, put a GCD timer up for the character.
            if (request.Spell.AdhereToGlobalCooldown)
            {
                TimerRequest gcdTimer = new TimerRequest()
                {
                    SourceId = request.SourceId,
                    Kind = TimerKind.GCD,
                    ExpireAtTime = CurrentTime + SpellDatabase.GCD.Cooldown!.Value
                };
                timerRequests.Enqueue(gcdTimer);
            }
            
            //Handle CastType.
            switch (request.Spell.CastType)
            {
                case CastType.Instant:
                    break; //break the loop, you will get a resolution.
                case CastType.Channeled:
                    TimerRequest channelTimer = new TimerRequest()
                    {
                        SourceId = request.SourceId,
                        PendingSpellCast = new PendingSpellCast() { SpellEvent = request, OwnerId = request.SourceId.Id, SpellId = request.Spell.Id, WeaponView = weapon},
                        ExpireAtTime = CurrentTime + request.Spell.Duration!.Value,
                        Kind = TimerKind.Channel
                    };
                    timerRequests.Enqueue(channelTimer);

                    return new SpellCastResult() { Success = true };
                case CastType.Charged:
                    TimerRequest chargeTimer = new TimerRequest()
                    {
                        SourceId = request.SourceId,
                        PendingSpellCast = new PendingSpellCast() { SpellEvent = request, OwnerId = request.SourceId.Id, SpellId = request.Spell.Id, WeaponView = weapon },
                        ExpireAtTime = CurrentTime + request.Spell.Duration!.Value,
                        Kind = TimerKind.Charged
                    };
                    timerRequests.Enqueue(chargeTimer);
                    return new SpellCastResult() { Success = true };
            }
            SpellEffectResult results = ResolveEffects(request, weapon);
            if (!results.Success)
            {
                return new SpellCastResult() { Success = false, FailureReason = SpellFailReason.None };
            }

            if (request.Spell.Cooldown.HasValue && request.Spell.Cooldown != TimeSpan.Zero)
            {
                TimerRequest cooldownTimer = new TimerRequest()
                {
                    SourceId = request.SourceId,
                    Kind = TimerKind.SpellCooldown,
                    ExpireAtTime = CurrentTime + request.Spell.Cooldown!.Value,
                    PendingSpellCast = new PendingSpellCast() { OwnerId = request.SourceId.Id, SpellEvent = request, SpellId = request.Spell.Id, WeaponView = weapon },
                };
                timerRequests.Enqueue(cooldownTimer);
            }
            return new SpellCastResult() { Success = true, FailureReason = SpellFailReason.None, InstantCastResult = results };
        }

        private static SpellEffectResult ResolveEffects(SpellEvent request, WeaponView? weapon)
        {
            List<ResourceChange> changes = new List<ResourceChange>();
            foreach (SpellEffectDefinition effect in request.Spell.Effects)
            {
                switch (effect.EffectKind)
                {
                    case EffectKind.WeaponDamage:
                        int damage = SpellMath.CalculateWeaponDamage(weapon, request.SourceId.BaseStats.Strength, request.RandomSeed) * -1;
                        changes.Add(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value.Id, ResourceType = ResourceType.Health, Amount = damage });
                        break;
                    case EffectKind.TechDamage:
                        Character character = request.SourceId;
                        int techDamage = SpellMath.CalculateScaledValue(effect, character.BaseStats) * -1;
                        changes.Add(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value.Id, ResourceType = ResourceType.Health, Amount = techDamage });
                        break;
                    case EffectKind.AddResource:
                        Character sourceCharacter = request.SourceId;
                        int resourceAmount = SpellMath.CalculateScaledValue(effect, sourceCharacter.BaseStats);

                        if (effect.TargetKind != TargetKind.Self)
                        {
                            changes.Add(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value.Id, ResourceType = effect.ResourceType!.Value, Amount = resourceAmount });
                        }
                        else
                            changes.Add(new ResourceChange() { CharacterId = request.SourceId.Id, ResourceType = effect.ResourceType!.Value, Amount = resourceAmount });
                        break;
                }

            }
            return new SpellEffectResult() { Success = true, ResourceChanges = changes };
        }
        #endregion
        #region State Modification Functions
        public static bool CreateTestSpellEvent(SpellCastRequest request, SpellDefinition testSpell, out SpellEvent result)
        {
            result = new();
            Character source = Characters[request.SourceId];
            Character? actualTarget = null;
            WeaponView? weaponView = null;
            if (request.PrimaryTargetId != null)
            {
                bool test = Characters.TryGetValue(request.PrimaryTargetId.Value, out Character target);
                if (test)
                {
                    actualTarget = target;
                }
            }
            else
            { return false; }
            if (Weapons.TryGetValue(source.Id, out WeaponView weapon))
            {
                weaponView = weapon;
            }
            
            
            result = new SpellEvent() { SourceId = source, PrimaryTargetId = actualTarget, WeaponView = weaponView, RandomSeed = Random.Next(), Spell = testSpell};
            return true;
        }
        public static void InitializeGame()
        {
            if (SimulationTick == null)
            {
                SimulationTick = new ImpuritiesSimulationTick();
            }
             if (!SimulationTick.Initialized)
                SimulationTick.AdvanceTime();
        } //must be void
        public static void StartCycle()
        {
            HashSet<ActiveTimer> expired = ExpiredTimers(); //Find expired timers.
            ProcessExpiredTimers(expired);
            ProcessTimerRequests(timerRequests); //get timers out of queue and into the Hashset.
        } //must be void.
        public static IReadOnlyList<ExpiredTimer> ProcessExpiredTimers(HashSet<ActiveTimer> expired)
        {
            List<ExpiredTimer> expiredTimers = new List<ExpiredTimer>();
            foreach (ActiveTimer time in expired)
            {
                ExpiredTimer timer = new ExpiredTimer() { ActiveTimer = time };
                pendingSpellCasts.TryGetValue(time.OwnerId, out PendingSpellCast cast);

                timer.PendingSpellCast = cast;
                switch (timer.ActiveTimer.Key)
                {
                    case TimerKind.Channel:
                        SpellEffectResult channelResult = SpellCastResult(timer.PendingSpellCast.Value);
                        expiredTimers.Add(new ExpiredTimer() { ActiveTimer = timer.ActiveTimer, PendingSpellCast = timer.PendingSpellCast.Value });
                        pendingSpellCasts.Remove(time.OwnerId);
                        RequestResourceChange(channelResult);
                        break;
                    case TimerKind.Charged:

                        SpellEffectResult chargedResult = SpellCastResult(timer.PendingSpellCast.Value);
                        expiredTimers.Add(new ExpiredTimer() { ActiveTimer = timer.ActiveTimer, PendingSpellCast = timer.PendingSpellCast.Value });
                        pendingSpellCasts.Remove(time.OwnerId);
                        RequestResourceChange(chargedResult);
                        break;
                    default:
                        expiredTimers.Add(new ExpiredTimer() {ActiveTimer = timer.ActiveTimer });
                        break;
                }
                if (activeGcdOwners.Contains(time.OwnerId))
                {
                    activeGcdOwners.Remove(time.OwnerId);
                }
                if (time.SpellId.HasValue && activeSpellLocks.Contains((time.OwnerId,time.SpellId.Value)))
                {
                    activeSpellLocks.Remove((time.OwnerId, time.SpellId.Value));
                }
            }
            activeTimers.SymmetricExceptWith(expired); //Remove expired timers from active timers.
            return expiredTimers;
        } 
        public static void RequestResourceChange(SpellEffectResult result)
        {
            if (result.ResourceChanges == null || result.ResourceChanges.Count == 0)
            {
                return; // No resource changes to apply
            }
            for (int i = 0; i < result.ResourceChanges.Count; i++)
            {
                ResourceChange change = result.ResourceChanges[i];
                resourceChanges.Enqueue(change);
            }
        }
        public static void EndCycle()
        {
            if (TryUpdateResources(resourceChanges,ResourceStates, out Dictionary<Guid, Dictionary<ResourceType, ResourceState>> changedResources))
            {
                foreach (var kvp in changedResources)
                {
                    ResourceStates[kvp.Key] = kvp.Value;
                }
            }

            CurrentTime = SimulationTick!.AdvanceTime(); //last point in the cycle. Nothing comes after this.
        }
        
        public static HashSet<ActiveTimer> ExpiredTimers()
        {
            return activeTimers.Where(timer => timer.IsExpired(CurrentTime)).ToHashSet();
        }
        public static HashSet<ActiveTimer> GetActiveTimers() { return activeTimers; }
        private static void ProcessTimerRequests(Queue<TimerRequest> timerRequests)
        {
            while (timerRequests.Count > 0)
            {
                TimerRequest request = timerRequests.Dequeue();
                ActiveTimer newTimer = new ActiveTimer()
                {
                    OwnerId = request.SourceId.Id,
                    ExpireTime = request.ExpireAtTime,
                    Key = request.Kind,
                    SpellId = request.PendingSpellCast?.SpellId
                };
                activeTimers.Add(newTimer);

                if (request.PendingSpellCast.HasValue && request.PendingSpellCast.Value.SpellEvent.Spell.CastType != CastType.Instant)
                {
                    pendingSpellCasts.Add(request.SourceId.Id,request.PendingSpellCast.Value);
                }
                if (request.Kind == TimerKind.GCD)
                {
                    activeGcdOwners.Add(newTimer.OwnerId);
                }
                if (request.Kind == TimerKind.SpellCooldown)
                {
                    activeSpellLocks.Add((newTimer.OwnerId, newTimer.SpellId!.Value));
                }
            }
        }
        public static SpellEffectResult SpellCastResult(PendingSpellCast pendingSpellCast)
        {
            if (pendingSpellCast.SpellEvent.Spell.Cooldown.HasValue && pendingSpellCast.SpellEvent.Spell.Cooldown.Value != TimeSpan.Zero)
            {
                
                TimerRequest cooldownTimer = new TimerRequest()
                {
                    SourceId = pendingSpellCast.SpellEvent.SourceId,
                    Kind = TimerKind.SpellCooldown,
                    ExpireAtTime = CurrentTime + pendingSpellCast.SpellEvent.Spell.Cooldown.Value,
                    PendingSpellCast = pendingSpellCast
                };
                timerRequests.Enqueue(cooldownTimer);
            }
            return ResolveEffects(pendingSpellCast.SpellEvent, pendingSpellCast.WeaponView);
        }
        private static bool TryUpdateResources(Queue<ResourceChange> resourceChanges, Dictionary<Guid, Dictionary<ResourceType, ResourceState>> previousState, out Dictionary<Guid, Dictionary<ResourceType, ResourceState>> changedResources)
        {
            changedResources = null!;
            if (resourceChanges.Count == 0)
            {
                return false;
            }

            Dictionary<Guid, Dictionary<ResourceType, ResourceState>> updatedResources = new Dictionary<Guid, Dictionary<ResourceType, ResourceState>>();

            while (resourceChanges.Count > 0)
            {
                ResourceChange state = resourceChanges.Dequeue();
                //updatedResources.TryGetValue(state.CharacterId, out Dictionary<ResourceType, ResourceState>? resourceState);
                if (!updatedResources.TryGetValue(state.CharacterId, out Dictionary<ResourceType, ResourceState>? resourceState))
                {
                    resourceState = previousState[state.CharacterId];
                }

                ResourceState oldResourceState = resourceState[state.ResourceType];
                int newCurrent = oldResourceState.Current + state.Amount;
                newCurrent = Math.Max(0, Math.Min(oldResourceState.Maximum, newCurrent));
                ResourceState newResourceState = new ResourceState
                {
                    ResourceType = oldResourceState.ResourceType,
                    Current = newCurrent,
                    Maximum = oldResourceState.Maximum
                };

                resourceState[state.ResourceType] = newResourceState;

                Dictionary<ResourceType, ResourceState> newSortieState = resourceState;

                updatedResources[state.CharacterId] = newSortieState;
            }
            changedResources = updatedResources;

            
            return true;
        }

        public class ImpuritiesSimulationTick : ISimulationTimeAdvance
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
                return delta + GameImpurities.CurrentTime;
            }
        }
        #endregion
    }



    public readonly record struct SortieState
    {
        public Dictionary<ResourceType, ResourceState> Resources { get; init; }
    }
    public readonly record struct ActiveTimer
    {
        public Guid OwnerId { get; init; } //who this timer belongs to.
        public int? SpellId { get; init; }
        public TimeSpan ExpireTime { get; init; }
        public bool IsExpired(TimeSpan currentTime) { return currentTime >= ExpireTime; }
        public TimerKind? Key { get; init; }
    }
    public enum TimerKind
    {
        GCD,
        SpellCooldown,
        Channel,
        Charged
    }
    public sealed record ExpiredTimer
    {
        public PendingSpellCast? PendingSpellCast { get; set; }
        public ActiveTimer ActiveTimer { get; init; }
    }
    public readonly record struct TimerRequest
    {
        public Character SourceId { get; init; }
        public TimeSpan ExpireAtTime { get; init; }
        public TimerKind Kind { get; init; }
        public PendingSpellCast? PendingSpellCast { get; init; }
    }

    public readonly record struct TimerKey
    {
        public Guid OwnerId { get; init; }
        public TimerKind Kind { get; init; }
        public int? SpellId { get; init; }
    }

    public interface ISimulationTimeAdvance
    {
        public bool Initialized { get; }
        TimeSpan AdvanceTime();
    }
}
