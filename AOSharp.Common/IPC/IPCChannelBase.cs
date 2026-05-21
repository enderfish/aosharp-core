using System;
using System.Collections.Generic;

namespace AOSharp.Core.IPC
{
    public abstract class IPCChannelBase
    {
        protected abstract int _localDynelId { get; }

        protected IPCChannelBase(byte channelId) { }

        public virtual void Broadcast(IPCMessage msg) { }

        public virtual void RegisterCallback(int opCode, Action<int, IPCMessage> callback) { }

        public virtual bool SetChannelId(byte channelId) => true;
    }
}
