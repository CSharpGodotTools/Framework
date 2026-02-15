using Godot;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

/// <summary>
/// Sends a snapshot of active remote player positions.
/// </summary>
public partial class SPacketPlayerPositions : ServerPacket
{
    public Dictionary<uint, Vector2> Positions { get; set; }
}
