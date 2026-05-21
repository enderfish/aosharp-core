using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOSharp.Core.GameData;
using AOSharp.Common.Unmanaged.Imports;
using AOSharp.Common.GameData;

namespace AOSharp.Core
{
    public unsafe class WeaponItem : SimpleItem
    {
        public float AttackRange => GetStat(Stat.AttackRange);

        public int Ammo => -1;
        public int MaxAmmo => 0;
        public void Reload() { }

        public readonly HashSet<SpecialAttack> SpecialAttacks;

        private readonly IntPtr _pWeaponHolder;
        private readonly IntPtr _pWeaponUnk;

        internal WeaponItem(IntPtr pointer, IntPtr pWeaponHolder, IntPtr pWeaponUnk) : base(pointer)
        {
            _pWeaponHolder = pWeaponHolder;
            _pWeaponUnk = pWeaponUnk;
            SpecialAttacks = GetSpecialAttacks();
        }

        internal WeaponItem(Dynel dynel) : base(dynel.Pointer)
        {
        }

        private HashSet<SpecialAttack> GetSpecialAttacks()
        {
            HashSet<SpecialAttack> specials = new HashSet<SpecialAttack>();
            CanFlags canFlags = (CanFlags)GetStat(Stat.Can);

            if (canFlags.HasFlag(CanFlags.AimedShot))
                specials.Add(SpecialAttack.AimedShot);

            if (canFlags.HasFlag(CanFlags.Brawl))
                specials.Add(SpecialAttack.Brawl);

            if (canFlags.HasFlag(CanFlags.Burst))
                specials.Add(SpecialAttack.Burst);

            if (canFlags.HasFlag(CanFlags.Dimach))
                specials.Add(SpecialAttack.Dimach);

            if (canFlags.HasFlag(CanFlags.FastAttack))
                specials.Add(SpecialAttack.FastAttack);

            if (canFlags.HasFlag(CanFlags.FlingShot))
                specials.Add(SpecialAttack.FlingShot);

            if (canFlags.HasFlag(CanFlags.FullAuto))
                specials.Add(SpecialAttack.FullAuto);

            if (canFlags.HasFlag(CanFlags.SneakAttack))
                specials.Add(SpecialAttack.SneakAttack);

            return specials;
        }

        public bool IsDynelInRange(Dynel target)
        {
            return WeaponHolder_t.IsDynelInWeaponRange(_pWeaponHolder, _pWeaponUnk, target.Pointer) == 0x01;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 0)]
        private new struct MemStruct
        {

        }
    }
}
