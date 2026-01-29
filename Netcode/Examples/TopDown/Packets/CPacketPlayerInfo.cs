using ENet;
using Godot;
using Framework.Netcode.Server;

namespace Framework.Netcode.Sandbox.Topdown;

public class CPacketPlayerInfo : ClientPacket
{
    [NetSend(1)]
    public string Username { get; set; }

    [NetSend(2)]
    public Vector2 Position { get; set; }

    public override void OnServerReceived(ENetServer server, Peer client)
    {
    }
}
