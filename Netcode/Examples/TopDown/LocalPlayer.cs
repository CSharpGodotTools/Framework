using Godot;

namespace Framework.Netcode.Examples.Topdown;

public sealed class LocalPlayer
{
    private const float MoveSpeed = 200f;
    private const float SendIntervalSeconds = 0.05f;
    private const float SendEpsilonSq = 0.25f;

    private readonly World _world;
    private readonly GameClient _client;
    private readonly ColorRect _node;
    private float _sendAccumulator;
    private Vector2 _lastSentPosition;

    public LocalPlayer(World world, GameClient client)
    {
        _world = world;
        _client = client;
        _node = World.CreatePlayerRect(new Color(0.2f, 0.8f, 1f));
        _node.Name = "LocalPlayer";
        _node.Position = _world.GetScreenCenter();
        _world.AddChild(_node);
        _lastSentPosition = _node.Position;
    }

    public void ResetAtCenter()
    {
        Vector2 center = _world.GetScreenCenter();
        _node.Position = center;
        _lastSentPosition = center;
        _sendAccumulator = 0f;
        _client.SendPosition(center);
    }

    public void Tick(float deltaSeconds)
    {
        UpdateMovement(deltaSeconds);
        TrySendPosition(deltaSeconds);
    }

    public void QueueFree()
    {
        _node.QueueFree();
    }

    private void UpdateMovement(float deltaSeconds)
    {
        Vector2 input = Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown);
        if (input == Vector2.Zero)
            return;

        _node.Position += input * MoveSpeed * deltaSeconds;
    }

    private void TrySendPosition(float deltaSeconds)
    {
        _sendAccumulator += deltaSeconds;
        if (_sendAccumulator < SendIntervalSeconds)
            return;

        Vector2 position = _node.Position;
        if ((position - _lastSentPosition).LengthSquared() < SendEpsilonSq)
            return;

        _sendAccumulator = 0f;
        _lastSentPosition = position;
        _client.SendPosition(position);
    }
}
