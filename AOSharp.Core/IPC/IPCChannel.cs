using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO;
using SmokeLounge.AOtomation.Messaging.Serialization;
using SmokeLounge.AOtomation.Messaging.Serialization.Serializers;
using StreamWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;
using StreamReader = SmokeLounge.AOtomation.Messaging.Serialization.StreamReader;
using TypeInfo = SmokeLounge.AOtomation.Messaging.Serialization.TypeInfo;
using AOSharp.Common.Unmanaged.Imports;
using AOSharp.Core.UI;
using System.Reflection;

namespace AOSharp.Core.IPC
{
    public class IPCChannel
    {
        private static IPAddress MulticastIP = IPAddress.Parse("224.0.0.111");
        private IPEndPoint _localEndPoint = new IPEndPoint(IPAddress.Any, Port);
        private IPEndPoint _remoteEndPoint = new IPEndPoint(MulticastIP, Port);
        private const int Port = 1911;
        private const ushort PacketPrefix = 0xFFFF;

        private byte _channelId;
        private UdpClient _udpClient;

        private static SerializerResolver _serializerResolver = new SerializerResolverBuilder<IPCMessage>().Build();
        private static TypeInfo _typeInfo = new TypeInfo(typeof(IPCMessage));
        private static PacketInspector _packetInspector;

        private ConcurrentQueue<byte[]> _packetQueue = new ConcurrentQueue<byte[]>();
        private Dictionary<int, Action<int, IPCMessage>> _callbacks = new Dictionary<int, Action<int, IPCMessage>>();
        private static List<IPCChannel> _ipcChannels = new List<IPCChannel>();

        public IPCChannel(byte channelId)
        {
            _channelId = channelId;

            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(_localEndPoint);

            _udpClient.JoinMulticastGroup(MulticastIP);
            _udpClient.BeginReceive(ReceiveCallback, null);

            _packetInspector = new PacketInspector(_typeInfo);
            _ipcChannels.Add(this);
        }

        ~IPCChannel()
        {
            _ipcChannels.Remove(this);
        }

        internal static void Update()
        {
            try
            {
                foreach (IPCChannel ipcChannel in _ipcChannels)
                    ipcChannel.ProcessQueue();
            }
            catch (Exception e)
            {
                Chat.WriteLine($"This shouldn't happen pls report (IPC): {e.Message}");
            }
        }

        private void ProcessQueue()
        {
            while (_packetQueue.TryDequeue(out byte[] msgBytes))
                ProcessIPCMessage(msgBytes);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            byte[] receiveBytes = _udpClient.EndReceive(ar, ref _localEndPoint);
            _udpClient.BeginReceive(ReceiveCallback, null);

            if (receiveBytes.Length < 11)
                return;

            _packetQueue.Enqueue(receiveBytes);
        }

        private void ProcessIPCMessage(byte[] msgBytes)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(msgBytes))
                {
                    StreamReader reader = new StreamReader(stream) { Position = 0 };

                    if (reader.ReadUInt16() != 0xFFFF)
                        return;

                    ushort len = reader.ReadUInt16();

                    if (len != msgBytes.Length)
                        return;

                    byte channelId = reader.ReadByte();

                    if (channelId != _channelId)
                        return;

                    int charId = reader.ReadInt32();

                    if (charId == Game.ClientInst)
                        return;

                    reader.Position = 2;
                    TypeInfo subTypeInfo = _packetInspector.FindSubType(reader, out int opCode);

                    if (subTypeInfo == null)
                        return;

                    var serializer = _serializerResolver.GetSerializer(subTypeInfo.Type);
                    if (serializer == null)
                        return;

                    reader.Position = 11;
                    SerializationContext serializationContext = new SerializationContext(_serializerResolver);

                    IPCMessage message = (IPCMessage)serializer.Deserialize(reader, serializationContext);

                    if (_callbacks.ContainsKey(opCode))
                        _callbacks[opCode]?.Invoke(charId, message);
                }
            }
            catch (Exception e)
            {
                //If you get this message and it concerns you please create an issue or contact me on Discord!
                Chat.WriteLine($"Failed to process IPC message {e.Message}");
            }
        }

        public void Broadcast(IPCMessage msg)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                ISerializer serializer = _serializerResolver.GetSerializer(msg.GetType());
                if (serializer == null)
                    return;

                SerializationContext serializationContext = new SerializationContext(_serializerResolver);
                StreamWriter writer = new StreamWriter(stream) { Position = 0 };
                writer.WriteUInt16(PacketPrefix);
                writer.WriteInt16(0);
                writer.WriteByte(_channelId);
                writer.WriteInt32(Game.ClientInst);
                writer.WriteInt16(msg.Opcode);
                serializer.Serialize(writer, serializationContext, msg);
                long length = writer.Position;
                writer.Position = 2;
                writer.WriteInt16((short)length);
                writer.Dispose();

                byte[] serialized = stream.ToArray();
                _udpClient.Send(serialized, serialized.Length, _remoteEndPoint);
            }
        }

        public void RegisterCallback(int opCode, Action<int, IPCMessage> callback)
        {
            if (_callbacks.ContainsKey(opCode))
                return;

            _callbacks.Add(opCode, callback);
        }

        public static void LoadMessages(Assembly assembly)
        {
            _typeInfo.InitializeSubTypesForAssembly(assembly);
        }

        /// <summary>
        /// Registers a single IPC message type. Use this for message classes that live in a shared library rather than
        /// in the plugin assembly itself, since only plugin assemblies are scanned automatically on load.
        /// </summary>
        public static void LoadMessage(Type messageType)
        {
            _typeInfo.InitializeSubType(messageType);
        }

        public bool SetChannelId(byte channelId)
        {
            if (_ipcChannels.Any(x => x._channelId == channelId))
                return false;

            _channelId = channelId;
            return true;
        }
    }
}
