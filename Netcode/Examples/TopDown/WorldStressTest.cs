using Framework.Netcode;
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Framework.Netcode.Examples.Topdown;

public partial class World
{
    private sealed class WorldStressTest
    {
        private const int TargetClients = 10;
        private const float SpawnIntervalSeconds = 0.1f;
        private const float CircleRadius = 90f;
        private const float AngularSpeed = Mathf.Pi * 2f / 6f;
        private const float SendIntervalSeconds = 0.05f;
        private const ushort DefaultPort = 25565;
        private const int DefaultMaxClients = 100;

        private readonly World _world;
        private readonly List<BotClient> _bots = [];
        private float _spawnAccumulator;
        private bool _started;
        private bool _paused;

        public WorldStressTest(World world)
        {
            _world = world;
        }

        public void Start()
        {
            if (_started)
                return;

            _started = true;
            EnsureServerRunning();
            EnsureLocalClientRunning();
            SpawnBot();
        }

        public void Tick(float deltaSeconds)
        {
            if (!_started)
                return;

            if (!IsServerRunning())
            {
                if (!_paused)
                {
                    StopBots();
                    _paused = true;
                }

                return;
            }

            if (_paused)
            {
                _paused = false;
                _spawnAccumulator = 0f;
                SpawnBot();
            }

            _spawnAccumulator += deltaSeconds;
            while (_bots.Count < TargetClients && _spawnAccumulator >= SpawnIntervalSeconds)
            {
                _spawnAccumulator -= SpawnIntervalSeconds;
                SpawnBot();
            }

            foreach (BotClient bot in _bots)
            {
                bot.Tick(deltaSeconds);
            }
        }

        public void Stop()
        {
            StopBots();
            _started = false;
            _paused = false;
        }

        private void SpawnBot()
        {
            if (_bots.Count >= TargetClients)
                return;

            BotClient bot = new(_world, _world.GetScreenCenter());
            _bots.Add(bot);
        }

        private void EnsureServerRunning()
        {
            Net net = _world._netControlPanel?.Net;
            if (net?.Server == null || net.Server.IsRunning)
                return;

            net.StartServer(DefaultPort, DefaultMaxClients, CreateSilentOptions());
        }

        private void EnsureLocalClientRunning()
        {
            Net net = _world._netControlPanel?.Net;
            if (net?.Client == null || net.Client.IsRunning)
                return;

            Task startTask = net.StartClient("127.0.0.1", DefaultPort);
            _ = startTask.ContinueWith(
                t => GameFramework.Logger.LogErr(t.Exception, "WorldStressTest"),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        private bool IsServerRunning()
        {
            Net net = _world._netControlPanel?.Net;
            return net?.Server != null && net.Server.IsRunning;
        }

        private void StopBots()
        {
            foreach (BotClient bot in _bots)
            {
                bot.Stop();
            }

            _bots.Clear();
        }

        private static ENetOptions CreateSilentOptions()
        {
            return new ENetOptions
            {
                PrintPacketByteSize = false,
                PrintPacketData = false,
                PrintPacketReceived = false,
                PrintPacketSent = false
            };
        }

        private sealed class BotClient
        {
            private readonly GameClient _client;
            private readonly Vector2 _center;
            private float _angle;
            private float _sendAccumulator;
            private bool _sentSpawn;

            public BotClient(World world, Vector2 center)
            {
                _center = center;
                _client = new GameClient();

                Task connectTask = _client.Connect("127.0.0.1", DefaultPort, CreateSilentOptions());
                _ = connectTask.ContinueWith(
                    t => GameFramework.Logger.LogErr(t.Exception, "WorldStressTest"),
                    TaskContinuationOptions.OnlyOnFaulted);
            }

            public void Tick(float deltaSeconds)
            {
                _client.HandlePackets();

                if (!_client.IsConnected)
                    return;

                if (!_sentSpawn)
                {
                    _client.SendPosition(_center);
                    _sentSpawn = true;
                }

                _angle += AngularSpeed * deltaSeconds;
                _sendAccumulator += deltaSeconds;

                if (_sendAccumulator < SendIntervalSeconds)
                    return;

                _sendAccumulator = 0f;
                Vector2 position = _center + new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * CircleRadius;
                _client.SendPosition(position);
            }

            public void Stop()
            {
                if (_client.IsRunning)
                    _client.Stop();
            }
        }
    }
}
