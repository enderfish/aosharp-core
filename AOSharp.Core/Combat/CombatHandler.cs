using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using AOSharp.Core.UI;

namespace AOSharp.Core.Combat
{
    public class CombatHandler
    {
        private float ACTION_TIMEOUT = 1f;
        private int MAX_CONCURRENT_PERKS = 3;
        protected Queue<CombatActionQueueItem> _actionQueue = new Queue<CombatActionQueueItem>();
        private List<ItemRule> _itemRules = new List<ItemRule>();
        private List<ScalingItemRule> _scalingItemRules = new List<ScalingItemRule>();
        private List<PerkRule> _perkRules = new List<PerkRule>();
        private List<SpellRule> _spellRules = new List<SpellRule>();

        protected delegate bool ItemConditionProcessor(Item item, SimpleChar fightingTarget, ref (SimpleChar Target, bool ShouldSetTarget) actionTarget);
        protected delegate bool PerkConditionProcessor(PerkAction perkAction, SimpleChar fightingTarget, ref (SimpleChar Target, bool ShouldSetTarget) actionTarget);
        protected delegate bool SpellConditionProcessor(Spell spell, SimpleChar fightingTarget, ref (SimpleChar Target, bool ShouldSetTarget) actionTarget);

        public static CombatHandler Instance { get; private set; }

        protected virtual bool ShouldUseSpecialAttack(SpecialAttack specialAttack)
        {
            return true;
        }

        public static void Set(CombatHandler combatHandler)
        {
            Instance = combatHandler;
        }

        internal void Update(float deltaTime)
        {
            if (!DynelManager.LocalPlayer.IsAlive)
                return;

            //try
            //{
                OnUpdate(deltaTime);
            //}
            //catch (Exception e) 
            //{
            //    Chat.WriteLine($"This shouldn't happen pls report (CombatHandler): {e.Message}");
            //}
        }

        protected virtual void OnUpdate(float deltaTime)
        {
            SimpleChar fightingTarget = DynelManager.LocalPlayer.FightingTarget;

            if (fightingTarget != null)
                PerformSpecialAttacks(fightingTarget);

            foreach (var scalingItemRule in _scalingItemRules.OrderBy(p => (int)p.Priority))
            {
                //Find highest usable ql of the item
                Item item;
                if ((item = Inventory.Inventory.FindAll(scalingItemRule.Ids)
                    .OrderByDescending(x => x.QualityLevel)
                    .FirstOrDefault(x => x.MeetsSelfUseReqs())) == null)
                    continue;

                //Ignore the item if it's already queued
                if (_actionQueue.Any(x => x.CombatAction is Item action && action == item))
                    continue;

                (SimpleChar Target, bool ShouldSetTarget) actionTarget = (fightingTarget, false);

                if (scalingItemRule.ItemConditionProcessor != null && scalingItemRule.ItemConditionProcessor.Invoke(item, fightingTarget, ref actionTarget))
                {
                    if (!item.MeetsUseReqs(actionTarget.Target))
                        continue;

                    if (actionTarget.Target != null && !item.IsInRange(actionTarget.Target))
                        continue;

                    //Chat.WriteLine($"Queueing item {item.Name} -- actionQ.Count = {_actionQueue.Count}");
                    float queueOffset = _actionQueue.Where(x => x.CombatAction is PerkAction).Sum(x => ((PerkAction)x.CombatAction).AttackDelay);
                    double timeoutOffset = item.AttackDelay + ACTION_TIMEOUT + queueOffset;
                    _actionQueue.Enqueue(new CombatActionQueueItem(item, actionTarget.Target, actionTarget.ShouldSetTarget, timeoutOffset));
                }
            }

            foreach (var itemRule in _itemRules.OrderBy(p => (int)p.Priority))
            {
                //Find highest usable ql of the item
                Item item;
                if ((item = Inventory.Inventory.FindAll(itemRule.LowId, itemRule.HighId)
                    .OrderByDescending(x => x.QualityLevel).FirstOrDefault(x => x.MeetsSelfUseReqs())) == null)
                    continue;

                //Ignore the item if it's already queued
                if (_actionQueue.Any(x => x.CombatAction is Item action && action == item))
                    continue;

                (SimpleChar Target, bool ShouldSetTarget) actionTarget = (fightingTarget, false);

                if (itemRule.ItemConditionProcessor != null && itemRule.ItemConditionProcessor.Invoke(item, fightingTarget, ref actionTarget))
                {
                    if (!item.MeetsUseReqs(actionTarget.Target))
                        continue;

                    if (actionTarget.Target != null && !item.IsInRange(actionTarget.Target))
                        continue;

                    //Chat.WriteLine($"Queueing item {item.Name} -- actionQ.Count = {_actionQueue.Count}");
                    float queueOffset = _actionQueue.Where(x => x.CombatAction is PerkAction).Sum(x => ((PerkAction)x.CombatAction).AttackDelay);
                    double timeoutOffset = item.AttackDelay + ACTION_TIMEOUT + queueOffset;
                    _actionQueue.Enqueue(new CombatActionQueueItem(item, actionTarget.Target, actionTarget.ShouldSetTarget, timeoutOffset));
                }
            }

            //Only queue perks if we have no items awaiting usage and aren't over max concurrent perks
            if (!_actionQueue.Any(x => x.CombatAction is Item))
            {
                foreach (var perkRule in _perkRules.OrderBy(p => (int)p.Priority))
                {
                    if (_actionQueue.Count(x => x.CombatAction is PerkAction) >= MAX_CONCURRENT_PERKS)
                        break;

                    if (!PerkAction.Find(perkRule.PerkHash, out PerkAction perk))
                        continue;

                    if (perk.IsPending || perk.IsExecuting || !perk.IsAvailable)
                        continue;

                    if (_actionQueue.Any(x => x.CombatAction is PerkAction action && action == perk))
                        continue;

                    (SimpleChar Target, bool ShouldSetTarget) actionTarget = (fightingTarget, false);

                    if (perkRule.PerkConditionProcessor != null && perkRule.PerkConditionProcessor.Invoke(perk, fightingTarget, ref actionTarget))
                    {
                        if (!perk.MeetsUseReqs(actionTarget.Target))
                            continue;

                        if (actionTarget.Target != null && !perk.IsInRange(actionTarget.Target))
                            continue;

                        //Chat.WriteLine($"Queueing perk {perk.Name} -- actionQ.Count = {_actionQueue.Count}");
                        _actionQueue.Enqueue(new CombatActionQueueItem(perk, actionTarget.Target, actionTarget.ShouldSetTarget));
                    }
                }
            }

            if (!Spell.HasPendingCast && DynelManager.LocalPlayer.MovementStatePermitsCasting)
            {
                foreach (var spellRule in _spellRules.OrderBy(s => (int)s.Priority))
                {
                    Spell spell = null;

                    foreach (int spellId in spellRule.SpellGroup)
                    {
                        if (!Spell.Find(spellId, out Spell curSpell))
                            continue;

                        if (!curSpell.MeetsSelfUseReqs())
                            continue;

                        spell = curSpell;
                        break;
                    }

                    if (spell == null)
                        continue;

                    if (!spell.IsReady)
                        continue;

                    (SimpleChar Target, bool ShouldSetTarget) actionTarget = (fightingTarget, true);

                    if (spellRule.SpellConditionProcessor != null && spellRule.SpellConditionProcessor.Invoke(spell, fightingTarget, ref actionTarget))
                    {
                        if (!spell.MeetsUseReqs(actionTarget.Target))
                            continue;

                        spell.Cast(actionTarget.Target, actionTarget.ShouldSetTarget);
                        break;
                    }
                }
            }

            if (_actionQueue.Count > 0)
            {
                //Drop any expired items
                while (_actionQueue.Count > 0 && _actionQueue.Peek().Timeout <= Time.NormalTime)
                    _actionQueue.Dequeue();

                List<CombatActionQueueItem> dequeueList = new List<CombatActionQueueItem>();

                foreach (CombatActionQueueItem actionItem in _actionQueue)
                {
                    if (actionItem.Used)
                        continue;

                    if (actionItem.CombatAction is Item item)
                    {
                        if (Item.HasPendingUse)
                            continue;

                        if (_actionQueue.Any(x => x.CombatAction is PerkAction))
                            continue;

                        //I have no real way of checking if a use action is valid so we'll just send it off and pray
                        item.Use(actionItem.Target, actionItem.ShouldSetTarget);
                        actionItem.Used = true;
                        actionItem.Timeout = Time.NormalTime + ACTION_TIMEOUT;
                    }
                    else if (actionItem.CombatAction is PerkAction perk)
                    {
                        if (!perk.Use(actionItem.Target, actionItem.ShouldSetTarget))
                        {
                            dequeueList.Add(actionItem);
                            continue;
                        }

                        actionItem.Used = true;
                        actionItem.Timeout = Time.NormalTime + ACTION_TIMEOUT;
                    }
                }

                //Drop any failed actions
                _actionQueue = new Queue<CombatActionQueueItem>(_actionQueue.Where(x => !dequeueList.Contains(x)));
            }
        }

        protected virtual void PerformSpecialAttacks(SimpleChar target)
        {
            foreach (SpecialAttack special in DynelManager.LocalPlayer.SpecialAttacks)
            {
                if (!ShouldUseSpecialAttack(special))
                    continue;

                if ((special == SpecialAttack.AimedShot || special == SpecialAttack.SneakAttack) && target.IsNpc)
                    continue;

                if (!special.IsAvailable())
                    continue;

                if (!special.IsInRange(target))
                    continue;

                if (special == SpecialAttack.Backstab && (!target.IsAttacking || target.FightingTarget.Identity == DynelManager.LocalPlayer.Identity || target.IsFacing(DynelManager.LocalPlayer)))
                    continue;

                special.UseOn(target);
            }
        }

        protected bool HasPerkProcessor(PerkHash perkHash)
        {
            return _perkRules.Any(rule => rule.PerkHash == perkHash);
        }
        
        protected void RegisterItemProcessor(int lowId, int highId, ItemConditionProcessor conditionProcessor, CombatActionPriority priority = CombatActionPriority.Medium)
        {
            _itemRules.Add(new ItemRule(lowId, highId, conditionProcessor, priority));
        }

        protected void RegisterItemProcessor(IEnumerable<int> ids, ItemConditionProcessor conditionProcessor, CombatActionPriority priority = CombatActionPriority.Medium)
        {
            _scalingItemRules.Add(new ScalingItemRule(ids, conditionProcessor, priority));
        }

        protected void RegisterPerkProcessor(PerkHash perkHash, PerkConditionProcessor conditionProcessor, CombatActionPriority priority = CombatActionPriority.Medium)
        {
            _perkRules.Add(new PerkRule(perkHash, conditionProcessor, priority));
        }

        protected void RegisterSpellProcessor(Spell spell, SpellConditionProcessor conditionProcessor, CombatActionPriority priority = CombatActionPriority.Medium)
        {
            RegisterSpellProcessor(new[] { spell.Id }, conditionProcessor, priority);
        }

        protected void RegisterSpellProcessor(IEnumerable<Spell> spellGroup, SpellConditionProcessor conditionProcessor, CombatActionPriority priority = CombatActionPriority.Medium)
        {
            RegisterSpellProcessor(spellGroup.GetIds(), conditionProcessor, priority);
        }

        protected void RegisterSpellProcessor(int spellId, SpellConditionProcessor conditionProcessor, CombatActionPriority priority = CombatActionPriority.Medium)
        {
            RegisterSpellProcessor(new[] { spellId }, conditionProcessor, priority);
        }

        protected void RegisterSpellProcessor(int[] spellGroup, SpellConditionProcessor conditionProcessor, CombatActionPriority priority = CombatActionPriority.Medium)
        {
            if (spellGroup.Length == 0)
                return;

            _spellRules.Add(new SpellRule(spellGroup, conditionProcessor, priority));
        }

        internal void OnItemUsed(int lowId, int highId, int ql)
        {
            //Drop the queued action
            _actionQueue = new Queue<CombatActionQueueItem>(_actionQueue.Where(x => !(x.CombatAction is Item action && action.Id == lowId && action.HighId == highId && action.QualityLevel == ql)));
        }

        internal void OnUsingItem(Item item, double timeout)
        {
            CombatActionQueueItem queueItem;
            if ((queueItem = _actionQueue.FirstOrDefault(x => x.CombatAction is Item && (Item)x.CombatAction == item)) == null)
                return;

            queueItem.Timeout = timeout;
        }

        internal void OnPerkExecuted(DummyItem perkDummyItem)
        {
            //Drop the queued action
            _actionQueue = new Queue<CombatActionQueueItem>(_actionQueue.Where(x => !(x.CombatAction is PerkAction action && action.Name == perkDummyItem.Name)));
        }

        internal void OnPerkLanded(PerkAction perkAction, double timeout)
        {
            //Update the queued perk's timeout to match the internal perk queue's
            foreach(CombatActionQueueItem queueItem in _actionQueue)
            {
                if (!(queueItem.CombatAction is PerkAction))
                    return;

                if ((PerkAction)queueItem.CombatAction == perkAction)
                {
                    //Chat.WriteLine($"Perk {perkAction.Name} landed. Time: {Time.NormalTime}\tOldTimeout: {queueItem.Timeout}\tNewTimeout: {timeout}");
                    queueItem.Timeout = timeout;
                }
            }
        }

        protected class CombatActionQueueItem : IEquatable<CombatActionQueueItem>
        {
            public ICombatAction CombatAction;
            public SimpleChar Target;
            public bool Used = false;
            public bool ShouldSetTarget = false;
            public double Timeout = 0;

            public CombatActionQueueItem(ICombatAction action, SimpleChar target, bool shouldSetTarget, double timeoutOffset = 1)
            {
                CombatAction = action;
                Target = target;
                ShouldSetTarget = shouldSetTarget;
                Timeout = Time.NormalTime + timeoutOffset;
            }

            public bool Equals(CombatActionQueueItem other)
            {
                if (other == null)
                    return false;

                if (CombatAction.GetType() != other.CombatAction.GetType())
                    return false;

                switch (CombatAction)
                {
                    case PerkAction perk:
                        return perk == ((PerkAction)other.CombatAction);
                    case Item item:
                        Item item1 = item;
                        Item item2 = (Item)other.CombatAction;
                        return item.Id == ((Item)other.CombatAction).Id && item.HighId == ((Item)other.CombatAction).HighId && item.QualityLevel == ((Item)other.CombatAction).QualityLevel;
                    case Spell spell:
                        return spell == ((Spell)other.CombatAction);
                    default:
                        return false;
                }
            }
        }

        protected enum CombatActionType
        {
            Damage,
            Heal,
            Buff
        }

        protected enum CombatActionPriority
        {
            High = 10,
            Medium = 20,
            Low = 30
        }

        protected readonly struct ItemRule
        {
            public int LowId { get; }
            public int HighId { get; }
            public ItemConditionProcessor ItemConditionProcessor { get; }
            public CombatActionPriority Priority { get; }

            public ItemRule(int lowId, int highId, ItemConditionProcessor itemConditionProcessor,
                CombatActionPriority combatActionPriority)
            {
                LowId = lowId;
                HighId = highId;
                ItemConditionProcessor = itemConditionProcessor;
                Priority = combatActionPriority;
            }
        }

        protected readonly struct ScalingItemRule
        {
            public IEnumerable<int> Ids { get; }

            public ItemConditionProcessor ItemConditionProcessor { get; }
            public CombatActionPriority Priority { get; }

            public ScalingItemRule(IEnumerable<int> ids, ItemConditionProcessor itemConditionProcessor,
                CombatActionPriority combatActionPriority)
            {
                Ids = ids;
                ItemConditionProcessor = itemConditionProcessor;
                Priority = combatActionPriority;
            }
        }

        protected readonly struct SpellRule
        {
            public int[] SpellGroup { get;  }
            public SpellConditionProcessor SpellConditionProcessor { get; }
            public CombatActionPriority Priority { get;  }

            public SpellRule(int[] spellGroup, SpellConditionProcessor spellConditionProcessor,
                CombatActionPriority combatActionPriority)
            {
                SpellGroup = spellGroup;
                SpellConditionProcessor = spellConditionProcessor;
                Priority = combatActionPriority;
            }
        }

        protected readonly struct PerkRule
        {
            public PerkHash PerkHash { get; }
            public PerkConditionProcessor PerkConditionProcessor { get;  }
            public CombatActionPriority Priority { get; }

            public PerkRule(PerkHash perkHash, PerkConditionProcessor perkConditionProcessor,
                CombatActionPriority combatActionPriority)
            {
                PerkHash = perkHash;
                PerkConditionProcessor = perkConditionProcessor;
                Priority = combatActionPriority;
            }
        }
    }
}
