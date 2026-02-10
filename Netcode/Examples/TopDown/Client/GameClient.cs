using ENet;
using Framework.Netcode.Client;
using Godot;
using System;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

public partial class GameClient : GodotClient
{
    public event Action<uint, bool> PlayerJoinedLeaved;
    public event Action<Dictionary<uint, Vector2>> PositionsUpdated;

    public uint PeerId => _peer.ID;

    public GameClient()
    {
        RegisterPacketHandler<SPacketPlayerJoinedLeaved>(OnPlayerJoinedLeaved);
        RegisterPacketHandler<SPacketPlayerPositions>(OnPlayerPositions);
    }

    protected override void OnConnect(Event netEvent)
    {
        Send(new CPacketPlayerJoinLeave { Joined = true });
    }

    public void SendPosition(Vector2 position)
    {
        Send(new CPacketPlayerPosition { Position = position });
    }

    private void OnPlayerJoinedLeaved(SPacketPlayerJoinedLeaved packet)
    {
        PlayerJoinedLeaved?.Invoke(packet.Id, packet.Joined);
    }

    private void OnPlayerPositions(SPacketPlayerPositions packet)
    {
        PositionsUpdated?.Invoke(new Dictionary<uint, Vector2>(packet.Positions));
    }
}
