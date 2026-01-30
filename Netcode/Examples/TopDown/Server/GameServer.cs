using Framework.Netcode.Server;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

public partial class GameServer : GodotServer<GameServer>
{
    public Dictionary<uint, Player> Players { get; } = [];

    private PlayerSystems _playerSystems;

    public GameServer()
    {
        _playerSystems = new PlayerSystems(this);

        RegisterPacketHandler<CPacketPlayerInfo>(_playerSystems.OnPlayerInfo);
    }
}
