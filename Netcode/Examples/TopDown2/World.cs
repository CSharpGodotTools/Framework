using Godot;
using System;

namespace Framework.Netcode.Examples.Topdown2;

public partial class World : Node
{
    private Net net;

    public override void _Ready()
    {
        Net.StartServer(25565, 100, new ENetOptions
        {
            PrintPacketByteSize = false
        });
    }
}
