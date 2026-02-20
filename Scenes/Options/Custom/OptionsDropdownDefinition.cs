using System;
using System.Collections.Generic;

namespace Framework.UI;

internal sealed class OptionsDropdownDefinition
{
    public OptionsDropdownDefinition(
        int id,
        OptionsTab tab,
        string label,
        Func<int> getValue,
        Action<int> setValue,
        IReadOnlyList<string> items,
        int order = 0)
    {
        Id = id;
        Tab = tab;
        Label = label;
        GetValue = getValue;
        SetValue = setValue;
        Items = [.. items];
        Order = order;
    }

    public int Id { get; }
    public OptionsTab Tab { get; }
    public string Label { get; }
    public Func<int> GetValue { get; }
    public Action<int> SetValue { get; }
    public IReadOnlyList<string> Items { get; }
    public int Order { get; }
}
