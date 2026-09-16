using AOSharp.Common.GameData;
using AOSharp.Common.Helpers;
using AOSharp.Common.Unmanaged.DataTypes;
using AOSharp.Common.Unmanaged.Imports;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using AOSharp.Core.Inventory;
using AOSharp.Core.UI;
using System.Collections.Generic;
using System.Reflection;

namespace AOSharp.Core
{
    public class DummyItem
    {
        public readonly string Name;
        public int Id;
        public readonly IntPtr Pointer;

        public float AttackDelay => GetStat(Stat.AttackDelay) / 100;

        public virtual float AttackRange => GetStat(Stat.AttackRange);
        public virtual CanFlags CanFlags => (CanFlags)GetStat(Stat.Can);

        public List<SpellData> UseModifiers => GetSpellList(SpellListType.Use);
        public List<SpellData> WearModifiers => GetSpellList(SpellListType.Wear);

        /*
        internal unsafe DummyItem(int lowId, int highId, int ql)
        {
            Identity none = Identity.None;
            IntPtr pEngine = N3Engine_t.GetInstance();

            if (!CreateDummyItemID(lowId, highId, ql, out Identity dummyItemId))
                throw new Exception($"Failed to create dummy item. LowId: {lowId}\tLowId: {highId}\tLowId: {ql}");

            IntPtr pItem = N3EngineClientAnarchy_t.GetItemByTemplate(pEngine, dummyItemId, ref none);

            if (pItem == IntPtr.Zero)
                throw new Exception($"DummyItem::DummyItem - Unable to locate item. LowId: {lowId}\tLowId: {highId}\tLowId: {ql}");

            Pointer = pItem;
            Identity = (*(MemStruct*)pItem).Identity;
            Name = Utils.UnsafePointerToString((*(MemStruct*)pItem).Name);
        }
        */

        /*
        internal unsafe DummyItem(Identity identity) : this
        {
            Pointer = pItem;
            Identity = identity;
            Name = Utils.UnsafePointerToString((*(MemStruct*)pItem).Name);
        }*/

        internal unsafe DummyItem(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
                throw new Exception($"DummyItem::DummyItem - Unable to load DummyItem");

            Pointer = pointer;
            Id = GetStat(Stat.StaticInstance);
            Name = Utils.UnsafePointerToString((*(MemStruct*)pointer).Name);
        }

        public static bool TryGet<T>(Identity identity, out T dummyItem) where T : DummyItem
        {
            dummyItem = null;

            IntPtr pItem = GetPtr(identity);

            if (pItem == IntPtr.Zero)
                return false;

            dummyItem = Activator.CreateInstance(typeof(T), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { pItem }, null) as T;
            return dummyItem != null;
        }

        public static bool TryGet(int lowId, int highId, int ql, out ACGItem dummyItem)
        {
            dummyItem = null;

            IntPtr pItem = GetPtr(lowId, highId, ql);

            if (pItem == IntPtr.Zero)
                return false;

            dummyItem = new ACGItem(pItem);
            return true;
        }

        public static bool CreateDummyItemID(int lowId, int highId, int ql, out Identity dummyItemId)
        {
            ACGItemQueryData queryData = new ACGItemQueryData
            {
                LowId = lowId,
                HighId = highId,
                QL = ql
            };

            IntPtr pEngine = N3Engine_t.GetInstance();

            Identity templateId = Identity.None;
            bool result =  N3EngineClientAnarchy_t.CreateDummyItemID(pEngine, ref templateId, ref queryData);
            dummyItemId = templateId;
            return result;
        }

        protected static IntPtr GetPtr(Identity identity)
        {
            Identity none = Identity.None;
            return N3EngineClientAnarchy_t.GetItemByTemplate(N3Engine_t.GetInstance(), identity, ref none);
        }

        protected static IntPtr GetPtr(int lowId, int highId, int ql)
        {
            if (!CreateDummyItemID(lowId, highId, ql, out Identity dummyItemId))
                return IntPtr.Zero;

            return GetPtr(dummyItemId);
        }

        public bool MeetsSelfUseReqs()
        {
            return MeetsUseReqs(ignoreTargetReqs: true);
        }

        public bool MeetsUseReqs(SimpleChar target = null, bool ignoreTargetReqs = false)
        {
            if (N3Engine_t.GetInstance() == IntPtr.Zero)
                return false;

            List<RequirementCriterion> criteria = GetUseRequirements();

            //Should I return true or false here? hmm.
            if (criteria == null)
                return true;

            ReqChecker reqChecker = new ReqChecker(criteria);

            return reqChecker.MeetsReqs(target, ignoreTargetReqs);
        }

        /// <summary>
        /// Returns the raw use-criteria of this item/nano in the client's postfix order
        /// (stat checks interleaved with And/Or/Not and OnUser/OnTarget scope switches),
        /// or null when the item has no use criteria.
        /// </summary>
        public unsafe List<RequirementCriterion> GetUseRequirements()
        {
            IntPtr pCriteria = N3EngineClientAnarchy_t.GetItemActionInfo(Pointer, ItemActionInfo.UseCriteria);

            if (pCriteria == IntPtr.Zero)
                return null;

            List<RequirementCriterion> criteria = new List<RequirementCriterion>();

            foreach (IntPtr pReq in ((StdStructVector*)(pCriteria + 0x4))->ToList(0xC))
            {
                criteria.Add(new RequirementCriterion
                {
                    Param1 = *(int*)(pReq),
                    Param2 = *(int*)(pReq + 0x4),
                    Operator = *(UseCriteriaOperator*)(pReq + 0x8)
                });
            }

            return criteria;
        }

        public int GetStat(Stat stat, int detail = 2)
        {
            return DummyItem_t.GetStat(Pointer, stat, detail);
        }

        public virtual bool IsInRange(SimpleChar target)
        {
            return DynelManager.LocalPlayer.GetLogicalRangeToTarget(target) < AttackRange;
        }

        private unsafe List<SpellData> GetSpellList(SpellListType spellListType)
        {
            List<SpellData> spellList = new List<SpellData>();

            IntPtr pSpellList = DummyItem_t.GetSpellList(Pointer, spellListType);

            if (pSpellList == IntPtr.Zero)
                return spellList;

            int numSpells = *(int*)(pSpellList + 0x10);

            if (numSpells == 0)
                return spellList;

            DummyItem_t.GetSpellDataUnkStruct spellDataUnkStruct = new DummyItem_t.GetSpellDataUnkStruct();
            DummyItem_t.GetSpellDataUnk(pSpellList, ref spellDataUnkStruct);

            for(int i = 0; i < numSpells; i++)
            {
                spellDataUnkStruct.Idx = i;
                IntPtr pSpellDataContainer = *(IntPtr*)DummyItem_t.GetSpellData(ref spellDataUnkStruct);
                
                SpellDataMemStruct spellData = **(SpellDataMemStruct**)(pSpellDataContainer + 0x20);

                spellList.Add(SpellData.New(spellData.Function, spellData.Properties.ToList<SpellProperty>().ToDictionary(x => x.Operator, x => x.Value)));
            }

            return spellList;
        }

        internal IntPtr GetItemActionInfo(ItemActionInfo itemActionInfo) => N3EngineClientAnarchy_t.GetItemActionInfo(Pointer, itemActionInfo);

        [StructLayout(LayoutKind.Explicit, Pack = 0)]
        private struct MemStruct
        {
            [FieldOffset(0x14)]
            public Identity Identity;

            [FieldOffset(0x9C)]
            public IntPtr Name;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 0)]
        private struct SpellDataMemStruct
        {
            [FieldOffset(0xC)]
            public SpellFunction Function;

            [FieldOffset(0x18)]
            public StdStructVector Properties;
        }

        private enum CriteriaSource
        {
            FightingTarget,
            Target,
            Self,
            User
        }
    }
}
