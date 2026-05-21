using System;
using System.Collections.Concurrent;
using AOSharp.Common.GameData;
using AOSharp.Common.Unmanaged.Imports;
using AOSharp.Core.UI;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace AOSharp.Core
{
    public static class Targeting
    {
        public static Dynel Target => GetTargetDynel();
        public static SimpleChar TargetChar => GetTargetChar();
        public static bool HasTarget => GetTargetIdentity() != Identity.None;

        public static void SelectSelf(bool packetOnly = false)
        {
            SetTarget(DynelManager.LocalPlayer);
        }

        public static void SetTarget(SimpleChar target, bool packetOnly = false)
        {
            SetTarget(target.Identity, packetOnly);
        }

        public static unsafe void SetTarget(Identity target, bool packetOnly = false)
        {
            if (!packetOnly)
            {
                TargetingModule_t.SetTarget(ref target, false);

                IntPtr pEngine = N3Engine_t.GetInstance();

                if (pEngine == IntPtr.Zero)
                    return;

                N3EngineClientAnarchy_t.SelectedTarget(pEngine, ref target);
            }
            else
            {
                Network.Send(new LookAtMessage()
                {
                    Target = target
                });
            }
        }

        private static Identity GetTargetIdentity()
        {
            IntPtr pInputConfig = InputConfig_t.GetInstance();

            if (pInputConfig == IntPtr.Zero)
                return Identity.None;

            Identity identity = Identity.None;
            InputConfig_t.GetCurrentTarget(pInputConfig, ref identity);

            return identity;
        }

        private static Dynel GetTargetDynel()
        {
            Identity targetIdentity = GetTargetIdentity();

            if (targetIdentity == Identity.None)
                return null;

            if (DynelManager.Find(targetIdentity, out Dynel targetDynel))
                return targetDynel;

            return null;
        }

        private static SimpleChar GetTargetChar()
        {
            Identity targetIdentity = GetTargetIdentity();

            if (targetIdentity == Identity.None)
                return null;

            if (targetIdentity.Type != IdentityType.SimpleChar)
                return null;

            if (DynelManager.Find(targetIdentity, out SimpleChar targetChar))
                return targetChar;

            return null;
        }
    }
}
