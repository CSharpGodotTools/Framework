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
    private uint _localId;
    private bool _hasLocalId;

    public uint PeerId => _peer.ID;
    public bool HasLocalId => _hasLocalId;
    public uint LocalId => _localId;

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
        _hasLocalId = false;
        _localId = 0;
        _remotePositions.Clear();
    }

    public void SendPosition(Vector2 position)
    {
        Send(new CPacketPlayerPosition { Position = position });
    }

    private void OnPlayerJoinedLeaved(SPacketPlayerJoinedLeaved packet)
    {
        uint id = packet.Id;

        if (packet.Joined)
        {
            // The first join packet is always our own server-assigned id.
            if (!_hasLocalId)
            {
                _localId = id;
                _hasLocalId = true;
                LocalPlayerReady?.Invoke(id);
                return;
            }

            if (id == _localId)
            {
                LocalPlayerReady?.Invoke(id);
                return;
            }

            RemotePlayerJoined?.Invoke(id);
            return;
        }

        if (_hasLocalId && id == _localId)
            return;

        _remotePositions.Remove(id);
        RemotePlayerLeft?.Invoke(id);
    }

    private void OnPlayerPositions(SPacketPlayerPositions packet)
    {
        _remotePositions.Clear();

        foreach (KeyValuePair<uint, Vector2> kvp in packet.Positions)
        {
            if (_hasLocalId && kvp.Key == _localId)
                continue;

            _remotePositions[kvp.Key] = kvp.Value;
        }

        RemotePositionsUpdated?.Invoke(new Dictionary<uint, Vector2>(_remotePositions));
    }
}
