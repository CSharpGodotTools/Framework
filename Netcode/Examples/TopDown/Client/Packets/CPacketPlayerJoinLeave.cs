namespace Framework.Netcode.Examples.Topdown;

/// <summary>
/// Signals a player session intent change (join/leave).
/// </summary>
public partial class CPacketPlayerJoinLeave : ClientPacket
{
    public bool Joined { get; set; }
}
