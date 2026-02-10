using ENet;
using GodotUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Framework.Netcode.Server;

// ENet API Reference: https://github.com/SoftwareGuy/ENet-CSharp/blob/master/DOCUMENTATION.md
public abstract class ENetServer : ENetLow
{
    protected ConcurrentQueue<Cmd<ENetServerOpcode>> ENetCmds { get; } = new();

    private readonly ConcurrentQueue<(Packet, Peer)> _incoming = new();
    private readonly ConcurrentQueue<ServerPacket> _outgoing = new();

    private readonly ConcurrentDictionary<Type, Action<ClientPacket, Peer>> _clientPacketHandlers = new();

    protected void RegisterPacketHandler<TPacket>(Action<TPacket, Peer> handler) 
        where TPacket : ClientPacket
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _clientPacketHandlers[typeof(TPacket)] = (packet, peer) => handler((TPacket)packet, peer);
    }

    /// <summary>
    /// This Dictionary is NOT thread safe and should only be accessed on the ENet Thread
    /// </summary>
    private readonly Dictionary<uint, Peer> _peers = [];

    private readonly ServerLogAggregator _logAggregator = new();

    /// <summary>
    /// Log a message as the server. This function is thread safe.
    /// </summary>
    public sealed override void Log(object message, BBColor color = BBColor.Gray)
    {
        string timestamp = string.Empty;
        if (Options != null && Options.ShowLogTimestamps)
            timestamp = $"[{DateTime.Now:HH:mm:ss}] ";

        GameFramework.Logger.Log($"{timestamp}[Server] {message}", color);
    }

    /// <summary>
    /// Kick everyone on the server with a specified opcode. Thread safe.
    /// </summary>
    public void KickAll(DisconnectOpcode opcode)
    {
        ENetCmds.Enqueue(new Cmd<ENetServerOpcode>(ENetServerOpcode.KickAll, opcode));
    }

    protected void EnqueuePacket(ServerPacket packet)
    {
        _outgoing.Enqueue(packet);
    }

    protected sealed override void ConcurrentQueues()
    {
        ProcessEnetCommands();
        ProcessIncomingPackets();
        ProcessOutgoingPackets();
        _logAggregator.Flush(message => Log(message));
    }

    protected sealed override void OnConnectLow(Event netEvent)
    {
        _peers[netEvent.Peer.ID] = netEvent.Peer;
        _logAggregator.RecordConnect(netEvent.Peer.ID);
    }

    protected virtual void OnPeerDisconnect(Event netEvent) { }

    protected sealed override void OnDisconnectLow(Event netEvent)
    {
        _peers.Remove(netEvent.Peer.ID);
        TryInvokePeerDisconnect(netEvent);
        _logAggregator.RecordDisconnect(netEvent.Peer.ID);
    }

    protected sealed override void OnTimeoutLow(Event netEvent)
    {
        _peers.Remove(netEvent.Peer.ID);
        TryInvokePeerDisconnect(netEvent);
        _logAggregator.RecordTimeout(netEvent.Peer.ID);
    }

    protected sealed override void OnReceiveLow(Event netEvent)
    {
        Packet packet = netEvent.Packet;

        if (packet.Length > GamePacket.MaxSize)
        {
            Log($"Tried to read packet from client of size {packet.Length} when max packet size is {GamePacket.MaxSize}");
            packet.Dispose();
            return;
        }

        _incoming.Enqueue((packet, netEvent.Peer));
    }

    protected void WorkerThread(ushort port, int maxClients)
    {
        Host = CreateServerHost(port, maxClients);

        if (Host == null)
            return;

        Interlocked.Exchange(ref _running, 1);
        Log("Server is running");

        try
        {
            WorkerLoop();
        }
        finally
        {
            Host.Dispose();
        }
        
        Log("Server has stopped");
    }

    protected sealed override void OnDisconnectCleanup(Peer peer)
    {
        base.OnDisconnectCleanup(peer);
        _peers.Remove(peer.ID);
    }

    /// <returns>Host or null if failed to create host</returns>
    private Host CreateServerHost(ushort port, int maxClients)
    {
        Host host = new();

        try
        {
            host.Create(new Address { Port = port }, maxClients);
        }
        catch (InvalidOperationException e)
        {
            Log($"A server is running on port {port} already! {e.Message}");
            return null;
        }

        return host;
    }

    private void ProcessEnetCommands()
    {
        while (ENetCmds.TryDequeue(out Cmd<ENetServerOpcode> cmd))
        {
            switch (cmd.Opcode)
            {
                case ENetServerOpcode.Stop:
                    HandleStopCommand();
                    break;

                case ENetServerOpcode.Kick:
                    HandleKickCommand(cmd);
                    break;

                case ENetServerOpcode.KickAll:
                    HandleKickAllCommand(cmd);
                    break;
            }
        }
    }

    private void HandleStopCommand()
    {
        KickAll(DisconnectOpcode.Stopping);

        if (CTS.IsCancellationRequested)
        {
            Log("Server is in the middle of stopping");
            return;
        }

        CTS.Cancel();
    }

    private void HandleKickCommand(Cmd<ENetServerOpcode> cmd)
    {
        uint id = (uint)cmd.Data[0];
        DisconnectOpcode opcode = (DisconnectOpcode)cmd.Data[1];

        if (!_peers.TryGetValue(id, out Peer peer))
        {
            Log($"Tried to kick peer with id '{id}' but this peer does not exist");
            return;
        }

        peer.DisconnectNow((uint)opcode);
        _peers.Remove(id);
    }

    private void HandleKickAllCommand(Cmd<ENetServerOpcode> cmd)
    {
        DisconnectOpcode opcode = (DisconnectOpcode)cmd.Data[0];

        foreach (Peer peer in _peers.Values)
        {
            peer.DisconnectNow((uint)opcode);
        }

        _peers.Clear();
    }

    private void ProcessIncomingPackets()
    {
        while (_incoming.TryDequeue(out (Packet ENetPacket, Peer Peer) packetPeer))
        {
            PacketReader reader = new(packetPeer.ENetPacket);

            try
            {
                if (!TryGetPacketAndType(reader, out ClientPacket clientPacket, out Type type))
                    continue;

                if (!TryReadPacket(clientPacket, reader, out string err))
                {
                    Log($"Received malformed packet: {err} (Ignoring)");
                    continue;
                }

                if (!_clientPacketHandlers.TryGetValue(type, out Action<ClientPacket, Peer> handler))
                {
                    Log($"No handler registered for client packet {type.Name} (Ignoring)");
                    continue;
                }

                try
                {
                    handler(clientPacket, packetPeer.Peer);
                }
                catch (Exception e)
                {
                    GameFramework.Logger.LogErr(e, "Server");
                    continue;
                }

                LogPacketReceived(type, packetPeer.Peer.ID, clientPacket);
            }
            finally
            {
                reader.Dispose();
            }
        }
    }

    private bool TryGetPacketAndType(PacketReader packetReader, out ClientPacket clientPacket, out Type type)
    {
        // The reader is positioned at start of packet when constructed
        byte opcode = packetReader.ReadByte();

        if (!PacketRegistry.ClientPacketTypes.TryGetValue(opcode, out type))
        {
            Log($"Received malformed opcode: {opcode} (Ignoring)");
            clientPacket = null;
            return false;
        }

        clientPacket = PacketRegistry.ClientPacketInfo[type].Instance;
        return true;
    }

    private static bool TryReadPacket(ClientPacket clientPacket, PacketReader packetReader, out string error)
    {
        try
        {
            clientPacket.Read(packetReader);
            error = "No error";
            return true;
        }
        catch (EndOfStreamException e)
        {
            error = e.Message;
            return false;
        }
    }

    private void LogPacketReceived(Type type, uint clientId, ClientPacket packet)
    {
        if (!Options.PrintPacketReceived || IgnoredPackets.Contains(type))
            return;

        string packetData = string.Empty;
        if (Options.PrintPacketData)
            packetData = $"\n{packet.ToFormattedString()}";
        Log($"Received packet: {type.Name} from client {clientId}{packetData}");
    }

    private void TryInvokePeerDisconnect(Event netEvent)
    {
        try
        {
            OnPeerDisconnect(netEvent);
        }
        catch (Exception e)
        {
            GameFramework.Logger.LogErr(e, "Server");
        }
    }

    private void ProcessOutgoingPackets()
    {
        while (_outgoing.TryDequeue(out ServerPacket packet))
        {
            try
            {
                SendType sendType = packet.GetSendType();

                switch (sendType)
                {
                    case SendType.Peer:
                        packet.Send();
                        break;

                    case SendType.Broadcast:
                        packet.Broadcast(Host);
                        break;
                }
            }
            catch (Exception e)
            {
                GameFramework.Logger.LogErr(e, "Server");
            }
        }
    }

    private sealed class ServerLogAggregator
    {
        private const double QuietGapSeconds = 0.5;
        private const double MaxWindowSeconds = 5.0;

        private int _connectedCount;
        private int _disconnectedCount;
        private int _timeoutCount;

        private long _windowStartTicks;
        private long _lastEventTicks;

        private long _lastConnectTicks;
        private long _lastDisconnectTicks;
        private long _lastTimeoutTicks;
        private uint _lastConnectPeerId;
        private uint _lastDisconnectPeerId;
        private uint _lastTimeoutPeerId;

        public void RecordConnect(uint peerId)
        {
            _connectedCount++;
            _lastConnectPeerId = peerId;
            MarkEvent(ref _lastConnectTicks);
        }

        public void RecordDisconnect(uint peerId)
        {
            _disconnectedCount++;
            _lastDisconnectPeerId = peerId;
            MarkEvent(ref _lastDisconnectTicks);
        }

        public void RecordTimeout(uint peerId)
        {
            _timeoutCount++;
            _lastTimeoutPeerId = peerId;
            MarkEvent(ref _lastTimeoutTicks);
        }

        public void Flush(Action<string> log)
        {
            if (_connectedCount == 0 && _disconnectedCount == 0 && _timeoutCount == 0)
                return;

            if (_windowStartTicks == 0 || _lastEventTicks == 0)
                return;

            long now = Stopwatch.GetTimestamp();
            double sinceLast = (now - _lastEventTicks) / (double)Stopwatch.Frequency;
            double windowSeconds = (_lastEventTicks - _windowStartTicks) / (double)Stopwatch.Frequency;

            if (sinceLast < QuietGapSeconds && windowSeconds < MaxWindowSeconds)
                return;

            int connects = _connectedCount;
            int disconnects = _disconnectedCount;
            int timeouts = _timeoutCount;
            long lastConnectTicks = _lastConnectTicks;
            long lastDisconnectTicks = _lastDisconnectTicks;
            long lastTimeoutTicks = _lastTimeoutTicks;
            uint lastConnectPeerId = _lastConnectPeerId;
            uint lastDisconnectPeerId = _lastDisconnectPeerId;
            uint lastTimeoutPeerId = _lastTimeoutPeerId;

            _connectedCount = 0;
            _disconnectedCount = 0;
            _timeoutCount = 0;
            _windowStartTicks = 0;
            _lastEventTicks = 0;
            _lastConnectTicks = 0;
            _lastDisconnectTicks = 0;
            _lastTimeoutTicks = 0;
            _lastConnectPeerId = 0;
            _lastDisconnectPeerId = 0;
            _lastTimeoutPeerId = 0;

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
            if (_windowStartTicks == 0)
                _windowStartTicks = now;

            _lastEventTicks = now;
            lastEventKindTicks = now;
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

        private static string FormatConnectMessage(int count, uint peerId, double seconds)
        {
            if (count == 1)
                return $"Client with id {peerId} connected";

            return $"{FormatCount("client", count)} connected{FormatLastSuffix(count, seconds)}";
        }

        private static string FormatDisconnectMessage(int count, uint peerId, double seconds)
        {
            if (count == 1)
                return $"Client with id {peerId} disconnected";

            return $"{FormatCount("client", count)} disconnected{FormatLastSuffix(count, seconds)}";
        }

        private static string FormatTimeoutMessage(int count, uint peerId, double seconds)
        {
            if (count == 1)
                return $"Client with id {peerId} timed out";

            return $"{FormatCount("client", count)} timed out{FormatLastSuffix(count, seconds)}";
        }
    }
}
