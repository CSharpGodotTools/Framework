using Godot;

namespace Framework.Netcode.Examples.Topdown;

internal sealed class LocalPlayer
{
    private const float MoveSpeed = 200f;
    private const float SendIntervalSeconds = 0.05f;
    private const float SendEpsilonSq = 0.25f;

    private readonly World _world;
    private GameClient _client;
    private ColorRect _node;
    private float _sendAccumulator;
    private Vector2 _lastSentPosition;

    public bool HasLocalPlayer => _node != null;

    public LocalPlayer(World world)
    {
        _world = world;
    }

    public void AttachClient(GameClient client)
    {
        _client = client;
    }

    public void DetachClient()
    {
        _client = null;
        Clear();
    }

    public void EnsureLocalPlayer()
    {
        if (_node != null || _client == null)
            return;

        _node = World.CreatePlayerRect(new Color(0.2f, 0.8f, 1f));
        _node.Name = "LocalPlayer";
        _node.Position = _world.GetScreenCenter();
        _world.AddChild(_node);
        _lastSentPosition = _node.Position;
        _sendAccumulator = 0f;
    }

    public void ResetAtCenter()
    {
        if (_node == null || _client == null)
            return;

        Vector2 center = _world.GetScreenCenter();
        _node.Position = center;
        _lastSentPosition = center;
        _sendAccumulator = 0f;
        _client.SendPosition(center);
    }

    public void Tick(float deltaSeconds)
    {
        if (_node == null || _client == null)
            return;

        UpdateMovement(deltaSeconds);
        TrySendPosition(deltaSeconds);
    }

    public void Clear()
    {
        _node?.QueueFree();
        _node = null;
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
