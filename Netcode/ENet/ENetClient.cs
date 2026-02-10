using ENet;
using System.Collections.Concurrent;
using System;
using GodotUtils;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Framework.Netcode.Client;

// ENet API Reference: https://github.com/SoftwareGuy/ENet-CSharp/blob/master/DOCUMENTATION.md
public abstract class ENetClient : ENetLow
{
    // Protected Members
    protected ConcurrentQueue<Cmd<ENetClientOpcode>> ENetCmds { get; } = new();
    protected ConcurrentQueue<Cmd<GodotOpcode>> GodotCmdsInternal { get; } = new();
    protected ConcurrentQueue<ClientPacket> Outgoing { get; } = new();
    protected ConcurrentQueue<PacketData> GodotPackets { get; } = new();

    protected Peer _peer;
    protected long _connected;

    private const double ConnectionLogQuietGapSeconds = 0.5;
    private const double ConnectionLogMaxWindowSeconds = 5.0;
    private const double LifecycleLogQuietGapSeconds = 0.5;
    private const double LifecycleLogMaxWindowSeconds = 5.0;
    private static int s_connectedCount;
    private static int s_disconnectedCount;
    private static int s_timeoutCount;
    private static long s_connectionWindowStartTicks;
    private static long s_connectionLastEventTicks;
    private static int s_startedCount;
    private static int s_stoppedCount;
    private static long s_lifecycleWindowStartTicks;
    private static long s_lifecycleLastEventTicks;

    // Config
    /// <summary>
    /// The ping interval in ms. The default is 1000.
    /// </summary>
    protected virtual uint PingIntervalMs { get; } = 1000;

    /// <summary>
    /// The peer timeout in ms. The default is 5000.
    /// </summary>
    protected virtual uint PeerTimeoutMs { get; } = 5000;

    /// <summary>
    /// The peer timeout minimum in ms. The default is 5000.
    /// </summary>
    protected virtual uint PeerTimeoutMinimumMs { get; } = 5000;

    /// <summary>
    /// The peer timeout maximum in ms. The default is 5000.
    /// </summary>
    protected virtual uint PeerTimeoutMaximumMs { get; } = 5000;

    private readonly ConcurrentQueue<Packet> _incoming = new();

    /// <summary>
    /// Log messages as the client. Thread safe.
    /// </summary>
    public sealed override void Log(object message, BBColor color = BBColor.Gray)
    {
        GameFramework.Logger.Log($"[Client] {message}", color);
    }

    protected sealed override void ConcurrentQueues()
    {
        ProcessENetCommands();
        ProcessIncomingPackets();
        ProcessOutgoingPackets();
        FlushConnectionLogs(force: false);
        FlushLifecycleLogs(force: false);
    }

    protected virtual void OnConnect(Event netEvent) { }
    protected virtual void OnDisconnect(Event netEvent) { }
    protected virtual void OnTimeout(Event netEvent) { }

    protected sealed override void OnConnectLow(Event netEvent)
    {
        Interlocked.Exchange(ref _connected, 1);
        GodotCmdsInternal.Enqueue(new Cmd<GodotOpcode>(GodotOpcode.Connected));
        Interlocked.Increment(ref s_connectedCount);
        MarkConnectionEvent();
        TryInvoke(() => OnConnect(netEvent));
    }

    protected sealed override void OnDisconnectLow(Event netEvent)
    {
        DisconnectOpcode opcode = (DisconnectOpcode)netEvent.Data;
        
        GodotCmdsInternal.Enqueue(new Cmd<GodotOpcode>(GodotOpcode.Disconnected, opcode));
        
        OnDisconnectCleanup(_peer);

        Interlocked.Increment(ref s_disconnectedCount);
        MarkConnectionEvent();
        TryInvoke(() => OnDisconnect(netEvent));
    }

    protected sealed override void OnTimeoutLow(Event netEvent)
    {
        // I do not remember why I enqueued both a Timeout AND a Disconnected Godot cmds
        GodotCmdsInternal.Enqueue(new Cmd<GodotOpcode>(GodotOpcode.Disconnected, DisconnectOpcode.Timeout));
        GodotCmdsInternal.Enqueue(new Cmd<GodotOpcode>(GodotOpcode.Timeout));

        OnDisconnectCleanup(_peer);
        Interlocked.Increment(ref s_timeoutCount);
        MarkConnectionEvent();
        TryInvoke(() => OnTimeout(netEvent));
    }

    protected sealed override void OnReceiveLow(Event netEvent)
    {
        Packet packet = netEvent.Packet;
        if (packet.Length > GamePacket.MaxSize)
        {
            Log($"Tried to read packet from server of size {packet.Length} when max packet size is {GamePacket.MaxSize}");
            
            packet.Dispose();
            return;
        }

        _incoming.Enqueue(packet);
    }

    protected sealed override void OnDisconnectCleanup(Peer peer)
    {
        base.OnDisconnectCleanup(peer);
        Interlocked.Exchange(ref _connected, 0);
    }

    protected void WorkerThread(string ip, ushort port)
    {
        Host = new Host();
        Host.Create();

        _peer = Host.Connect(CreateAddress(ip, port));
        _peer.PingInterval(PingIntervalMs);
        _peer.Timeout(PeerTimeoutMs, PeerTimeoutMinimumMs, PeerTimeoutMaximumMs);

        try
        {
            WorkerLoop();
        }
        finally
        {
            Host.Dispose();
        }
        
        NotifyClientStopped();
    }

    private void ProcessENetCommands()
    {
        while (ENetCmds.TryDequeue(out Cmd<ENetClientOpcode> cmd))
        {
            if (cmd.Opcode == ENetClientOpcode.Disconnect)
            {
                if (CTS.IsCancellationRequested)
                {
                    Log("Client is in the middle of stopping");
                    break;
                }

                _peer.Disconnect((uint)DisconnectOpcode.Disconnected);
                OnDisconnectCleanup(_peer);
            }
        }
    }

    private void ProcessIncomingPackets()
    {
        while (_incoming.TryDequeue(out Packet packet))
        {
            PacketReader packetReader = new(packet);
            Type type = null;

            try
            {
                byte opcode = packetReader.ReadByte();
                if (!PacketRegistry.ServerPacketTypes.TryGetValue(opcode, out type))
                {
                    Log($"Received malformed opcode: {opcode} (Ignoring)");
                    packetReader.Dispose();
                    continue;
                }
            }
            catch (EndOfStreamException e)
            {
                Log($"Received malformed packet: {e.Message} (Ignoring)");
                packetReader.Dispose();
                continue;
            }

            ServerPacket handlePacket = PacketRegistry.ServerPacketInfo[type].Instance;

            /*
             * Instead of packets being handled client-side, they are handled on the Godot thread.
             * Note that handlePacket AND packetReader need to be sent over.
             */
            GodotPackets.Enqueue(new PacketData
            {
                Type = type,
                PacketReader = packetReader,
                HandlePacket = handlePacket
            });
        }
    }

    private void ProcessOutgoingPackets()
    {
        while (Outgoing.TryDequeue(out ClientPacket clientPacket))
        {
            Type type = clientPacket.GetType();

            try
            {
                LogOutgoingPacket(type, clientPacket);
                clientPacket.Send();
            }
            catch (Exception e)
            {
                GameFramework.Logger.LogErr(e, "Client");
            }
        }
    }

    private void LogOutgoingPacket(Type type, ClientPacket clientPacket)
    {
        if (!Options.PrintPacketSent || IgnoredPackets.Contains(type))
            return;

        string packetData = Options.PrintPacketData ? $"\n{clientPacket.ToFormattedString()}" : string.Empty;
        Log($"Sent packet: {type.Name} {FormatByteSize(clientPacket.GetSize())}{packetData}");
    }

    private static Address CreateAddress(string ip, ushort port)
    {
        Address address = new() { Port = port };
        address.SetHost(ip);
        return address;
    }

    private void FlushConnectionLogs(bool force)
    {
        if (Volatile.Read(ref s_connectedCount) == 0 &&
            Volatile.Read(ref s_disconnectedCount) == 0 &&
            Volatile.Read(ref s_timeoutCount) == 0)
            return;

        long startTicks = Interlocked.Read(ref s_connectionWindowStartTicks);
        long lastEventTicks = Interlocked.Read(ref s_connectionLastEventTicks);
        if (startTicks == 0 || lastEventTicks == 0)
            return;

        long now = Stopwatch.GetTimestamp();
        double sinceLast = (now - lastEventTicks) / (double)Stopwatch.Frequency;
        double windowSeconds = (lastEventTicks - startTicks) / (double)Stopwatch.Frequency;

        if (!force && sinceLast < ConnectionLogQuietGapSeconds && windowSeconds < ConnectionLogMaxWindowSeconds)
            return;

        if (!force && Interlocked.CompareExchange(ref s_connectionLastEventTicks, 0, lastEventTicks) != lastEventTicks)
            return;

        int connects = Interlocked.Exchange(ref s_connectedCount, 0);
        int disconnects = Interlocked.Exchange(ref s_disconnectedCount, 0);
        int timeouts = Interlocked.Exchange(ref s_timeoutCount, 0);
        if (force)
            Interlocked.Exchange(ref s_connectionLastEventTicks, 0);

        Interlocked.CompareExchange(ref s_connectionWindowStartTicks, 0, startTicks);

        double reportSeconds = Math.Max(windowSeconds, 0.01);

        if (connects > 0)
            Log($"{connects} connect event{(connects == 1 ? "" : "s")} (last {reportSeconds:0.##}s)");

        if (disconnects > 0)
            Log($"{disconnects} disconnect event{(disconnects == 1 ? "" : "s")} (last {reportSeconds:0.##}s)");

        if (timeouts > 0)
            Log($"{timeouts} timeout event{(timeouts == 1 ? "" : "s")} (last {reportSeconds:0.##}s)");
    }

    private void FlushLifecycleLogs(bool force)
    {
        int startedSnapshot = Volatile.Read(ref s_startedCount);
        int stoppedSnapshot = Volatile.Read(ref s_stoppedCount);
        if (startedSnapshot == 0 && stoppedSnapshot == 0)
            return;

        long startTicks = Interlocked.Read(ref s_lifecycleWindowStartTicks);
        long lastEventTicks = Interlocked.Read(ref s_lifecycleLastEventTicks);
        if (startTicks == 0 || lastEventTicks == 0)
            return;

        long now = Stopwatch.GetTimestamp();
        double sinceLast = (now - lastEventTicks) / (double)Stopwatch.Frequency;
        double windowSeconds = (lastEventTicks - startTicks) / (double)Stopwatch.Frequency;

        if (!force && sinceLast < LifecycleLogQuietGapSeconds && windowSeconds < LifecycleLogMaxWindowSeconds)
            return;

        if (!force && Interlocked.CompareExchange(ref s_lifecycleLastEventTicks, 0, lastEventTicks) != lastEventTicks)
            return;

        int started = Interlocked.Exchange(ref s_startedCount, 0);
        int stopped = Interlocked.Exchange(ref s_stoppedCount, 0);

        if (force)
            Interlocked.Exchange(ref s_lifecycleLastEventTicks, 0);

        Interlocked.CompareExchange(ref s_lifecycleWindowStartTicks, 0, startTicks);

        double reportSeconds = Math.Max(windowSeconds, 0.01);

        if (started > 0)
            Log($"{started} client{(started == 1 ? "" : "s")} started (last {reportSeconds:0.##}s)");

        if (stopped > 0)
            Log($"{stopped} client{(stopped == 1 ? "" : "s")} stopped (last {reportSeconds:0.##}s)");
    }

    protected void NotifyClientStarting()
    {
        Interlocked.Increment(ref s_startedCount);
        MarkLifecycleEvent();
    }

    private void NotifyClientStopped()
    {
        Interlocked.Increment(ref s_stoppedCount);
        MarkLifecycleEvent();
    }

    private static void MarkConnectionEvent()
    {
        long now = Stopwatch.GetTimestamp();
        if (Interlocked.CompareExchange(ref s_connectionWindowStartTicks, now, 0) == 0)
            Interlocked.Exchange(ref s_connectionLastEventTicks, now);
        else
            Interlocked.Exchange(ref s_connectionLastEventTicks, now);
    }

    private static void MarkLifecycleEvent()
    {
        long now = Stopwatch.GetTimestamp();
        if (Interlocked.CompareExchange(ref s_lifecycleWindowStartTicks, now, 0) == 0)
            Interlocked.Exchange(ref s_lifecycleLastEventTicks, now);
        else
            Interlocked.Exchange(ref s_lifecycleLastEventTicks, now);
    }

    private void TryInvoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            GameFramework.Logger.LogErr(e, "Client");
        }
    }
}
