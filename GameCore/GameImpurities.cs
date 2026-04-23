using System;
using System.Collections.Generic;
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
        private static TimeSpan CurrentTime { get; set; } = TimeSpan.Zero; //the current time of the sortie according to the simulation clock. This is the definitive Time GameImpurities uses. SimulationClock only advances time.
        public static ISimulationTimeAdvance? SimulationTick;
        public static Dictionary<Guid, Character> Characters { get; private set; } = new Dictionary<Guid, Character>(); //characters in the sortie, indexed by their unique ID. This is all characters.
        public static Dictionary<Guid, WeaponView> Weapons { get; private set; } = new Dictionary<Guid, WeaponView>(); //weapons from characters.
        public static Dictionary<Guid, SortieState> SortieStates { get; private set; } = new Dictionary<Guid, SortieState>();
        private static List<PendingSpellCast> pendingSpellCasts = new List<PendingSpellCast>();
        private static Queue<ResourceChange> resourceChanges { get; set; } = new Queue<ResourceChange>();
        private static Queue<TimerRequest> timerRequests { get; set; } = new Queue<TimerRequest>();
        private static HashSet<ActiveTimer> activeTimers { get; set; } = new HashSet<ActiveTimer>();
        public static Random Random { get; private set; } = new Random();
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
            SortieState target = SortieStates[request.PrimaryTargetId.Value.Id];
            if (!target.Resources.TryGetValue(ResourceType.Health, out ResourceState targetHealth))
            {
                return new SpellCastResult() { Success = false, FailureReason = SpellFailReason.InvalidTarget }; //its... cant hit it.
            }
            /*
             * Validate timers. 
             * If there is a timer with the GUID of the character and the spell at all, fail. Spell is either on cooldown, or is being cast.
             * If there is a timer with the GCD for the character, fail. Character is on GCD.
             * Else timers are valid.
             */
            bool hasSpellTimer = activeTimers.Any(timer =>
                timer.OwnerId == request.SourceId.Id &&
                timer.SpellId == request.Spell.Id);

            bool hasGcdTimer = activeTimers.Any(timer =>
                timer.OwnerId == request.SourceId.Id &&
                timer.Key == TimerKind.GCD);
            if (hasSpellTimer || hasGcdTimer)
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
            SpellCastResult results = ResolveEffects(request, weapon);
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
            return ResolveEffects(request, weapon);
        }

        private static SpellCastResult ResolveEffects(SpellEvent request, WeaponView? weapon)
        {
            List<ResourceChange> changes = new List<ResourceChange>();
            foreach (SpellEffectDefinition effect in request.Spell.Effects)
            {
                switch (effect.EffectKind)
                {
                    case EffectKind.WeaponDamage:
                        int damage = SpellMath.CalculateWeaponDamage(weapon, Characters[request.SourceId.Id].BaseStats.Strength, request.RandomSeed) * -1;
                        changes.Add(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value.Id, ResourceType = ResourceType.Health, Amount = damage });
                        break;
                    case EffectKind.TechDamage:
                        Character character = Characters[request.SourceId.Id];
                        int techDamage = SpellMath.CalculateScaledValue(effect, character.BaseStats) * -1;
                        changes.Add(new ResourceChange() { CharacterId = request.PrimaryTargetId.Value.Id, ResourceType = ResourceType.Health, Amount = techDamage });
                        break;
                    case EffectKind.AddResource:
                        Character sourceCharacter = Characters[request.SourceId.Id];
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
            return new SpellCastResult() { Success = true, ResourceChanges = changes };
        }
        #endregion
        #region State Modification Functions

        public static void InitializeGame()
        {
            if (SimulationTick == null)
            {
                SimulationTick = new ImpuritiesSimulationTick();
            }
             if (!SimulationTick.Initialized)
                SimulationTick.AdvanceTime();
        }
        public static void StartCycle()
        {
            HashSet<ActiveTimer> expired = ExpiredTimers().ToHashSet(); //Find expired timers.
            activeTimers.SymmetricExceptWith(expired); //Remove expired timers from active timers.
            HandleAllExpiredTimers(expired);
            ProcessTimerRequests(); //get timers out of queue and into the Hashset.
        }
        private static void HandleAllExpiredTimers(HashSet<ActiveTimer> expired)
        {
            foreach (ActiveTimer timer in expired)
            {
                PendingSpellCast? pendingCast = pendingSpellCasts
                .FirstOrDefault(p =>
                    p.SpellEvent.SourceId.Id == timer.OwnerId &&
                    p.SpellEvent.Spell.Id == timer.SpellId);

                switch (timer.Key)
                {
                    case TimerKind.Channel:
                        if (pendingCast.HasValue )
                        {
                            SpellCastResult channelResult = SpellCastResult(pendingCast.Value);
                            RequestResourceChange(channelResult);
                        }
                        break;
                    case TimerKind.Charged:
                        if (pendingCast.HasValue)
                        {
                            SpellCastResult chargedResult = SpellCastResult(pendingCast.Value);
                            RequestResourceChange(chargedResult);

                        }
                        break;
                    default:
                        break;
                }
            }
        }
        public static void RequestResourceChange(SpellCastResult result)
        {
            if (result.ResourceChanges == null || result.ResourceChanges.Count == 0)
            {
                return; // No resource changes to apply
            }
            List<ResourceChange> changes = result.ResourceChanges.ToList();
            for (int i = 0; i < changes.Count; i++)
            {
                ResourceChange change = changes[i];
                resourceChanges.Enqueue(change);
            }
        }
        public static void EndCycle()
        {
            UpdateResources();

            CurrentTime = SimulationTick!.AdvanceTime(); //last point in the cycle. Nothing comes after this.
        }
        
        public static IReadOnlySet<ActiveTimer> ExpiredTimers()
        {
            return activeTimers.Where(timer => timer.IsExpired(CurrentTime)).ToHashSet();
        }
        public static HashSet<ActiveTimer> GetActiveTimers() { return activeTimers; }
        private static void ProcessTimerRequests()
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

                if (request.PendingSpellCast.HasValue)
                {
                    pendingSpellCasts.Add(request.PendingSpellCast.Value);
                }
            }
        }
        public static SpellCastResult SpellCastResult(PendingSpellCast pendingSpellCast)
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
        private static void UpdateResources()
        {

            if (resourceChanges.Count == 0)
            {
                return;
            }

            Dictionary<Guid, SortieState> updatedResources = new Dictionary<Guid, SortieState>();

            while (resourceChanges.Count > 0)
            {
                ResourceChange state = resourceChanges.Dequeue();

                if (!updatedResources.TryGetValue(state.CharacterId, out SortieState resourceState))
                {
                    resourceState = SortieStates[state.CharacterId];
                }

                Dictionary<ResourceType, ResourceState> newResources =
                    resourceState.Resources.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                ResourceState oldResourceState = newResources[state.ResourceType];

                ResourceState newResourceState = new ResourceState
                {
                    Current = oldResourceState.Current + state.Amount,
                    Maximum = oldResourceState.Maximum
                };

                newResources[state.ResourceType] = newResourceState;

                SortieState newSortieState = resourceState with
                {
                    Resources = newResources
                };

                updatedResources[state.CharacterId] = newSortieState;
            }

            foreach (var kvp in updatedResources)
            {
                SortieStates[kvp.Key] = kvp.Value;
            }
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
                TimeSpan delta = GameImpurities.CurrentTime - currentElapsed;
                _previousElapsed = currentElapsed;
                return delta + GameImpurities.CurrentTime;
            }
        }
        #endregion
    }

    public readonly record struct SortieState
    {
        public IReadOnlyDictionary<ResourceType, ResourceState> Resources { get; init; }
    }
    public sealed record ActiveTimer
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
    public readonly record struct PendingSpellCast
    {
        public Guid OwnerId { get; init; }
        public int SpellId { get; init; }
        public SpellEvent SpellEvent { get; init; }
        public WeaponView? WeaponView { get; init; }
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
