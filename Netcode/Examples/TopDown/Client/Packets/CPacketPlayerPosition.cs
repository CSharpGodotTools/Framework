using Godot;

namespace Framework.Netcode.Examples.Topdown;

/// <summary>
/// Sends local player position updates to the server.
/// </summary>
public partial class CPacketPlayerPosition : ClientPacket
{
    public Vector2 Position { get; set; }
}
