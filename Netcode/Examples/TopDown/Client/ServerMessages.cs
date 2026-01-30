namespace Framework.Netcode.Examples.Topdown;

public class ServerMessages
{
    private readonly GameClient _client;

    public ServerMessages(GameClient client)
    {
        _client = client;
    }

    public void OnHello(SPacketHello hello)
    {
        _client.Log("The server said to me: " + hello.Message);
    }
}
