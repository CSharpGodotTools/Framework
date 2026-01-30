using ENet;
using Framework.Netcode.Server;
using System;

namespace Framework.Netcode.Sandbox.Topdown;

public partial class GameServer : GodotServer<GameServer>
{
    public GameServer()
    {
        RegisterPacketHandler<CPacketPlayerInfo>(OnPlayerInfoPacketReceived);
    }

    private void OnPlayerInfoPacketReceived(CPacketPlayerInfo info, Peer peer)
    {
        Log($"Received {info.Username} from peer {peer.ID}");
    }
}
