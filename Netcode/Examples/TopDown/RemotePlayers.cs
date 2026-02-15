using Godot;
using System.Collections.Generic;

namespace Framework.Netcode.Examples.Topdown;

internal sealed class RemotePlayers
{
    private const float RemoteLerpSpeed = 6f;

    private readonly World _world;
    private readonly Dictionary<uint, ColorRect> _players = [];
    private readonly Dictionary<uint, Vector2> _targets = [];

    public RemotePlayers(World world)
    {
        _world = world;
    }

    public void EnsureRemote(uint id)
    {
        EnsurePlayerNode(id);
    }

    public void Remove(uint id)
    {
        if (_players.Remove(id, out ColorRect rect))
        {
            rect.QueueFree();
        }

        _targets.Remove(id);
    }

    public void ClearAll()
    {
        foreach (ColorRect rect in _players.Values)
        {
            rect.QueueFree();
        }

        _players.Clear();
        _targets.Clear();
    }

    public void UpdateTargets(IReadOnlyDictionary<uint, Vector2> positions)
    {
        foreach (KeyValuePair<uint, Vector2> kvp in positions)
        {
            ColorRect rect = EnsurePlayerNode(kvp.Key);
            if (!_targets.ContainsKey(kvp.Key))
            {
                rect.Position = kvp.Value;
            }

            _targets[kvp.Key] = kvp.Value;
        }
    }

    public void Tick(float deltaSeconds)
    {
        if (_targets.Count > 0)
        {
            float interpolation = 1f - Mathf.Exp(-RemoteLerpSpeed * deltaSeconds);

            foreach (KeyValuePair<uint, Vector2> kvp in _targets)
            {
                if (_players.TryGetValue(kvp.Key, out ColorRect rect))
                {
                    rect.Position = rect.Position.Lerp(kvp.Value, interpolation);
                }
            }
        }
    }

    private ColorRect EnsurePlayerNode(uint id)
    {
        if (!_players.TryGetValue(id, out ColorRect rect))
        {
            rect = World.CreatePlayerRect(new Color(1f, 0.55f, 0.2f));
            rect.Name = $"Player_{id}";
            rect.Position = _world.GetScreenCenter();
            _players[id] = rect;
            _world.AddChild(rect);
        }

        return rect;
    }
}
