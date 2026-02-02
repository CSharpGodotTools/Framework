using ENet;
using Framework.Netcode.Client;
using Godot;
using System.Collections.Generic;
using System.Text;

namespace Framework.Netcode.Examples.Topdown;

public partial class GameClient : GodotClient
{
    private ServerMessages _serverMessages;

    public GameClient()
    {
        _serverMessages = new ServerMessages(this);

        RegisterPacketHandler<SPacketHello>(_serverMessages.OnHello);
    }

    protected override void OnConnect(Event netEvent)
    {
        CPacketPlayerInfo infoPacket = new()
        {
            Username = "Valky",
            Position = new Vector2(100, 100)
        };

        Send(infoPacket);

        CPacketTest testPacket = new()
        {
            Id = 42,
            Name = "PacketTest",

            Numbers = new List<int>
            {
                10, 20, 30
            },

            Matrix = new List<List<int>>
            {
                new() { 1, 2 },
                new() { 3, 4, 5 },
                new() { }
            },

            Scores = new Dictionary<string, int>
            {
                ["Alice"] = 100,
                ["Bob"] = 200
            },

            Test = new Dictionary<string, List<int>>
            {
                ["A"] = new List<int> { 1, 2, 3 },
                ["B"] = new List<int> { 4, 5, 6 },
                ["Empty"] = new List<int>()
            },

            Deep = new Dictionary<string, List<List<int>>>
            {
                ["X"] = new List<List<int>>
                {
                    new() { 1 },
                    new() { 2, 3 }
                },
                ["Y"] = new List<List<int>>
                {
                    new(),
                    new() { 9, 8, 7 }
                }
            }
        };

        Send(testPacket);
    }
}
