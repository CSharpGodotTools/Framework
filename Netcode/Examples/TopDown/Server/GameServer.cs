using ENet;
using Framework.Netcode.Server;
using Godot;
using System.Collections.Generic;
using System.Diagnostics;

namespace Framework.Netcode.Examples.Topdown;

public partial class GameServer : GodotServer
{
    private const int PositionBroadcastIntervalMs = 50;

    private readonly HashSet<uint> _players = [];
    private readonly Dictionary<uint, Vector2> _positions = [];
    private long _lastPositionBroadcastTicks;

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
            AddPlayer(peer);
            return;
        }

        RemovePlayer(peer.ID);
    }

    private void OnPlayerPosition(CPacketPlayerPosition packet, Peer peer)
    {
        if (!_players.Contains(peer.ID))
        {
            return;
        }

        _positions[peer.ID] = packet.Position;
        BroadcastPositions(excludePeer: peer);
    }

    private void AddPlayer(Peer peer)
    {
        if (!_players.Add(peer.ID))
        {
            return;
        }

        Send(new SPacketPlayerJoinedLeaved { Id = peer.ID, Joined = true, IsLocal = true }, peer);
        Broadcast(new SPacketPlayerJoinedLeaved { Id = peer.ID, Joined = true, IsLocal = false }, peer);
        SendExistingPlayersTo(peer);
        SendPositionsSnapshotTo(peer);
    }

    private void RemovePlayer(uint id)
    {
        if (!_players.Remove(id))
        {
            return;
        }

        _positions.Remove(id);
        Broadcast(new SPacketPlayerJoinedLeaved { Id = id, Joined = false });
        BroadcastPositions(force: true);
    }

    private void BroadcastPositions(bool force = false, Peer? excludePeer = null)
    {
        if (!CanBroadcastPositions(PositionBroadcastIntervalMs, force))
        {
            return;
        }

        SPacketPlayerPositions packet = new()
        {
            Positions = new Dictionary<uint, Vector2>(_positions)
        };

        if (excludePeer is { } excludedPeer)
        {
            Broadcast(packet, excludedPeer);
        }
        else
        {
            Broadcast(packet);
        }
    }

    private bool CanBroadcastPositions(int intervalMs, bool force)
    {
        if (intervalMs <= 0)
        {
            return true;
        }

        long now = Stopwatch.GetTimestamp();
        if (force)
        {
            _lastPositionBroadcastTicks = now;
            return true;
        }

        if (_lastPositionBroadcastTicks == 0)
        {
            _lastPositionBroadcastTicks = now;
            return true;
        }

        long intervalTicks = (long)(intervalMs * (double)Stopwatch.Frequency / 1000.0);
        if (now - _lastPositionBroadcastTicks < intervalTicks)
        {
            return false;
        }

        _lastPositionBroadcastTicks = now;
        return true;
    }

    private void SendExistingPlayersTo(Peer peer)
    {
        foreach (uint id in _players)
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
