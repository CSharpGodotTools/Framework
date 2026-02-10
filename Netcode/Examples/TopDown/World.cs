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
    private uint _localId;
    private bool _hasLocalId;

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
        _client.PlayerJoinedLeaved += OnPlayerJoinedLeaved;
        _client.PositionsUpdated += OnPositionsUpdated;
    }

    private void OnClientDestroyed(GodotClient client)
    {
        if (client is not GameClient gameClient)
            return;

        gameClient.Connected -= OnClientConnected;
        gameClient.Disconnected -= OnClientDisconnected;
        gameClient.PlayerJoinedLeaved -= OnPlayerJoinedLeaved;
        gameClient.PositionsUpdated -= OnPositionsUpdated;

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

    private void OnPlayerJoinedLeaved(uint id, bool joined)
    {
        if (joined)
        {
            // First joined packet we receive is our own server-assigned id.
            // Cache it so we can distinguish local vs remote players.
            if (!_hasLocalId)
            {
                _localId = id;
                _hasLocalId = true;
                EnsureLocalPlayer();
                TryEnableProcessing();
                return;
            }

            // Ignore a redundant join message for ourselves.
            if (id == _localId)
            {
                EnsureLocalPlayer();
                return;
            }

            // Any other join belongs to a remote player.
            EnsureRemotePlayer(id);
        }
        else
        {
            // Remove player visuals on leave.
            RemovePlayer(id);
        }
    }

    private void OnPositionsUpdated(Dictionary<uint, Vector2> positions)
    {
        // Apply authoritative positions for all known players.
        foreach (KeyValuePair<uint, Vector2> kvp in positions)
        {
            uint id = kvp.Key;
            Vector2 position = kvp.Value;

            if (id == _localId)
            {
                // Update local visuals too; server positions may include corrections.
                if (_localPlayer != null)
                {
                    _localPlayer.Position = position;
                }

                continue;
            }

            // Ensure remote placeholder exists, then update its position.
            ColorRect rect = EnsureRemotePlayer(id);
            rect.Position = position;
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
        _localId = 0;
        _hasLocalId = false;
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
        if (id == _localId)
        {
            _localPlayer?.QueueFree();
            _localPlayer = null;
            return;
        }

        if (_remotePlayers.TryGetValue(id, out ColorRect rect))
        {
            rect.QueueFree();
            _remotePlayers.Remove(id);
        }
    }

    private void ClearPlayers()
    {
        _sendAccumulator = 0f;
        _hasLocalId = false;
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
        if (_client != null && _client.IsConnected && _localPlayer != null && _hasLocalId)
            SetProcess(true);
    }
}
