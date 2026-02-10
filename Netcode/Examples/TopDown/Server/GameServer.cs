using ENet;
using Framework.Netcode.Server;
using Godot;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

public partial class GameServer : GodotServer
{
    private readonly Dictionary<uint, Vector2> _positions = [];

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
            if (_positions.ContainsKey(peer.ID))
            {
                return;
            }

            _positions[peer.ID] = Vector2.Zero;
            Broadcast(new SPacketPlayerJoinedLeaved { Id = peer.ID, Joined = true });
            BroadcastPositions();
            return;
        }

        RemovePlayer(peer.ID);
    }

    private void OnPlayerPosition(CPacketPlayerPosition packet, Peer peer)
    {
        _positions[peer.ID] = packet.Position;
        BroadcastPositions();
    }

    private void RemovePlayer(uint id)
    {
        if (!_positions.Remove(id))
        {
            return;
        }

        Broadcast(new SPacketPlayerJoinedLeaved { Id = id, Joined = false });
        BroadcastPositions();
    }

    private void BroadcastPositions()
    {
        Broadcast(new SPacketPlayerPositions
        {
            Positions = new Dictionary<uint, Vector2>(_positions)
        });
    }
}
