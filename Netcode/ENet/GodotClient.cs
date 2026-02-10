using ENet;
using GodotUtils;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.Netcode.Client;

public abstract class GodotClient : ENetClient
{
    private readonly ConcurrentDictionary<Type, Action<ServerPacket>> _serverPacketHandlers = new();

    protected void RegisterPacketHandler<TPacket>(Action<TPacket> handler)
        where TPacket : ServerPacket
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _serverPacketHandlers[typeof(TPacket)] = (packet) => handler((TPacket)packet);
    }

    /// <summary>
    /// Fires when the client connects to the server. Thread safe.
    /// </summary>
    public event Action Connected;

    /// <summary>
    /// Fires when the client disconnects or times out from the server. Thread safe.
    /// </summary>
    public event Action<DisconnectOpcode> Disconnected;

    /// <summary>
    /// Fires when the client times out from the server. Thread safe.
    /// </summary>
    public event Action Timedout;

    /// <summary>
    /// Is the client connected to the server? Thread safe.
    /// </summary>
    public bool IsConnected => Interlocked.Read(ref _connected) == 1;

    /// <summary>
    /// <para>
    /// A thread safe way to connect to the server. IP can be set to "127.0.0.1" for 
    /// localhost and port can be set to something like 25565.
    /// </para>
    /// 
    /// <para>
    /// Options contains settings for enabling certain logging features and ignored 
    /// packets are packets that do not get logged to the console.
    /// </para>
    /// </summary>
    public async Task Connect(string ip, ushort port, ENetOptions options = default, params Type[] ignoredPackets)
    {
        Options = options ?? new ENetOptions();

        NotifyClientStarting();
        InitIgnoredPackets(ignoredPackets);

        Interlocked.Exchange(ref _running, 1);
        CTS = new CancellationTokenSource();
        
        try
        {
            await Task.Factory.StartNew(
                () => WorkerThread(ip, port),
                CTS.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping the client.
        }
        catch (Exception e)
        {
            GameFramework.Logger.LogErr(e, "Client");
        }
    }

    /// <summary>
    /// Stop the client. This function is thread safe.
    /// </summary>
    public sealed override void Stop()
    {
        if (!IsRunning)
        {
            Log("Client has stopped already");
            return;
        }

        ENetCmds.Enqueue(new Cmd<ENetClientOpcode>(ENetClientOpcode.Disconnect));
    }

    /// <summary>
    /// Send a packet to the server. Packets are defined to be reliable by default. This
    /// function is thread safe.
    /// </summary>
    public void Send(ClientPacket packet)
    {
        if (!IsConnected)
        {
            Log($"Can not send packet '{packet.GetType()}' because client is not connected to the server");
            return;
        }

        packet.Write();
        packet.SetPeer(_peer);
        Outgoing.Enqueue(packet);
    }

    /// <summary>
    /// This function should be called in the _PhysicsProcess in the Godot thread. 
    /// </summary>
    public void HandlePackets()
    {
        ProcessGodotPackets();
        ProcessGodotCommands();
    }

    private void ProcessGodotPackets()
    {
        while (GodotPackets.TryDequeue(out PacketData packetData))
        {
            PacketReader packetReader = packetData.PacketReader;
            ServerPacket serverPacket = packetData.HandlePacket;
            Type type = packetData.Type;

            try
            {
                serverPacket.Read(packetReader);

                if (!_serverPacketHandlers.TryGetValue(type, out Action<ServerPacket> handler))
                {
                    Log($"No handler registered for server packet {type.Name} (Ignoring)");
                    continue;
                }

                handler(serverPacket);
                LogReceivedPacket(type, serverPacket);
            }
            catch (Exception e)
            {
                GameFramework.Logger.LogErr(e, "Client");
            }
            finally
            {
                packetReader.Dispose();
            }
        }
    }

    private void LogReceivedPacket(Type type, ServerPacket packet)
    {
        if (!Options.PrintPacketReceived || IgnoredPackets.Contains(type))
            return;

        string packetData = Options.PrintPacketData ? $"\n{packet.ToFormattedString()}" : string.Empty;
        Log($"Received packet: {type.Name}{packetData}");
    }

    private void ProcessGodotCommands()
    {
        while (GodotCmdsInternal.TryDequeue(out Cmd<GodotOpcode> cmd))
        {
            GodotOpcode opcode = cmd.Opcode;

            switch (opcode)
            {
                case GodotOpcode.Connected:
                    TryInvoke(() => Connected?.Invoke(), "Client");
                    break;

                case GodotOpcode.Disconnected:
                {
                    DisconnectOpcode disconnectOpcode = (DisconnectOpcode)cmd.Data[0];
                    TryInvoke(() => Disconnected?.Invoke(disconnectOpcode), "Client");
                    break;
                }

                case GodotOpcode.Timeout:
                    TryInvoke(() => Timedout?.Invoke(), "Client");
                    break;
            }
        }
    }

    private static void TryInvoke(Action action, string tag)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            GameFramework.Logger.LogErr(e, tag);
        }
    }
}
