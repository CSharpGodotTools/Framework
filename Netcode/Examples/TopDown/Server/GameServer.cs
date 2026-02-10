using ENet;
using Framework.Netcode.Server;
using Godot;
using System.Diagnostics;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

public partial class GameServer : GodotServer
{
    private const int BroadcastIntervalMs = 50;

    private readonly Dictionary<uint, Vector2> _positions = [];
    private long _lastBroadcastTicks;

    public GameServer()
    {
        RegisterPacketHandler<CPacketPlayerJoinLeave>(OnPlayerJoinLeave);
        RegisterPacketHandler<CPacketPlayerPosition>(OnPlayerPosition);
    }

    protected override void OnPeerDisconnect(Event netEvent)
    {
        RemovePlayer(netEvent.Peer.ID);
    }

    private void OnPlayerJoinLeave(CPacketPlayerJoinLeave packet, Peer peer)
    {
        if (packet.Joined)
        {
            // Tell the joining peer their server-assigned id first.
            Send(new SPacketPlayerJoinedLeaved { Id = peer.ID, Joined = true, IsLocal = true }, peer);
            // Inform everyone else about the new peer.
            Broadcast(new SPacketPlayerJoinedLeaved { Id = peer.ID, Joined = true, IsLocal = false }, peer);
            SendExistingPlayersTo(peer);
            SendPositionsSnapshotTo(peer);
            return;
        }

        RemovePlayer(peer.ID);
    }

    private void OnPlayerPosition(CPacketPlayerPosition packet, Peer peer)
    {
        _positions[peer.ID] = packet.Position;
        BroadcastPositions(excludePeer: peer);
    }

    private void RemovePlayer(uint id)
    {
        if (!_positions.Remove(id))
        {
            return;
        }

        Broadcast(new SPacketPlayerJoinedLeaved { Id = id, Joined = false });
        BroadcastPositions(force: true);
    }

    private void BroadcastPositions(bool force = false, Peer? excludePeer = null)
    {
        long now = Stopwatch.GetTimestamp();
        double elapsedMs = (now - _lastBroadcastTicks) * 1000.0 / Stopwatch.Frequency;
        if (!force && elapsedMs < BroadcastIntervalMs)
        {
            return;
        }

        _lastBroadcastTicks = now;
        SPacketPlayerPositions packet = new()
        {
            Positions = new Dictionary<uint, Vector2>(_positions)
        };

        if (excludePeer is { } peer)
        {
            Broadcast(packet, peer);
        }
        else
        {
            Broadcast(packet);
        }
    }

    private void SendExistingPlayersTo(Peer peer)
    {
        foreach (uint id in _positions.Keys)
        {
            if (id == peer.ID)
            {
                continue;
            }

            Send(new SPacketPlayerJoinedLeaved { Id = id, Joined = true, IsLocal = false }, peer);
        }
    }

    private void SendPositionsSnapshotTo(Peer peer)
    {
        Dictionary<uint, Vector2> snapshot = [];
        foreach (KeyValuePair<uint, Vector2> kvp in _positions)
        {
            if (kvp.Key == peer.ID)
            {
                continue;
            }

            snapshot[kvp.Key] = kvp.Value;
        }

        Send(new SPacketPlayerPositions { Positions = snapshot }, peer);
    }
}
