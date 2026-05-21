using SmokeLounge.AOtomation.Messaging.Serialization;
using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

namespace AOSharp.Core.IPC
{
    [AoKnownType(9, IdentifierType.Int16)]
    public abstract class IPCMessage
    {
        public abstract short Opcode { get; }
    }
}
