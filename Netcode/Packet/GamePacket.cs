using ENet;
using System;

namespace Framework.Netcode;

/// <summary>
/// Shared packet functionality for client-to-server and server-to-client packets.
/// </summary>
public abstract class GamePacket
{
    public static int MaxSize => 8192;

    protected Peer[] Peers { get; private set; } = [];
    protected byte ChannelId { get; }

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
        ArgumentNullException.ThrowIfNull(peers);

        Peers = [.. peers];
    }

    public long GetSize()
    {
        return _size;
    }

    public abstract byte GetOpcode();

    /// <summary>
    /// Writes packet payload data after opcode serialization.
    /// PacketGen generates this path in packet partial classes, and reflection fallback should be avoided for new packets.
    /// </summary>
    public virtual void Write(PacketWriter writer)
    {
        // Implemented in generated packet partials.
    }

    /// <summary>
    /// Reads packet payload data after opcode deserialization.
    /// PacketGen generates this path in packet partial classes, and reflection fallback should be avoided for new packets.
    /// </summary>
    public virtual void Read(PacketReader reader)
    {
        // Implemented in generated packet partials.
    }

    protected Packet CreateENetPacket()
    {
        if (_data == null)
        {
            throw new InvalidOperationException($"{GetType().Name} cannot create an ENet packet before Write() is called.");
        }

        Packet enetPacket = default;
        enetPacket.Create(_data, _packetFlags);
        return enetPacket;
    }
}
