using System;

namespace Framework.UI;

internal sealed class OptionsLineEditDefinition
{
    public OptionsLineEditDefinition(
        int id,
        OptionsTab tab,
        string label,
        Func<string> getValue,
        Action<string> setValue,
        string placeholder,
        int order = 0)
    {
        Id = id;
        Tab = tab;
        Label = label;
        GetValue = getValue;
        SetValue = setValue;
        Placeholder = placeholder ?? string.Empty;
        Order = order;
    }

    public int Id { get; }
    public OptionsTab Tab { get; }
    public string Label { get; }
    public Func<string> GetValue { get; }
    public Action<string> SetValue { get; }
    public string Placeholder { get; }
    public int Order { get; }
}
