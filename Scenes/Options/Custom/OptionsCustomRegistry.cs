using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

#nullable enable

namespace Framework.UI;

/// <summary>
/// Handles registration and inline JSON persistence for custom option definitions.
/// </summary>
internal sealed class OptionsCustomRegistry
{
    private readonly ResourceOptions _options;

    private readonly Dictionary<int, RegisteredSliderOption> _customSliderOptions = [];
    private readonly Dictionary<int, RegisteredDropdownOption> _customDropdownOptions = [];
    private readonly Dictionary<int, RegisteredLineEditOption> _customLineEditOptions = [];
    private readonly Dictionary<(OptionsTab Tab, string Label), int> _customOptionIds = [];
    private int _nextCustomOptionId;

    public OptionsCustomRegistry(ResourceOptions options)
    {
        _options = options;
    }

    public IEnumerable<RegisteredSliderOption> GetSliderOptions()
    {
        return _customSliderOptions.Values;
    }

    public IEnumerable<RegisteredDropdownOption> GetDropdownOptions()
    {
        return _customDropdownOptions.Values;
    }

    public IEnumerable<RegisteredLineEditOption> GetLineEditOptions()
    {
        return _customLineEditOptions.Values;
    }

    public RegisteredSliderOption AddSlider(SliderOptionDefinition option)
    {
        ArgumentNullException.ThrowIfNull(option);

        ValidateOptionLabel(option.Label, "Slider");
        ValidateSliderRange(option.MinValue, option.MaxValue, option.Step);

        int id = GetOrCreateOptionId(option.Tab, option.Label);
        string key = GetInlineSaveKey(option.Label);

        float minValue = (float)option.MinValue;
        float maxValue = (float)option.MaxValue;
        float defaultValue = Mathf.Clamp(option.DefaultValue, minValue, maxValue);

        float trackedValue = Mathf.Clamp(GetOrCreateSliderValue(key, defaultValue), minValue, maxValue);
        SetCustomSliderValue(key, trackedValue);
        option.SetValue(trackedValue);

        float getValue()
        {
            float value = Mathf.Clamp(GetOrCreateSliderValue(key, defaultValue), minValue, maxValue);
            SetCustomSliderValue(key, value);
            return value;
        }

        void setValue(float value)
        {
            float clampedValue = Mathf.Clamp(value, minValue, maxValue);
            SetCustomSliderValue(key, clampedValue);
            option.SetValue(clampedValue);
        }

        RegisteredSliderOption slider = new(id, option, getValue, setValue);
        _customDropdownOptions.Remove(id);
        _customLineEditOptions.Remove(id);
        _customSliderOptions[id] = slider;
        return slider;
    }

    public RegisteredDropdownOption AddDropdown(DropdownOptionDefinition option)
    {
        ArgumentNullException.ThrowIfNull(option);

        ValidateOptionLabel(option.Label, "Dropdown");
        ValidateDropdownItems(option.Items);

        int id = GetOrCreateOptionId(option.Tab, option.Label);
        string key = GetInlineSaveKey(option.Label);

        int maxIndex = option.Items.Count - 1;
        int defaultValue = Mathf.Clamp(option.DefaultValue, 0, maxIndex);

        int trackedValue = Mathf.Clamp(GetOrCreateDropdownValue(key, defaultValue), 0, maxIndex);
        SetCustomDropdownValue(key, trackedValue);
        option.SetValue(trackedValue);

        int getValue()
        {
            int value = Mathf.Clamp(GetOrCreateDropdownValue(key, defaultValue), 0, maxIndex);
            SetCustomDropdownValue(key, value);
            return value;
        }

        void setValue(int value)
        {
            int clampedValue = Mathf.Clamp(value, 0, maxIndex);
            SetCustomDropdownValue(key, clampedValue);
            option.SetValue(clampedValue);
        }

        RegisteredDropdownOption dropdown = new(id, option, getValue, setValue);
        _customSliderOptions.Remove(id);
        _customLineEditOptions.Remove(id);
        _customDropdownOptions[id] = dropdown;
        return dropdown;
    }

    public RegisteredLineEditOption AddLineEdit(LineEditOptionDefinition option)
    {
        ArgumentNullException.ThrowIfNull(option);

        ValidateOptionLabel(option.Label, "LineEdit");

        int id = GetOrCreateOptionId(option.Tab, option.Label);
        string key = GetInlineSaveKey(option.Label);
        string defaultValue = option.DefaultValue ?? string.Empty;

        string trackedValue = GetOrCreateLineEditValue(key, defaultValue);
        SetCustomLineEditValue(key, trackedValue);
        option.SetValue(trackedValue);

        string getValue() => GetOrCreateLineEditValue(key, defaultValue);
        void setValue(string value)
        {
            string sanitized = value ?? string.Empty;
            SetCustomLineEditValue(key, sanitized);
            option.SetValue(sanitized);
        }

        RegisteredLineEditOption lineEdit = new(id, option, getValue, setValue);
        _customSliderOptions.Remove(id);
        _customDropdownOptions.Remove(id);
        _customLineEditOptions[id] = lineEdit;
        return lineEdit;
    }

    private int GetOrCreateOptionId(OptionsTab tab, string label)
    {
        (OptionsTab Tab, string Label) key = (tab, label);

        if (_customOptionIds.TryGetValue(key, out int id))
            return id;

        id = ++_nextCustomOptionId;
        _customOptionIds[key] = id;
        return id;
    }

    private static void ValidateOptionLabel(string label, string optionType)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException($"{optionType} label cannot be empty.");
    }

    private static void ValidateDropdownItems(IReadOnlyList<string> items)
    {
        if (items == null || items.Count == 0)
            throw new ArgumentException("Dropdown must define at least one item.");

        foreach (string item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
                throw new ArgumentException("Dropdown items cannot be empty.");
        }
    }

    private static void ValidateSliderRange(double minValue, double maxValue, double step)
    {
        if (maxValue <= minValue)
            throw new ArgumentException("Slider max value must be greater than min value.");

        if (step <= 0)
            throw new ArgumentException("Slider step must be greater than 0.");
    }

    /// <summary>
    /// Convert an option label to a consistent PascalCase key used when
    /// storing custom values in <see cref="ResourceOptions.CustomOptionValues"/>.
    /// </summary>
    /// <remarks>
    /// The algorithm matches the behaviour previously implemented by the
    /// earlier <c>ToPascalCaseKey</c> helper.  It is kept simple so that
    /// different label formats (spaces, punctuation, all‑upper) map to the
    /// same identifier.
    /// </remarks>
    private static string GetInlineSaveKey(string label)
    {
        return ToPascalCaseKey(label);
    }

    /// <summary>
    /// Read the current value for a slider option, falling back to the default
    /// and ensuring an entry exists in the custom dictionary if nothing was
    /// previously stored.
    /// </summary>
    private float GetOrCreateSliderValue(string key, float defaultValue)
    {
        return GetOrCreateCustomValue(key, defaultValue, element =>
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetSingle(out float number))
                    return (true, number);
                if (element.TryGetDouble(out double numberDouble))
                    return (true, (float)numberDouble);
            }
            else if (element.ValueKind == JsonValueKind.String &&
                float.TryParse(element.GetString(), out float parsed))
            {
                return (true, parsed);
            }

            return (false, defaultValue);
        });
    }

    /// <summary>
    /// Read the current value for a dropdown option.  See
    /// <see cref="GetOrCreateSliderValue"/> for behaviour comments.
    /// </summary>
    private int GetOrCreateDropdownValue(string key, int defaultValue)
    {
        return GetOrCreateCustomValue(key, defaultValue, element =>
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetInt32(out int number))
                    return (true, number);
                if (element.TryGetDouble(out double numberDouble))
                    return (true, (int)numberDouble);
            }
            else if (element.ValueKind == JsonValueKind.String &&
                     int.TryParse(element.GetString(), out int parsed))
            {
                return (true, parsed);
            }

            return (false, defaultValue);
        });
    }

    /// <summary>
    /// Read or create a line-edit value (a simple string) from the options
    /// store.  Converts the stored JSON value to text and ensures the
    /// dictionary contains an entry afterwards.
    /// </summary>
    private string GetOrCreateLineEditValue(string key, string defaultValue)
    {
        // defaultValue is guaranteed non-null by callers.
        return GetOrCreateCustomValue<string>(key, defaultValue, element =>
        {
            // GetRawText returns the JSON literal; strip wrapping quotes if present.
            string raw = element.GetRawText();
            if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            {
                raw = raw.Substring(1, raw.Length - 2);
            }
            return (true, raw);
        });
    }

    private void SetCustomSliderValue(string key, float value)
    {
        SetCustomValue(key, value);
    }

    private void SetCustomDropdownValue(string key, int value)
    {
        SetCustomValue(key, value);
    }

    private void SetCustomLineEditValue(string key, string value)
    {
        SetCustomValue(key, value ?? string.Empty);
    }

    /// <summary>
    /// If <see cref="ResourceOptions"/> exposes a public property with the
    /// given name return its current value.  Used to prefer typed properties
    /// over the custom dictionary when both exist.
    /// </summary>
    private bool TryGetOptionsProperty(string key, out object? value)
    {
        PropertyInfo? prop = typeof(ResourceOptions).GetProperty(key);
        if (prop != null)
        {
            value = prop.GetValue(_options);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Attempt to extract a value from a JSON element using the provided
    /// parser.  If the key does not exist in the custom dictionary the
    /// default value is returned and the dictionary is populated with that
    /// default.
    /// </summary>
    private T GetOrCreateCustomValue<T>(string key, T defaultValue, Func<JsonElement, (bool success, T value)> tryParse)
    {
        // prefer typed property if present
        if (TryGetOptionsProperty(key, out object? raw) && raw is T t)
            return t;

        Dictionary<string, JsonElement> values = _options.CustomOptionValues ??= [];

        if (values.TryGetValue(key, out JsonElement element))
        {
            (bool ok, T val) = tryParse(element);
            if (ok)
                return val;
        }

        // missing or invalid; store default and return it
        SetCustomValue(key, defaultValue);
        return defaultValue;
    }

    /// <summary>
    /// Store a value in the custom-options dictionary unless a typed property
    /// already handles the key.
    /// </summary>
    private void SetCustomValue<T>(string key, T value)
    {
        if (TryGetOptionsProperty(key, out _))
            return; // property takes precedence

        Dictionary<string, JsonElement> values = _options.CustomOptionValues ??= [];
        values[key] = JsonSerializer.SerializeToElement(value);
    }


    /// <summary>
    /// Convert a human-readable label into a clean PascalCase identifier suitable
    /// for use as a dictionary key.  Preserves existing behaviour from before
    /// this refactor.
    /// </summary>
    private static string ToPascalCaseKey(string label)
    {
        string source = label ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        bool hasSeparator = false;
        foreach (char c in source)
        {
            if (!char.IsLetterOrDigit(c))
            {
                hasSeparator = true;
                break;
            }
        }

        if (!hasSeparator)
        {
            bool allUpper = true;
            foreach (char c in source)
            {
                if (char.IsLetter(c) && !char.IsUpper(c))
                {
                    allUpper = false;
                    break;
                }
            }

            if (allUpper)
            {
                string lowered = source.ToLowerInvariant();
                return char.ToUpperInvariant(lowered[0]) + lowered[1..];
            }

            return char.ToUpperInvariant(source[0]) + source[1..];
        }

        StringBuilder result = new();
        bool capitalizeNext = true;

        foreach (char c in source)
        {
            if (!char.IsLetterOrDigit(c))
            {
                capitalizeNext = true;
                continue;
            }

            if (capitalizeNext)
            {
                result.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                result.Append(char.ToLowerInvariant(c));
            }
        }

        return result.ToString();
    }
}
