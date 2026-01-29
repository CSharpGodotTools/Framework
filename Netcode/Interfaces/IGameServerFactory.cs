using Framework.Netcode.Server;

namespace Framework.Netcode;

public interface IGameServerFactory
{
    GodotServer CreateServer();
}
