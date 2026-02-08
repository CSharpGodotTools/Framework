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
            Position = new Vector2(100, 100),

            InventoryItemIds = [1, 5, 12, 42],

            ActiveBuffs =
            [
                "SpeedBoost",
                "Regeneration",
                "FireResist"
            ],

            Stats =
            {
                ["Health"] = 250,
                ["Mana"] = 120,
                ["Defense"] = 18
            }
        };

        Send(infoPacket);
    }
}
