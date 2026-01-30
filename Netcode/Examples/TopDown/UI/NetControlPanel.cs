namespace Framework.Netcode.Examples.Topdown;

public partial class NetControlPanel : NetControlPanelLow<GameClient, GameServer>
{
    protected override ENetOptions Options { get; set; } = new()
    {
        PrintPacketByteSize = true,
        PrintPacketData = true,
        PrintPacketReceived = true,
        PrintPacketSent = true
    };
}
