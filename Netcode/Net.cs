using System;
using System.Threading.Tasks;
using Framework.Netcode.Client;
using Framework.Netcode.Server;

namespace Framework.Netcode;

public class Net<TServer> where TServer : ENetServer<TServer>
{
    public event Action<GodotServer<TServer>> ServerCreated;
    public event Action<GodotClient> ClientCreated;

    public static int HeartbeatPosition { get; } = 20;

    public GodotServer<TServer> Server { get; private set; }
    public GodotClient Client { get; private set; }

    private const int ShutdownPollIntervalMs = 50;

    private readonly IGameClientFactory _clientFactory;
    private readonly IGameServerFactory<TServer> _serverFactory;
    private readonly bool _enetInitialized;

    public Net(IGameClientFactory clientFactory, IGameServerFactory<TServer> serverFactory)
    {
        try
        {
            ENet.Library.Initialize();
            _enetInitialized = true;
        }
        catch (DllNotFoundException e)
        {
            GameFramework.Logger.LogErr(e);
            _enetInitialized = false;
        }

        Autoloads.Instance.PreQuit += StopThreads;
        GameFramework.Services.Get<UI.PopupMenu>().MainMenuBtnPressed += async () => await StopThreads();

        _clientFactory = clientFactory;
        _serverFactory = serverFactory;

        Client = clientFactory.CreateClient();
        Server = serverFactory.CreateServer();
    }

    public void StopServer()
    {
        Server.Stop();
    }

    public void StartServer(ushort port, int maxClients, ENetOptions options)
    {
        if (Server.IsRunning)
        {
            Server.Log("Server is running already");
            return;
        }

        Server = _serverFactory.CreateServer();
        ServerCreated?.Invoke(Server);
        Server.Start(port, maxClients, options);
    }

    public async Task StartClient(string ip, ushort port)
    {
        if (Client.IsRunning)
        {
            Client.Log("Client is running already");
            return;
        }

        Client = _clientFactory.CreateClient();

        ClientCreated?.Invoke(Client);

        await Client.Connect(ip, port, new ENetOptions
        {
            PrintPacketByteSize = false,
            PrintPacketData = false,
            PrintPacketReceived = false,
            PrintPacketSent = false
        });
    }

    public void StopClient()
    {
        if (!Client.IsRunning)
        {
            Client.Log("Client was stopped already");
            return;
        }

        Client.Stop();
    }

    private async Task StopThreads()
    {
        // Stop the server and client
        if (_enetInitialized)
        {
            if (Server.IsRunning)
            {
                Server.Stop();

                while (Server.IsRunning)
                {
                    await Task.Delay(ShutdownPollIntervalMs);
                }
            }

            if (Client.IsRunning)
            {
                Client.Stop();

                while (Client.IsRunning)
                {
                    await Task.Delay(ShutdownPollIntervalMs);
                }
            }

            ENet.Library.Deinitialize();
        }

        // Wait for the logger to finish enqueing the remaining logs
        while (GameFramework.Logger.StillWorking())
        {
            await Task.Delay(ShutdownPollIntervalMs);
        }
    }
}
