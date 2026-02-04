namespace Framework.Netcode;

[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PacketRegistryAttribute : System.Attribute
{
    public System.Type OpcodeType { get; }

    public PacketRegistryAttribute()
    {
        OpcodeType = typeof(byte);
    }

    public PacketRegistryAttribute(System.Type opcodeType)
    {
        OpcodeType = opcodeType;
    }
}
