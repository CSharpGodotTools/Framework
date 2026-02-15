using ENet;
using System.Collections.Concurrent;
using System;
using GodotUtils;
using System.Diagnostics;
using System.Collections.Generic;
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
    private static readonly ClientLogAggregator _logAggregator = new();

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

    public uint PeerId => _peer.ID;

    /// <summary>
    /// Log messages as the client. Thread safe.
    /// </summary>
    public sealed override void Log(object message, BBColor color = BBColor.Gray)
    {
        string timestamp = string.Empty;
        if (Options != null && Options.ShowLogTimestamps)
            timestamp = $"[{DateTime.Now:HH:mm:ss}] ";

        GameFramework.Logger.Log($"{timestamp}[Client] {message}", color);
    }

    protected sealed override void ConcurrentQueues()
    {
        ProcessENetCommands();
        ProcessIncomingPackets();
        ProcessOutgoingPackets();
        _logAggregator.Flush(force: false, message => Log(message));
    }

    protected virtual void OnConnect(Event netEvent) { }
    protected virtual void OnDisconnect(Event netEvent) { }
    protected virtual void OnTimeout(Event netEvent) { }

    protected sealed override void OnConnectLow(Event netEvent)
    {
        Interlocked.Exchange(ref _connected, 1);
        GodotCmdsInternal.Enqueue(new Cmd<GodotOpcode>(GodotOpcode.Connected));
        _logAggregator.RecordConnect(netEvent.Peer.ID);
        TryInvoke(() => OnConnect(netEvent));
    }

    protected sealed override void OnDisconnectLow(Event netEvent)
    {
        DisconnectOpcode opcode = (DisconnectOpcode)netEvent.Data;
        
        GodotCmdsInternal.Enqueue(new Cmd<GodotOpcode>(GodotOpcode.Disconnected, opcode));
        
        OnDisconnectCleanup(_peer);

        _logAggregator.RecordDisconnect(netEvent.Peer.ID);
        TryInvoke(() => OnDisconnect(netEvent));
    }

    protected sealed override void OnTimeoutLow(Event netEvent)
    {
        // I do not remember why I enqueued both a Timeout AND a Disconnected Godot cmds
        GodotCmdsInternal.Enqueue(new Cmd<GodotOpcode>(GodotOpcode.Disconnected, DisconnectOpcode.Timeout));
        GodotCmdsInternal.Enqueue(new Cmd<GodotOpcode>(GodotOpcode.Timeout));

        OnDisconnectCleanup(_peer);
        _logAggregator.RecordTimeout(netEvent.Peer.ID);
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

        string packetData = string.Empty;
        if (Options.PrintPacketData)
            packetData = $"\n{clientPacket.ToFormattedString()}";
        Log($"Sent packet: {type.Name} {FormatByteSize(clientPacket.GetSize())}{packetData}");
    }

    private static Address CreateAddress(string ip, ushort port)
    {
        Address address = new() { Port = port };
        address.SetHost(ip);
        return address;
    }

    private sealed class ClientLogAggregator
    {
        private const double QuietGapSeconds = 0.5;
        private const double MaxWindowSeconds = 5.0;

        private int _connectedCount;
        private int _disconnectedCount;
        private int _timeoutCount;

        private long _eventWindowStartTicks;
        private long _eventLastEventTicks;

        private long _lastConnectTicks;
        private long _lastDisconnectTicks;
        private long _lastTimeoutTicks;
        private long _lastConnectPeerId;
        private long _lastDisconnectPeerId;
        private long _lastTimeoutPeerId;

        public void RecordConnect(uint peerId)
        {
            Interlocked.Increment(ref _connectedCount);
            Interlocked.Exchange(ref _lastConnectPeerId, (long)peerId);
            MarkEvent(ref _lastConnectTicks);
        }

        public void RecordDisconnect(uint peerId)
        {
            Interlocked.Increment(ref _disconnectedCount);
            Interlocked.Exchange(ref _lastDisconnectPeerId, (long)peerId);
            MarkEvent(ref _lastDisconnectTicks);
        }

        public void RecordTimeout(uint peerId)
        {
            Interlocked.Increment(ref _timeoutCount);
            Interlocked.Exchange(ref _lastTimeoutPeerId, (long)peerId);
            MarkEvent(ref _lastTimeoutTicks);
        }

        public void Flush(bool force, Action<string> log)
        {
            int connectedSnapshot = Volatile.Read(ref _connectedCount);
            int disconnectedSnapshot = Volatile.Read(ref _disconnectedCount);
            int timeoutSnapshot = Volatile.Read(ref _timeoutCount);
            if (connectedSnapshot == 0 && disconnectedSnapshot == 0 && timeoutSnapshot == 0)
                return;

            long startTicks = Interlocked.Read(ref _eventWindowStartTicks);
            long lastEventTicks = Interlocked.Read(ref _eventLastEventTicks);
            if (startTicks == 0 || lastEventTicks == 0)
                return;

            long now = Stopwatch.GetTimestamp();
            double sinceLast = (now - lastEventTicks) / (double)Stopwatch.Frequency;
            double windowSeconds = (lastEventTicks - startTicks) / (double)Stopwatch.Frequency;

            if (!force && sinceLast < QuietGapSeconds && windowSeconds < MaxWindowSeconds)
                return;

            if (!force && Interlocked.CompareExchange(ref _eventLastEventTicks, 0, lastEventTicks) != lastEventTicks)
                return;

            int connects = Interlocked.Exchange(ref _connectedCount, 0);
            int disconnects = Interlocked.Exchange(ref _disconnectedCount, 0);
            int timeouts = Interlocked.Exchange(ref _timeoutCount, 0);
            long lastConnectTicks = Interlocked.Exchange(ref _lastConnectTicks, 0);
            long lastDisconnectTicks = Interlocked.Exchange(ref _lastDisconnectTicks, 0);
            long lastTimeoutTicks = Interlocked.Exchange(ref _lastTimeoutTicks, 0);
            long lastConnectPeerId = Interlocked.Exchange(ref _lastConnectPeerId, 0);
            long lastDisconnectPeerId = Interlocked.Exchange(ref _lastDisconnectPeerId, 0);
            long lastTimeoutPeerId = Interlocked.Exchange(ref _lastTimeoutPeerId, 0);

            if (force)
                Interlocked.Exchange(ref _eventLastEventTicks, 0);

            Interlocked.CompareExchange(ref _eventWindowStartTicks, 0, startTicks);

            double reportSeconds = Math.Max(windowSeconds, 0.01);

            List<(long Tick, Action LogAction)> entries = new(3);
            if (connects > 0)
                entries.Add((lastConnectTicks, () => log(FormatConnectMessage(connects, lastConnectPeerId, reportSeconds))));
            if (disconnects > 0)
                entries.Add((lastDisconnectTicks, () => log(FormatDisconnectMessage(disconnects, lastDisconnectPeerId, reportSeconds))));
            if (timeouts > 0)
                entries.Add((lastTimeoutTicks, () => log(FormatTimeoutMessage(timeouts, lastTimeoutPeerId, reportSeconds))));

            entries.Sort(static (a, b) => a.Tick.CompareTo(b.Tick));
            foreach (var entry in entries)
                entry.LogAction();
        }

        private void MarkEvent(ref long lastEventKindTicks)
        {
            long now = Stopwatch.GetTimestamp();
            if (Interlocked.CompareExchange(ref _eventWindowStartTicks, now, 0) == 0)
                Interlocked.Exchange(ref _eventLastEventTicks, now);
            else
                Interlocked.Exchange(ref _eventLastEventTicks, now);

            Interlocked.Exchange(ref lastEventKindTicks, now);
        }

        private static string FormatCount(string singular, int count)
        {
            if (count == 1)
                return $"1 {singular}";

            return $"{count} {singular}s";
        }

        private static string FormatLastSuffix(int count, double seconds)
        {
            if (count == 1)
                return string.Empty;

            return $" (last {seconds:0.##}s)";
        }

        private static string FormatConnectMessage(int count, long peerId, double seconds)
        {
            if (count == 1)
                return peerId > 0 ? $"Connected to server as peer {peerId}" : "Connected to server";

            return $"{FormatCount("connect event", count)}{FormatLastSuffix(count, seconds)}";
        }

        private static string FormatDisconnectMessage(int count, long peerId, double seconds)
        {
            if (count == 1)
                return peerId > 0 ? $"Disconnected from server (peer {peerId})" : "Disconnected from server";

            return $"{FormatCount("disconnect event", count)}{FormatLastSuffix(count, seconds)}";
        }

        private static string FormatTimeoutMessage(int count, long peerId, double seconds)
        {
            if (count == 1)
                return peerId > 0 ? $"Connection to server timed out (peer {peerId})" : "Connection to server timed out";

            return $"{FormatCount("timeout event", count)}{FormatLastSuffix(count, seconds)}";
        }

    }

    protected void NotifyClientStarting()
    {
        // Intentionally no-op to avoid noisy client lifecycle logs.
    }

    private void NotifyClientStopped()
    {
        // Intentionally no-op to avoid noisy client lifecycle logs.
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
