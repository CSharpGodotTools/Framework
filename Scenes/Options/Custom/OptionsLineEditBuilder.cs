using System;

namespace Framework.UI;

public sealed class OptionsLineEditBuilder
{
    private string _label;
    private Func<string> _getValue;
    private Action<string> _setValue;
    private string _placeholder = string.Empty;
    private bool _hasDefault;
    private string _defaultValue = string.Empty;

    internal int OrderValue { get; private set; }
    internal bool TrackInResourceValue { get; private set; } = true;

    public OptionsLineEditBuilder Label(string label)
    {
        _label = label;
        return this;
    }

    public OptionsLineEditBuilder Value(Func<string> getValue, Action<string> setValue)
    {
        _getValue = getValue;
        _setValue = setValue;
        return this;
    }

    public OptionsLineEditBuilder Default(string defaultValue)
    {
        _hasDefault = true;
        _defaultValue = defaultValue ?? string.Empty;
        return this;
    }

    public OptionsLineEditBuilder Placeholder(string placeholder)
    {
        _placeholder = placeholder ?? string.Empty;
        return this;
    }

    public OptionsLineEditBuilder WithOrder(int order)
    {
        OrderValue = order;
        return this;
    }

    internal OptionsLineEditBuilder TrackInResource(bool trackInResource)
    {
        TrackInResourceValue = trackInResource;
        return this;
    }

    internal OptionsLineEditRegistration Build()
    {
        if (string.IsNullOrWhiteSpace(_label))
            throw new ArgumentException("LineEdit label cannot be empty.");

        if ((_getValue == null) != (_setValue == null))
            throw new ArgumentException("LineEdit getter and setter must either both be set or both be omitted.");

        return new OptionsLineEditRegistration(
            _label,
            _getValue,
            _setValue,
            _hasDefault,
            _defaultValue,
            TrackInResourceValue,
            _placeholder,
            OrderValue);
    }
}

internal readonly record struct OptionsLineEditRegistration(
    string Label,
    Func<string> GetValue,
    Action<string> SetValue,
    bool HasDefault,
    string DefaultValue,
    bool TrackInResource,
    string Placeholder,
    int Order);
