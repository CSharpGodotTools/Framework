namespace Framework.Netcode.Examples.Topdown;

/// <summary>
/// Notifies clients that a player joined or left.
/// </summary>
public partial class SPacketPlayerJoinedLeaved : ServerPacket
{
    public uint Id { get; set; }
    public bool Joined { get; set; }
    public bool IsLocal { get; set; }
}
