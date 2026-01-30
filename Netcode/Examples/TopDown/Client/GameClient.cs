using ENet;
using Godot;
using Framework.Netcode.Client;

namespace Framework.Netcode.Sandbox.Topdown;

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
    }
}
