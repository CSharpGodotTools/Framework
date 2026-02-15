using System;

namespace Framework.Netcode;

/// <summary>
/// Packet sent from a client to a server.
/// </summary>
public abstract class ClientPacket : GamePacket
{
    private readonly Type _packetType;

    public ClientPacket()
    {
        _packetType = GetType();
    }

    public void Send()
    {
        if (Peers == null || Peers.Length == 0)
        {
            throw new InvalidOperationException($"{GetType().Name} cannot send without a target peer.");
        }

        ENet.Packet enetPacket = CreateENetPacket();
        Peers[0].Send(ChannelId, ref enetPacket);
    }

    public override byte GetOpcode()
    {
        return PacketRegistry.ClientPacketInfo[_packetType].Opcode;
    }
}
