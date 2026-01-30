using Framework.Netcode.Server;
using System;

namespace Framework.Netcode;

/// <summary>
/// A packet sent from the client to the server
/// </summary>
public abstract class ClientPacket : GamePacket
{
    private readonly Type _type;

    public ClientPacket()
    {
        _type = GetType();
    }

    public void Send()
    {
        ENet.Packet enetPacket = CreateENetPacket();
        Peers[0].Send(ChannelId, ref enetPacket);
    }

    public override byte GetOpcode()
    {
        return PacketRegistry.ClientPacketInfoByType[_type].Opcode;
    }
}
