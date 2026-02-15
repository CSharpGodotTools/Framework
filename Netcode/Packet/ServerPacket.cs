using ENet;
using System;
using Framework.Netcode.Client;

namespace Framework.Netcode;

/// <summary>
/// Packet sent from a server to one or more clients.
/// </summary>
public abstract class ServerPacket : GamePacket
{
    private SendType _sendType;
    private readonly Type _packetType;

    public ServerPacket()
    {
        _packetType = GetType();
    }

    public void Send()
    {
        if (Peers == null || Peers.Length == 0)
        {
            throw new InvalidOperationException($"{GetType().Name} cannot send without a target peer.");
        }

        Packet enetPacket = CreateENetPacket();
        Peers[0].Send(ChannelId, ref enetPacket);
    }

    public void Broadcast(Host host)
    {
        ArgumentNullException.ThrowIfNull(host);

        Packet enetPacket = CreateENetPacket();
        Peer[] peers = Peers ?? [];

        if (peers.Length == 0)
        {
            host.Broadcast(ChannelId, ref enetPacket);
        }
        else if (peers.Length == 1)
        {
            host.Broadcast(ChannelId, ref enetPacket, peers[0]);
        }
        else
        {
            host.Broadcast(ChannelId, ref enetPacket, peers);
        }
    }

    public void SetSendType(SendType sendType)
    {
        _sendType = sendType;
    }

    public SendType GetSendType()
    {
        return _sendType;
    }

    public override byte GetOpcode()
    {
        return PacketRegistry.ServerPacketInfo[_packetType].Opcode;
    }
}

/// <summary>
/// Delivery mode selected when enqueuing server packets.
/// </summary>
public enum SendType
{
    Peer,
    Broadcast
}
