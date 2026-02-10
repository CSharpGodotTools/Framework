using Framework.Netcode;
using Framework.Netcode.Client;
using Godot;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

public partial class World : Node2D
{
    private const float MoveSpeed = 200f;
    private const float PlayerSize = 18f;
    private const float SendIntervalSeconds = 0.05f;
    private const float SendEpsilonSq = 0.25f;

    private NetControlPanel _netControlPanel;
    private GameClient _client;

    private ColorRect _localPlayer;
    private readonly Dictionary<uint, ColorRect> _remotePlayers = [];
    private float _sendAccumulator;
    private Vector2 _lastSentPosition;

    public override void _Ready()
    {
        _netControlPanel = GetNode<NetControlPanel>("CanvasLayer/Multiplayer");
        _netControlPanel.Net.ClientCreated += OnClientCreated;
        _netControlPanel.Net.ClientDestroyed += OnClientDestroyed;
        SetProcess(false);
    }

    public override void _ExitTree()
    {
        if (_netControlPanel?.Net != null)
        {
            _netControlPanel.Net.ClientCreated -= OnClientCreated;
            _netControlPanel.Net.ClientDestroyed -= OnClientDestroyed;
        }

        DetachClient();
    }

    public override void _Process(double delta)
    {
        float deltaSeconds = (float)delta;
        UpdateLocalMovement(deltaSeconds);
        TrySendPosition(deltaSeconds);
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
        Vector2 center = InitializeLocalPlayerAtCenter();
        _client.SendPosition(center);
        TryEnableProcessing();
    }

    private void OnClientDisconnected(DisconnectOpcode _)
    {
        SetProcess(false);
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
            if (_client != null && _client.HasLocalId && kvp.Key == _client.LocalId)
                continue;

            ColorRect rect = EnsureRemotePlayer(kvp.Key);
            rect.Position = kvp.Value;
        }
    }

    private void UpdateLocalMovement(float deltaSeconds)
    {
        Vector2 input = Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown);
        if (input == Vector2.Zero)
            return;

        _localPlayer.Position += input * MoveSpeed * deltaSeconds;
    }

    private void TrySendPosition(float deltaSeconds)
    {
        _sendAccumulator += deltaSeconds;
        if (_sendAccumulator < SendIntervalSeconds)
            return;

        Vector2 position = _localPlayer.Position;
        if ((position - _lastSentPosition).LengthSquared() < SendEpsilonSq)
            return;

        _sendAccumulator = 0f;
        _lastSentPosition = position;
        _client.SendPosition(position);
    }

    private void DetachClient()
    {
        _client = null;
        SetProcess(false);
        ClearPlayers();
    }

    private void EnsureLocalPlayer()
    {
        if (_localPlayer != null)
            return;

        _localPlayer = CreatePlayerRect(new Color(0.2f, 0.8f, 1f));
        _localPlayer.Name = "LocalPlayer";
        _localPlayer.Position = GetScreenCenter();
        AddChild(_localPlayer);
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
        }
    }

    private void ClearPlayers()
    {
        _sendAccumulator = 0f;
        _localPlayer?.QueueFree();
        _localPlayer = null;

        foreach (ColorRect rect in _remotePlayers.Values)
        {
            rect.QueueFree();
        }

        _remotePlayers.Clear();
    }

    private static ColorRect CreatePlayerRect(Color color)
    {
        return new ColorRect
        {
            Color = color,
            Size = new Vector2(PlayerSize, PlayerSize)
        };
    }

    private Vector2 GetScreenCenter()
    {
        return GetViewportRect().Size * 0.5f;
    }

    private Vector2 InitializeLocalPlayerAtCenter()
    {
        EnsureLocalPlayer();
        Vector2 center = GetScreenCenter();
        _localPlayer.Position = center;
        _lastSentPosition = center;
        _sendAccumulator = 0f;
        return center;
    }

    private void TryEnableProcessing()
    {
        if (_client != null && _client.IsConnected && _localPlayer != null && _client.HasLocalId)
            SetProcess(true);
    }
}
