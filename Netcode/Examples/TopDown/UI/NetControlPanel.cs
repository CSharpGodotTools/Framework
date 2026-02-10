namespace Framework.Netcode.Examples.Topdown;

public partial class NetControlPanel : NetControlPanelLow<GameClient, GameServer>
{
    private const bool VerbosePacketLogs = false;

    protected override ENetOptions Options { get; set; } = new()
    {
        PrintPacketByteSize = VerbosePacketLogs,
        PrintPacketData = VerbosePacketLogs,
        PrintPacketReceived = VerbosePacketLogs,
        PrintPacketSent = VerbosePacketLogs
    };
}
