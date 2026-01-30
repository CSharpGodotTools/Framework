using ENet;
using Framework.Netcode.Server;
using Godot;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

public partial class GameServer : GodotServer
{
    public Dictionary<uint, Player> Players { get; } = [];

    private PlayerSystems _playerSystems;

    public GameServer()
    {
        _playerSystems = new PlayerSystems(this);

        RegisterPacketHandler<CPacketPlayerInfo>(_playerSystems.OnPlayerInfo);
    }

    protected override void OnPeerDisconnect(Event netEvent)
    {
        _playerSystems.OnPlayerDisconnect(netEvent);
    }
}
