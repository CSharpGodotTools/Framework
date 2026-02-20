using System;

namespace Framework.UI;

public enum OptionsTab
{
    General,
    Gameplay,
    Display,
    Graphics,
    Audio,
    Input
}

internal sealed class OptionsSliderDefinition
{
    public OptionsSliderDefinition(
        int id,
        OptionsTab tab,
        string label,
        Func<float> getValue,
        Action<float> setValue,
        double minValue,
        double maxValue,
        double step = 1.0,
        int order = 0)
    {
        Id = id;
        Tab = tab;
        Label = label;
        GetValue = getValue;
        SetValue = setValue;
        MinValue = minValue;
        MaxValue = maxValue;
        Step = step;
        Order = order;
    }

    public int Id { get; }
    public OptionsTab Tab { get; }
    public string Label { get; }
    public Func<float> GetValue { get; }
    public Action<float> SetValue { get; }
    public double MinValue { get; }
    public double MaxValue { get; }
    public double Step { get; }
    public int Order { get; }
}
