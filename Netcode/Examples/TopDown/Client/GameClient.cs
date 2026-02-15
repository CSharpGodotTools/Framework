using ENet;
using Framework.Netcode.Client;
using Godot;
using System;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

public partial class GameClient : GodotClient
{
    public event Action<uint> LocalPlayerReady;
    public event Action<uint> RemotePlayerJoined;
    public event Action<uint> RemotePlayerLeft;
    public event Action<IReadOnlyDictionary<uint, Vector2>> RemotePositionsUpdated;

    private readonly Dictionary<uint, Vector2> _remotePositions = [];
    private readonly Dictionary<uint, Vector2> _pendingPositions = [];

    public GameClient()
    {
        RegisterPacketHandler<SPacketPlayerJoinedLeaved>(OnPlayerJoinedLeaved);
        RegisterPacketHandler<SPacketPlayerPositions>(OnPlayerPositions);
    }

    protected override void OnConnect(Event netEvent)
    {
        Send(new CPacketPlayerJoinLeave { Joined = true });
    }

    protected override void OnDisconnect(Event netEvent)
    {
        _remotePositions.Clear();
        _pendingPositions.Clear();
    }

    public void SendPosition(Vector2 position)
    {
        Send(new CPacketPlayerPosition { Position = position });
    }

    private void OnPlayerJoinedLeaved(SPacketPlayerJoinedLeaved packet)
    {
        if (packet.Joined)
        {
            HandlePlayerJoined(packet);
            return;
        }

        HandlePlayerLeft(packet.Id);
    }

    private void OnPlayerPositions(SPacketPlayerPositions packet)
    {
        if (!HasLocalId)
        {
            CachePendingPositions(packet.Positions);
            return;
        }

        ApplyRemotePositions(packet.Positions);
    }

    private void HandlePlayerJoined(SPacketPlayerJoinedLeaved packet)
    {
        if (packet.IsLocal)
        {
            if (TrySetLocalId(packet.Id))
            {
                LocalPlayerReady?.Invoke(LocalId);
                FlushPendingPositions();
            }

            return;
        }

        if (HasLocalId && packet.Id == LocalId)
        {
            return;
        }

        RemotePlayerJoined?.Invoke(packet.Id);
    }

    private void HandlePlayerLeft(uint id)
    {
        if (HasLocalId && id == LocalId)
        {
            return;
        }

        _remotePositions.Remove(id);
        _pendingPositions.Remove(id);
        RemotePlayerLeft?.Invoke(id);
    }

    private void CachePendingPositions(IReadOnlyDictionary<uint, Vector2> positions)
    {
        _pendingPositions.Clear();
        foreach (KeyValuePair<uint, Vector2> kvp in positions)
        {
            _pendingPositions[kvp.Key] = kvp.Value;
        }
    }

    private void FlushPendingPositions()
    {
        if (_pendingPositions.Count == 0)
            return;

        ApplyRemotePositions(_pendingPositions);
        _pendingPositions.Clear();
    }

    private void ApplyRemotePositions(IReadOnlyDictionary<uint, Vector2> positions)
    {
        uint localId = LocalId;
        _remotePositions.Clear();

        foreach (KeyValuePair<uint, Vector2> kvp in positions)
        {
            if (kvp.Key == localId)
            {
                continue;
            }

            _remotePositions[kvp.Key] = kvp.Value;
        }

        RemotePositionsUpdated?.Invoke(new Dictionary<uint, Vector2>(_remotePositions));
    }
}
