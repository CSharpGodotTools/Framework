namespace Framework.Netcode;

[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PacketRegistryAttribute : System.Attribute
{
    /// <summary>
    /// Numeric opcode backing type used by generated packet registry code.
    /// </summary>
    public System.Type OpcodeType { get; }

    public PacketRegistryAttribute()
    {
        OpcodeType = typeof(byte);
    }

    public PacketRegistryAttribute(System.Type opcodeType)
    {
        System.ArgumentNullException.ThrowIfNull(opcodeType);
        OpcodeType = opcodeType;
    }
}
