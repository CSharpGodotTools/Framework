using Framework.Netcode;
using Framework.Netcode.Client;
using Godot;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

public partial class World : Node2D
{
    private const float PlayerSize = 18f;

    private NetControlPanel _netControlPanel;
    private GameClient _client;
    private WorldStressTest _stressTest;
    private LocalPlayer _localPlayer;
    private RemotePlayers _remotePlayers;

    public override void _Ready()
    {
        _netControlPanel = GetNode<NetControlPanel>("CanvasLayer/Multiplayer");
        _netControlPanel.Net.ClientCreated += OnClientCreated;
        _netControlPanel.Net.ClientDestroyed += OnClientDestroyed;
        SetProcess(false);
        _localPlayer = new LocalPlayer(this);
        _remotePlayers = new RemotePlayers(this);
        _stressTest = new WorldStressTest(this);
    }

    public override void _ExitTree()
    {
        if (_netControlPanel?.Net != null)
        {
            _netControlPanel.Net.ClientCreated -= OnClientCreated;
            _netControlPanel.Net.ClientDestroyed -= OnClientDestroyed;
        }

        _stressTest?.Stop();
        _stressTest = null;
        _localPlayer = null;
        _remotePlayers = null;
        DetachClient();
    }

    public override void _Process(double delta)
    {
        float deltaSeconds = (float)delta;
        _localPlayer?.Tick(deltaSeconds);
        _remotePlayers?.Tick(deltaSeconds);
        _stressTest?.Tick(deltaSeconds);
    }

    private void OnClientCreated(GodotClient client)
    {
        if (client is not GameClient gameClient)
            return;

        _client = gameClient;
        _client.Connected += OnClientConnected;
        _client.Disconnected += OnClientDisconnected;
        _client.LocalPlayerReady += OnLocalPlayerReady;
        _client.RemotePlayerJoined += OnRemotePlayerJoined;
        _client.RemotePlayerLeft += OnRemotePlayerLeft;
        _client.RemotePositionsUpdated += OnRemotePositionsUpdated;
        _localPlayer?.AttachClient(gameClient);
    }

    private void OnClientDestroyed(GodotClient client)
    {
        if (client is not GameClient gameClient)
            return;

        gameClient.Connected -= OnClientConnected;
        gameClient.Disconnected -= OnClientDisconnected;
        gameClient.LocalPlayerReady -= OnLocalPlayerReady;
        gameClient.RemotePlayerJoined -= OnRemotePlayerJoined;
        gameClient.RemotePlayerLeft -= OnRemotePlayerLeft;
        gameClient.RemotePositionsUpdated -= OnRemotePositionsUpdated;

        DetachClient();
    }

    private void OnClientConnected()
    {
        _localPlayer?.EnsureLocalPlayer();
        _localPlayer?.ResetAtCenter();
        TryEnableProcessing();
    }

    private void OnClientDisconnected(DisconnectOpcode _)
    {
        ClearPlayers();
    }

    private void OnLocalPlayerReady(uint _)
    {
        _localPlayer?.EnsureLocalPlayer();
        TryEnableProcessing();
    }

    private void OnRemotePlayerJoined(uint id)
    {
        _remotePlayers?.EnsureRemote(id);
    }

    private void OnRemotePlayerLeft(uint id)
    {
        _remotePlayers?.Remove(id);
    }

    private void OnRemotePositionsUpdated(IReadOnlyDictionary<uint, Vector2> positions)
    {
        _remotePlayers?.UpdateTargets(positions);
    }

    private void DetachClient()
    {
        _client = null;
        _localPlayer?.DetachClient();
        if (_stressTest == null || !_stressTest.IsRunning)
            SetProcess(false);
        ClearPlayers();
    }

    internal void ClearRemotePlayers()
    {
        _remotePlayers?.ClearAll();
    }

    private void ClearPlayers()
    {
        _localPlayer?.Clear();
        _remotePlayers?.ClearAll();
    }

    public static ColorRect CreatePlayerRect(Color color)
    {
        return new ColorRect
        {
            Color = color,
            Size = new Vector2(PlayerSize, PlayerSize)
        };
    }

    public Vector2 GetScreenCenter()
    {
        return GetViewportRect().Size * 0.5f;
    }

    private void TryEnableProcessing()
    {
        if (_client != null && _client.IsConnected && _localPlayer != null && _localPlayer.HasLocalPlayer && _client.HasLocalId)
            SetProcess(true);
    }

    private void StartStressTest()
    {
        _stressTest?.Start();
    }
}
