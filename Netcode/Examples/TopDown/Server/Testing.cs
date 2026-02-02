using ENet;
using GodotUtils;

namespace Framework.Netcode.Examples.Topdown;

public class Testing
{
    private readonly GameServer _server;

    public Testing(GameServer server)
    {
        _server = server;
    }

    public void OnTest(CPacketTest test, Peer peer)
    {
        //_server.Log(test.Test.ToFormattedString());
    }
}
