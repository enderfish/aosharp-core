using System;
using System.Runtime.InteropServices;

namespace AOSharp.Core
{
    [StructLayout(LayoutKind.Explicit, Pack = 0)]
    public struct WeaponHolder
    {
        //2 if attacking otherwise 1
        [FieldOffset(0x44)]
        public byte AttackingState;
    }

    public unsafe class WeaponHolderInfo
    {
        private readonly IntPtr _ptr;

        internal WeaponHolderInfo(IntPtr ptr) { _ptr = ptr; }

        public bool WeaponsAreBusy
        {
            get
            {
                if (_ptr == IntPtr.Zero) return false;
                return ((WeaponHolder*)_ptr)->AttackingState == 0x02;
            }
        }
    }
}
