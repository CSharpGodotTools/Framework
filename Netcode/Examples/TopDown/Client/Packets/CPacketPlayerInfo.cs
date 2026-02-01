using ENet;
using Godot;

namespace Framework.Netcode.Examples.Topdown;

public partial class CPacketPlayerInfo : ClientPacket
{
    public string Username { get; set; }
    public Vector2 Position { get; set; }
}
