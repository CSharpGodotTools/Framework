using System;
using System.Collections.Generic;

namespace Framework.UI;

public sealed class OptionsDropdownBuilder
{
    private string _label;
    private Func<int> _getValue;
    private Action<int> _setValue;
    private readonly List<string> _items = [];
    private bool _hasDefault;
    private int _defaultValue;

    internal int OrderValue { get; private set; }
    internal bool TrackInResourceValue { get; private set; } = true;

    public OptionsDropdownBuilder Label(string label)
    {
        _label = label;
        return this;
    }

    public OptionsDropdownBuilder Value(Func<int> getValue, Action<int> setValue)
    {
        _getValue = getValue;
        _setValue = setValue;
        return this;
    }

    public OptionsDropdownBuilder Default(int defaultValue)
    {
        _hasDefault = true;
        _defaultValue = defaultValue;
        return this;
    }

    public OptionsDropdownBuilder Items(params string[] items)
    {
        _items.Clear();

        if (items == null)
            return this;

        _items.AddRange(items);
        return this;
    }

    public OptionsDropdownBuilder WithOrder(int order)
    {
        OrderValue = order;
        return this;
    }

    internal OptionsDropdownBuilder TrackInResource(bool trackInResource)
    {
        TrackInResourceValue = trackInResource;
        return this;
    }

    internal OptionsDropdownRegistration Build()
    {
        if (string.IsNullOrWhiteSpace(_label))
            throw new ArgumentException("Dropdown label cannot be empty.");

        if ((_getValue == null) != (_setValue == null))
            throw new ArgumentException("Dropdown getter and setter must either both be set or both be omitted.");

        if (_items.Count == 0)
            throw new ArgumentException("Dropdown must define at least one item.");

        foreach (string item in _items)
        {
            if (string.IsNullOrWhiteSpace(item))
                throw new ArgumentException("Dropdown items cannot be empty.");
        }

        return new OptionsDropdownRegistration(
            _label,
            _getValue,
            _setValue,
            _hasDefault,
            _defaultValue,
            TrackInResourceValue,
            [.. _items],
            OrderValue);
    }
}

internal readonly record struct OptionsDropdownRegistration(
    string Label,
    Func<int> GetValue,
    Action<int> SetValue,
    bool HasDefault,
    int DefaultValue,
    bool TrackInResource,
    string[] Items,
    int Order);
