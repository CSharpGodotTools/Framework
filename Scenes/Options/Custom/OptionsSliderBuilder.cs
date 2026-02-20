using System;

namespace Framework.UI;

public sealed class OptionsSliderBuilder
{
    private string _label;
    private Func<float> _getValue;
    private Action<float> _setValue;
    private bool _hasRange;
    private double _minValue;
    private double _maxValue;
    private bool _hasDefault;
    private float _defaultValue;

    internal int OrderValue { get; private set; }
    internal double StepValue { get; private set; } = 1.0;
    internal bool TrackInResourceValue { get; private set; } = true;

    public OptionsSliderBuilder Label(string label)
    {
        _label = label;
        return this;
    }

    public OptionsSliderBuilder Value(Func<float> getValue, Action<float> setValue)
    {
        _getValue = getValue;
        _setValue = setValue;
        return this;
    }

    public OptionsSliderBuilder Default(float defaultValue)
    {
        _hasDefault = true;
        _defaultValue = defaultValue;
        return this;
    }

    public OptionsSliderBuilder Range(double minValue, double maxValue)
    {
        _hasRange = true;
        _minValue = minValue;
        _maxValue = maxValue;
        return this;
    }

    public OptionsSliderBuilder Step(double step)
    {
        StepValue = step;
        return this;
    }

    public OptionsSliderBuilder WithOrder(int order)
    {
        OrderValue = order;
        return this;
    }

    internal OptionsSliderBuilder TrackInResource(bool trackInResource)
    {
        TrackInResourceValue = trackInResource;
        return this;
    }

    internal OptionsSliderRegistration Build()
    {
        if (string.IsNullOrWhiteSpace(_label))
            throw new ArgumentException("Slider label cannot be empty.");

        if ((_getValue == null) != (_setValue == null))
            throw new ArgumentException("Slider getter and setter must either both be set or both be omitted.");

        if (!_hasRange)
            throw new ArgumentException("Slider range must be set with Range.");

        if (_maxValue <= _minValue)
            throw new ArgumentException("Slider max value must be greater than min value.");

        if (StepValue <= 0)
            throw new ArgumentException("Slider step must be greater than 0.");

        return new OptionsSliderRegistration(
            _label,
            _getValue,
            _setValue,
            _hasDefault,
            _defaultValue,
            TrackInResourceValue,
            _minValue,
            _maxValue,
            StepValue,
            OrderValue);
    }
}

internal readonly record struct OptionsSliderRegistration(
    string Label,
    Func<float> GetValue,
    Action<float> SetValue,
    bool HasDefault,
    float DefaultValue,
    bool TrackInResource,
    double MinValue,
    double MaxValue,
    double Step,
    int Order);
