using ENet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Framework.Netcode;

/// <summary>
/// A base class with common functionality for Client and Server packets
/// </summary>
public abstract class GamePacket
{
    public static int MaxSize => 8192;

    protected Peer[] Peers { get; private set; }
    protected byte ChannelId { get; }

    // Packets are reliable by default
    private readonly PacketFlags _packetFlags = PacketFlags.Reliable;
    private long _size;
    private byte[] _data;

    public void Write()
    {
        using PacketWriter writer = new();
        writer.Write(GetOpcode());
        Write(writer);

        _data = writer.Stream.ToArray();
        _size = writer.Stream.Length;
    }

    public void SetPeer(Peer peer)
    {
        Peers = [peer];
    }

    public void SetPeers(Peer[] peers)
    {
        Peers = peers;
    }

    public long GetSize()
    {
        return _size;
    }

    public abstract byte GetOpcode();

    public virtual void Write(PacketWriter writer)
    {
        // Handled by source generator
    }

    public virtual void Read(PacketReader reader)
    {
        // Handled by source generator
    }

    protected Packet CreateENetPacket()
    {
        Packet enetPacket = default;
        enetPacket.Create(_data, _packetFlags);
        return enetPacket;
    }
}
