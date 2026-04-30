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
        private static Queue<SpellCastIntent> incomingSpellCasts { get; set; } = new();
        private static Queue<ResourceChange> resourceChanges { get; set; } = new Queue<ResourceChange>();

        private static PriorityQueue<WakeTimer, TimeSpan> wakeTimers { get; set; } = new PriorityQueue<WakeTimer, TimeSpan>();
        private static HashSet<Guid> activeGcdOwners { get; set; } = new();
        private static HashSet<(Guid OwnerId, int SpellId)> activeSpellLocks { get; set; } = new();
        private static Dictionary<Guid, Dictionary<WakeTimerKind, WakeTimer>> wakeTimersByOwner { get; set; } = new Dictionary<Guid, Dictionary<WakeTimerKind, WakeTimer>>();
        private static Dictionary<Guid, AiMemorySet> AiMemory = new();
        public static Random Random { get; private set; } = new Random();
        #endregion

        #region Randomization Functions
        public static int GetRandomInt(int min, int max)
        {
            return Random.Next(min, max);
        }
        public static int GetRandomInt()
        {
            return Random.Next();
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

        public static int GetHP(Guid id)
        {
            if(ResourceStates.TryGetValue(id, out Dictionary<ResourceType,ResourceState>? resources))
            {
                if (resources.ContainsKey(ResourceType.Health))
                {
                    return resources[ResourceType.Health].Current;
                }
                
            }
            return -1;
        }
        #region GameLogic Functions


        public static bool InsertSpell(SpellCastIntent intent)
        {
            incomingSpellCasts.Enqueue(intent);
            return true;
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
        } //must be void
        public static void StartCycle()
        {

            //ProcessExpiredTimers(expired);
        } //must be void.
        private static void RequestResourceChange(SpellEffectResult result)
        {
            if (result.ResourceChanges != null)
            {
                foreach (ResourceChange change in result.ResourceChanges)
                {
                    resourceChanges.Enqueue(change);
                }
            }
        }
        private static void RequestResourceChange(Queue<SpellEffectResult> results)
        {
            while (results.Count > 0)
            {
                SpellEffectResult spellEffectResult = results.Dequeue();
                if (spellEffectResult.ResourceChanges == null)
                {
                    continue;
                }
                foreach (ResourceChange change in spellEffectResult.ResourceChanges)
                {
                    resourceChanges.Enqueue(change);
                }
            }
        }
        public static void EndCycle()
        {

            CurrentTime = SimulationTick!.AdvanceTime(); //last point in the cycle. Nothing comes after this.
        }
        public static HashSet<WakeTimer> ExpireWakeTimers()
        {
            HashSet<WakeTimer> expired = new();
            while (wakeTimers.TryPeek(out WakeTimer? timer, out TimeSpan expireTime))
            {
                if (expireTime > CurrentTime)
                {
                    break;
                }

                wakeTimers.Dequeue();

                if (timer.Cancelled)
                    continue; //simply drop timers that were cancelled.
                expired.Add(timer);
            }
            return expired;
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
        public static WakeTimer RequestWake(Guid ownerId, TimeSpan expireTime, WakeTimerKind kind)
        {
            var timer = new WakeTimer
            {
                OwnerId = ownerId,
                ExpireTime = expireTime,
                Key = kind
            };
            return timer;
        }
        private static bool EnqueueWakes(List<WakeTimer> wakes)
        {
            for (int i = 0; i < wakes.Count; i++)
            {
                WakeTimer wakeTimer = wakes[i];
                //if there is no one owning this timer, go ahead and add an entry.
                if (!wakeTimersByOwner.TryGetValue(wakeTimer.OwnerId, out Dictionary<WakeTimerKind, WakeTimer>? timers))
                {
                    wakeTimersByOwner.Add(wakeTimer.OwnerId, new Dictionary<WakeTimerKind, WakeTimer>());
                }
                if (timers.ContainsKey(wakeTimer.Key))
                {
                    continue; //drop enqueue. Timer already exists for that entity.
                }

                wakeTimers.Enqueue(wakeTimer, wakeTimer.ExpireTime);
                wakeTimersByOwner[wakeTimer.OwnerId][wakeTimer.Key] = wakeTimer;
            }
            return true;
        }
        public static bool TryCancelWake(Guid ownerId, WakeTimerKind kind, out WakeTimer? cancelledTimer)
        {
            cancelledTimer = null;
            if (!wakeTimersByOwner.TryGetValue(ownerId, out Dictionary<WakeTimerKind, WakeTimer>? ownerTimers))
            {
                return false;
            }

            if (!ownerTimers.TryGetValue(kind, out WakeTimer? wakeTimer))
            {
                return false;
            }

            wakeTimer.Cancelled = true;
            cancelledTimer = wakeTimer;

            ownerTimers.Remove(kind);

            if (ownerTimers.Count == 0)
            {
                wakeTimersByOwner.Remove(ownerId);
            }

            return true;
        }
        private static void AiThinkTime()
        {

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

    public sealed record WakeTimer
    {
        public Guid OwnerId { get; init; } //what entity created this.
        public TimeSpan ExpireTime { get; init; }
        public WakeTimerKind Key { get; init; }
        public bool IsExpired(TimeSpan currentTime) { return currentTime >= ExpireTime; }
        public bool Cancelled { get; set; }
    }
    public enum WakeTimerKind
    {
        AiThink,
        SpellGraphicFinish
    }
    public readonly record struct CancelWakeRequest
    {
        public Guid OwnerId { get; init; }
        public WakeTimer TimerToCancel {get; init;}
    }

    public interface ISimulationTimeAdvance
    {
        public bool Initialized { get; }
        TimeSpan AdvanceTime();
    }
}
