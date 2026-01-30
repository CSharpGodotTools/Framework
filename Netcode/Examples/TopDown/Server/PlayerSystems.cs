using ENet;

namespace Framework.Netcode.Examples.Topdown;

public class PlayerSystems
{
    private readonly GameServer _server;

    public PlayerSystems(GameServer server)
    {
        _server = server;
    }

    public void OnPlayerInfo(CPacketPlayerInfo info, Peer peer)
    {
        if (_server.Players.ContainsKey(peer.ID))
        {
            _server.Log($"Received player info for peer {peer.ID} (username {info.Username}) but they exist on the server already");
            return;
        }

        _server.Players[peer.ID] = new Player
        {
            Username = info.Username,
            Position = info.Position
        };

        _server.Send(new SPacketHello { Message = "What's up?" }, peer);
    }
}
