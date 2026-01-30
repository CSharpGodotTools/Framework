using Framework.Netcode.Server;

namespace Framework.Netcode;

public interface IGameServerFactory<TServer> where TServer : ENetServer<TServer>
{
    GodotServer<TServer> CreateServer();
}
