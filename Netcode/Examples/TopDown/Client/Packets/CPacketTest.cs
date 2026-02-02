using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

public partial class CPacketTest : ClientPacket
{
    public int Id { get; set; }

    public string Name { get; set; }

    public List<int> Numbers { get; set; }

    public List<List<int>> Matrix { get; set; }

    public Dictionary<string, int> Scores { get; set; }

    public Dictionary<string, List<int>> Test { get; set; }

    public Dictionary<string, List<List<int>>> Deep { get; set; }
}
