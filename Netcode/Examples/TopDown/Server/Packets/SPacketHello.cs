using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Netcode.Sandbox.Topdown;

public class SPacketHello : ServerPacket
{
    [NetSend(1)]
    public string Message { get; set; }
}
