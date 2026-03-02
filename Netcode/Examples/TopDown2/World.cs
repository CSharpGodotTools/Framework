using Godot;
using Framework.Netcode.Client;
using Framework.Netcode.Server;
using Template.Framework.Netcode.Examples.TopDown2; 

namespace Framework.Netcode.Examples.TopDown2;

public partial class World : Node
{
    private Net _net;

    public override void _Ready()
    {
        _net = new Net(new ClientFactory(), new ServerFactory());
        _net.StartServer(25565, 100);
        _net.StartClient("127.0.0.1", 25565);
    }

    #region Factories
    private class ClientFactory : IGameClientFactory
    {
        public GodotClient CreateClient() => new GameClient();
    }

    private class ServerFactory : IGameServerFactory
    {
        public GodotServer CreateServer() => new GameServer();
    }
    #endregion
}
