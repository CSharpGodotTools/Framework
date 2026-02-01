namespace Framework.Netcode;

public class PacketInfo<T>
{
    public byte Opcode { get; set; }
    public T Instance { get; set; }
}
