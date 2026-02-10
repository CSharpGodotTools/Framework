using Framework.Netcode;
using Framework.Netcode.Client;
using Godot;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

public partial class World : Node2D
{
    private const float PlayerSize = 18f;
    private const float RemoteLerpSpeed = 6f;

    private NetControlPanel _netControlPanel;
    private GameClient _client;
    private WorldStressTest _stressTest;

    private LocalPlayer _localPlayer;
    private readonly Dictionary<uint, ColorRect> _remotePlayers = [];
    private readonly Dictionary<uint, Vector2> _remoteTargets = [];

    public override void _Ready()
    {
        _netControlPanel = GetNode<NetControlPanel>("CanvasLayer/Multiplayer");
        _netControlPanel.Net.ClientCreated += OnClientCreated;
        _netControlPanel.Net.ClientDestroyed += OnClientDestroyed;
        SetProcess(false);
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
        DetachClient();
    }

    public override void _Process(double delta)
    {
        float deltaSeconds = (float)delta;
        _localPlayer?.Tick(deltaSeconds);
        UpdateRemotePlayers(deltaSeconds);
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
        EnsureLocalPlayer();
        _localPlayer.ResetAtCenter();
        _stressTest?.Start();
        TryEnableProcessing();
    }

    private void OnClientDisconnected(DisconnectOpcode _)
    {
        ClearPlayers();
    }

    private void OnLocalPlayerReady(uint _)
    {
        EnsureLocalPlayer();
        TryEnableProcessing();
    }

    private void OnRemotePlayerJoined(uint id)
    {
        EnsureRemotePlayer(id);
    }

    private void OnRemotePlayerLeft(uint id)
    {
        RemovePlayer(id);
    }

    private void OnRemotePositionsUpdated(IReadOnlyDictionary<uint, Vector2> positions)
    {
        foreach (KeyValuePair<uint, Vector2> kvp in positions)
        {
            ColorRect rect = EnsureRemotePlayer(kvp.Key);
            if (!_remoteTargets.ContainsKey(kvp.Key))
                rect.Position = kvp.Value;

            _remoteTargets[kvp.Key] = kvp.Value;
        }
    }

    private void DetachClient()
    {
        _client = null;
        if (_stressTest == null || !_stressTest.IsRunning)
            SetProcess(false);
        ClearPlayers();
    }

    private void EnsureLocalPlayer()
    {
        if (_localPlayer != null)
            return;

        _localPlayer = new LocalPlayer(this, _client);
    }

    private ColorRect EnsureRemotePlayer(uint id)
    {
        if (_remotePlayers.TryGetValue(id, out ColorRect rect))
            return rect;

        rect = CreatePlayerRect(new Color(1f, 0.55f, 0.2f));
        rect.Name = $"Player_{id}";
        rect.Position = GetScreenCenter();
        _remotePlayers[id] = rect;
        AddChild(rect);
        return rect;
    }

    private void RemovePlayer(uint id)
    {
        if (_remotePlayers.TryGetValue(id, out ColorRect rect))
        {
            rect.QueueFree();
            _remotePlayers.Remove(id);
            _remoteTargets.Remove(id);
        }
    }

    private void ClearPlayers()
    {
        _localPlayer?.QueueFree();
        _localPlayer = null;

        foreach (ColorRect rect in _remotePlayers.Values)
        {
            rect.QueueFree();
        }

        _remotePlayers.Clear();
        _remoteTargets.Clear();
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
        if (_client != null && _client.IsConnected && _localPlayer != null && _client.HasLocalId)
            SetProcess(true);
    }

    private void UpdateRemotePlayers(float deltaSeconds)
    {
        if (_remoteTargets.Count == 0)
            return;

        float t = 1f - Mathf.Exp(-RemoteLerpSpeed * deltaSeconds);

        foreach (KeyValuePair<uint, Vector2> kvp in _remoteTargets)
        {
            if (_remotePlayers.TryGetValue(kvp.Key, out ColorRect rect))
                rect.Position = rect.Position.Lerp(kvp.Value, t);
        }
    }

    private void StartStressTest()
    {
        _stressTest?.Start();
    }
}
