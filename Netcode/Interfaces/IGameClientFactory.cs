using Framework.Netcode.Client;

namespace Framework.Netcode;

public interface IGameClientFactory
{
    GodotClient CreateClient();
}
