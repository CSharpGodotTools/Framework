using ENet;
using Godot;

namespace Framework.Netcode.Examples.Topdown;

public class CPacketPlayerInfo : ClientPacket
{
    [NetSend(1)]
    public string Username { get; set; }

    [NetSend(2)]
    public Vector2 Position { get; set; }
}
