using ENet;
using Godot;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

public partial class CPacketPlayerInfo : ClientPacket
{
    public string Username { get; set; }
    public Vector2 Position { get; set; }

    // Array type
    public int[] InventoryItemIds { get; set; } = [];

    // Generic collection types
    public List<string> ActiveBuffs { get; set; } = [];
    public Dictionary<string, int> Stats { get; set; } = [];
}
