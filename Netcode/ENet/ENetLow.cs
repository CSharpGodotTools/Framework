using ENet;
using System.Collections.Generic;
using System.Threading;
using System;

namespace Framework.Netcode;

/// <summary>
/// ENetServer and ENetClient both extend from this class.
/// </summary>
public abstract class ENetLow
{
    // Properties
    protected Host Host { get; set; }
    protected CancellationTokenSource CTS { get; set; }
    protected ENetOptions Options { get; set; }
    protected HashSet<Type> IgnoredPackets { get; private set; } = [];
    
    // Fields
    protected long _running; // Interlocked.Read requires this to be a field

    // Methods
    public bool IsRunning => Interlocked.Read(ref _running) == 1;
    public abstract void Log(object message, BBColor color);
    public abstract void Stop();

    protected virtual void OnDisconnectCleanup(Peer peer)
    {
        CTS.Cancel();
    }

    protected void InitIgnoredPackets(Type[] ignoredPackets)
    {
        IgnoredPackets = new HashSet<Type>(ignoredPackets);
    }

    protected void WorkerLoop()
    {
        while (!CTS.IsCancellationRequested)
        {
            bool polled = false;

            ConcurrentQueues();

            while (!polled)
            {
                if (Host.CheckEvents(out Event netEvent) <= 0)
                {
                    if (Host.Service(15, out netEvent) <= 0)
                    {
                        break;
                    }

                    polled = true;
                }

                switch (netEvent.Type)
                {
                    case EventType.None:
                        // do nothing
                        break;
                    case EventType.Connect:
                        OnConnectLow(netEvent);
                        break;
                    case EventType.Disconnect:
                        OnDisconnectLow(netEvent);
                        break;
                    case EventType.Timeout:
                        OnTimeoutLow(netEvent);
                        break;
                    case EventType.Receive:
                        OnReceiveLow(netEvent);
                        break;
                }
            }
        }

        Host.Flush();
        _running = 0;
    }

    protected abstract void ConcurrentQueues();
    protected abstract void OnConnectLow(Event netEvent);
    protected abstract void OnDisconnectLow(Event netEvent);
    protected abstract void OnTimeoutLow(Event netEvent);
    protected abstract void OnReceiveLow(Event netEvent);

    /// <summary>
    /// Formats the number of bytes into a readable string. For example if <paramref name="bytes"/>
    /// is 1 then "1 byte" is returned. If <paramref name="bytes"/> is 2 then "2 bytes" is returned.
    /// An empty string is returned if printing the packet size is disabled in the options.
    /// </summary>
    protected string FormatByteSize(long bytes)
    {
        return Options.PrintPacketByteSize ? $"({bytes} byte{(bytes == 1 ? "" : "s")}) " : "";
    }
}
