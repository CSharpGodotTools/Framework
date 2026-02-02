using ENet;
using Framework.Netcode.Server;
using Godot;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

public partial class GameServer : GodotServer
{
    public Dictionary<uint, Player> Players { get; } = [];

    private PlayerSystems _playerSystems;
    private Testing _testing;

    public GameServer()
    {
        _playerSystems = new PlayerSystems(this);
        _testing = new Testing(this);

        RegisterPacketHandler<CPacketPlayerInfo>(_playerSystems.OnPlayerInfo);
        RegisterPacketHandler<CPacketTest>(_testing.OnTest);
    }

    protected override void OnPeerDisconnect(Event netEvent)
    {
        _playerSystems.OnPlayerDisconnect(netEvent);
    }
}
