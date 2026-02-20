using Godot;
using GodotUtils;
using GodotUtils.RegEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FileAccess = Godot.FileAccess;

namespace Framework.UI;

// Autoload
public partial class OptionsManager : IDisposable
{
    // Events
    public event Action<WindowMode> WindowModeChanged;
    internal event Action<OptionsSliderDefinition> SliderOptionRegistered;
    internal event Action<OptionsDropdownDefinition> DropdownOptionRegistered;
    internal event Action<OptionsLineEditDefinition> LineEditOptionRegistered;

    // Constants
    private const string PathOptions = "user://options.json";
    private const string PathHotkeys = "user://hotkeys.tres";

    // Fields
    private Godot.Collections.Dictionary<StringName, Godot.Collections.Array<InputEvent>> _defaultHotkeys;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private ResourceOptions _options;
    private ResourceHotkeys _hotkeys;
    private readonly Dictionary<int, OptionsSliderDefinition> _customSliderOptions = [];
    private readonly Dictionary<int, OptionsDropdownDefinition> _customDropdownOptions = [];
    private readonly Dictionary<int, OptionsLineEditDefinition> _customLineEditOptions = [];
    private readonly Dictionary<(OptionsTab Tab, string Label), int> _customOptionIds = [];
    private int _nextCustomOptionId;
    private bool _optionsLoadedFromDisk;
    private string _currentOptionsTab = "General";
    private AutoloadsFramework _autoloads;

    public OptionsManager(AutoloadsFramework autoloads)
    {
        SetupAutoloads(autoloads);

        LoadOptions();

        GetDefaultHotkeys();
        LoadHotkeys();

        SetWindowMode();
        SetVSyncMode();
        SetWinSize();
        SetMaxFPS();
        SetLanguage();
        SetAntialiasing();
    }

    private void SetupAutoloads(AutoloadsFramework autoloads)
    {
        _autoloads = autoloads;
        _autoloads.PreQuit += SaveSettingsOnQuit;
    }

    public void Update()
    {
        if (Input.IsActionJustPressed(InputActions.Fullscreen))
        {
            ToggleFullscreen();
        }
    }

    public void Dispose()
    {
        _autoloads.PreQuit -= SaveSettingsOnQuit;
        GC.SuppressFinalize(this);
    }

    public string GetCurrentTab()
    {
        return _currentOptionsTab;
    }

    public void SetCurrentTab(string tab)
    {
        _currentOptionsTab = tab;
    }

    public ResourceOptions GetOptions()
    {
        return _options;
    }

    public ResourceOptions Settings => _options;

    public ResourceHotkeys GetHotkeys()
    {
        return _hotkeys;
    }

    internal IEnumerable<OptionsSliderDefinition> GetSliderOptions()
    {
        return _customSliderOptions.Values;
    }

    internal IEnumerable<OptionsDropdownDefinition> GetDropdownOptions()
    {
        return _customDropdownOptions.Values;
    }

    internal IEnumerable<OptionsLineEditDefinition> GetLineEditOptions()
    {
        return _customLineEditOptions.Values;
    }

    public float GetSliderValue(OptionsTab tab, string label, float defaultValue = 0.0f)
    {
        string key = GetCustomOptionKey(tab, label);
        return GetOrCreateSliderValue(key, defaultValue);
    }

    public int GetDropdownValue(OptionsTab tab, string label, int defaultValue = 0)
    {
        string key = GetCustomOptionKey(tab, label);
        return GetOrCreateDropdownValue(key, defaultValue);
    }

    public string GetLineEditValue(OptionsTab tab, string label, string defaultValue = "")
    {
        string key = GetCustomOptionKey(tab, label);
        return GetOrCreateLineEditValue(key, defaultValue ?? string.Empty);
    }

    public void SetSliderValue(OptionsTab tab, string label, float value)
    {
        string key = GetCustomOptionKey(tab, label);
        SetCustomSliderValue(key, value);
    }

    public void SetDropdownValue(OptionsTab tab, string label, int value)
    {
        string key = GetCustomOptionKey(tab, label);
        SetCustomDropdownValue(key, value);
    }

    public void SetLineEditValue(OptionsTab tab, string label, string value)
    {
        string key = GetCustomOptionKey(tab, label);
        SetCustomLineEditValue(key, value ?? string.Empty);
    }

    public void AddSlider(
        OptionsTab tab,
        string label,
        double minValue,
        double maxValue,
        double step = 1.0,
        float defaultValue = 0.0f,
        int order = 0,
        Action<float> onValueChanged = null)
    {
        AddSlider(tab, slider =>
        {
            slider
                .Label(label)
                .Range(minValue, maxValue)
                .Step(step)
                .Default(defaultValue)
                .WithOrder(order);

            if (onValueChanged != null)
            {
                slider.Value(
                    () => GetSliderValue(tab, label, defaultValue),
                    value => onValueChanged(value));
            }
        });
    }

    public void AddDropdown(
        OptionsTab tab,
        string label,
        IEnumerable<string> items,
        int defaultValue = 0,
        int order = 0,
        Action<int> onValueChanged = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        string[] itemArray = [.. items];

        AddDropdown(tab, dropdown =>
        {
            dropdown
                .Label(label)
                .Items(itemArray)
                .Default(defaultValue)
                .WithOrder(order);

            if (onValueChanged != null)
            {
                dropdown.Value(
                    () => GetDropdownValue(tab, label, defaultValue),
                    value => onValueChanged(value));
            }
        });
    }

    public void AddLineEdit(
        OptionsTab tab,
        string label,
        string defaultValue = "",
        string placeholder = "",
        int order = 0,
        Action<string> onValueChanged = null)
    {
        AddLineEdit(tab, lineEdit =>
        {
            lineEdit
                .Label(label)
                .Default(defaultValue ?? string.Empty)
                .Placeholder(placeholder ?? string.Empty)
                .WithOrder(order);

            if (onValueChanged != null)
            {
                lineEdit.Value(
                    () => GetLineEditValue(tab, label, defaultValue),
                    value => onValueChanged(value ?? string.Empty));
            }
        });
    }

    public void AddSlider(
        OptionsTab tab,
        string label,
        Func<float> getValue,
        Action<float> setValue,
        double minValue,
        double maxValue,
        double step = 1.0,
        float defaultValue = 0.0f,
        int order = 0)
    {
        ArgumentNullException.ThrowIfNull(getValue);
        ArgumentNullException.ThrowIfNull(setValue);

        AddSlider(tab, slider => slider
            .Label(label)
            .Value(getValue, setValue)
            .Range(minValue, maxValue)
            .Step(step)
            .Default(defaultValue)
            .WithOrder(order)
            .TrackInResource(false));
    }

    public void AddDropdown(
        OptionsTab tab,
        string label,
        IEnumerable<string> items,
        Func<int> getValue,
        Action<int> setValue,
        int defaultValue = 0,
        int order = 0)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(getValue);
        ArgumentNullException.ThrowIfNull(setValue);

        string[] itemArray = [.. items];

        AddDropdown(tab, dropdown => dropdown
            .Label(label)
            .Items(itemArray)
            .Value(getValue, setValue)
            .Default(defaultValue)
            .WithOrder(order)
            .TrackInResource(false));
    }

    public void AddLineEdit(
        OptionsTab tab,
        string label,
        Func<string> getValue,
        Action<string> setValue,
        string defaultValue = "",
        string placeholder = "",
        int order = 0)
    {
        ArgumentNullException.ThrowIfNull(getValue);
        ArgumentNullException.ThrowIfNull(setValue);

        AddLineEdit(tab, lineEdit => lineEdit
            .Label(label)
            .Value(getValue, setValue)
            .Default(defaultValue ?? string.Empty)
            .Placeholder(placeholder ?? string.Empty)
            .WithOrder(order)
            .TrackInResource(false));
    }

    public void AddSlider(OptionsTab tab, Action<OptionsSliderBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        OptionsSliderBuilder builder = new();
        configure(builder);

        OptionsSliderRegistration registration = builder.Build();
        int id = GetOrCreateOptionId(tab, registration.Label);
        string key = GetCustomOptionKey(tab, registration.Label);

        Func<float> getValue;
        Action<float> setValue;

        if (registration.TrackInResource)
        {
            float defaultValue = registration.HasDefault
                ? registration.DefaultValue
                : registration.GetValue != null
                    ? registration.GetValue()
                    : 0.0f;

            float trackedValue = GetOrCreateSliderValue(key, defaultValue);
            trackedValue = Mathf.Clamp(trackedValue, (float)registration.MinValue, (float)registration.MaxValue);
            SetCustomSliderValue(key, trackedValue);
            registration.SetValue?.Invoke(trackedValue);

            getValue = () =>
            {
                float value = GetOrCreateSliderValue(key, defaultValue);
                value = Mathf.Clamp(value, (float)registration.MinValue, (float)registration.MaxValue);
                SetCustomSliderValue(key, value);
                return value;
            };

            setValue = value =>
            {
                float clamped = Mathf.Clamp(value, (float)registration.MinValue, (float)registration.MaxValue);
                SetCustomSliderValue(key, clamped);
                registration.SetValue?.Invoke(clamped);
            };
        }
        else
        {
            RemoveCustomOptionValue(key);

            if (registration.GetValue == null || registration.SetValue == null)
                throw new InvalidOperationException("Slider with TrackInResource(false) requires getter and setter.");

            if (!_optionsLoadedFromDisk && registration.HasDefault)
            {
                float defaultClamped = Mathf.Clamp(
                    registration.DefaultValue,
                    (float)registration.MinValue,
                    (float)registration.MaxValue);
                registration.SetValue(defaultClamped);
            }

            getValue = () =>
            {
                float value = registration.GetValue();
                return Mathf.Clamp(value, (float)registration.MinValue, (float)registration.MaxValue);
            };

            setValue = value =>
            {
                float clamped = Mathf.Clamp(value, (float)registration.MinValue, (float)registration.MaxValue);
                registration.SetValue(clamped);
            };
        }

        OptionsSliderDefinition slider = new(
            id,
            tab,
            registration.Label,
            getValue,
            setValue,
            registration.MinValue,
            registration.MaxValue,
            registration.Step,
            registration.Order);

        _customDropdownOptions.Remove(id);
        _customLineEditOptions.Remove(id);
        _customSliderOptions[id] = slider;
        SliderOptionRegistered?.Invoke(slider);
    }

    public void AddDropdown(OptionsTab tab, Action<OptionsDropdownBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        OptionsDropdownBuilder builder = new();
        configure(builder);

        OptionsDropdownRegistration registration = builder.Build();
        int id = GetOrCreateOptionId(tab, registration.Label);
        string key = GetCustomOptionKey(tab, registration.Label);

        int maxIndex = registration.Items.Length - 1;
        Func<int> getValue;
        Action<int> setValue;

        if (registration.TrackInResource)
        {
            int defaultValue = registration.HasDefault
                ? registration.DefaultValue
                : registration.GetValue != null
                    ? registration.GetValue()
                    : 0;

            int trackedValue = Mathf.Clamp(GetOrCreateDropdownValue(key, defaultValue), 0, maxIndex);
            SetCustomDropdownValue(key, trackedValue);
            registration.SetValue?.Invoke(trackedValue);

            getValue = () =>
            {
                int value = Mathf.Clamp(GetOrCreateDropdownValue(key, defaultValue), 0, maxIndex);
                SetCustomDropdownValue(key, value);
                return value;
            };

            setValue = value =>
            {
                int clamped = Mathf.Clamp(value, 0, maxIndex);
                SetCustomDropdownValue(key, clamped);
                registration.SetValue?.Invoke(clamped);
            };
        }
        else
        {
            RemoveCustomOptionValue(key);

            if (registration.GetValue == null || registration.SetValue == null)
                throw new InvalidOperationException("Dropdown with TrackInResource(false) requires getter and setter.");

            if (!_optionsLoadedFromDisk && registration.HasDefault)
            {
                int defaultClamped = Mathf.Clamp(registration.DefaultValue, 0, maxIndex);
                registration.SetValue(defaultClamped);
            }

            getValue = () =>
            {
                int value = registration.GetValue();
                return Mathf.Clamp(value, 0, maxIndex);
            };

            setValue = value =>
            {
                int clamped = Mathf.Clamp(value, 0, maxIndex);
                registration.SetValue(clamped);
            };
        }

        OptionsDropdownDefinition dropdown = new(
            id,
            tab,
            registration.Label,
            getValue,
            setValue,
            registration.Items,
            registration.Order);

        _customSliderOptions.Remove(id);
        _customLineEditOptions.Remove(id);
        _customDropdownOptions[id] = dropdown;
        DropdownOptionRegistered?.Invoke(dropdown);
    }

    public void AddLineEdit(OptionsTab tab, Action<OptionsLineEditBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        OptionsLineEditBuilder builder = new();
        configure(builder);

        OptionsLineEditRegistration registration = builder.Build();
        int id = GetOrCreateOptionId(tab, registration.Label);
        string key = GetCustomOptionKey(tab, registration.Label);

        Func<string> getValue;
        Action<string> setValue;

        if (registration.TrackInResource)
        {
            string defaultValue = registration.HasDefault
                ? registration.DefaultValue
                : registration.GetValue != null
                    ? registration.GetValue() ?? string.Empty
                    : string.Empty;

            string trackedValue = GetOrCreateLineEditValue(key, defaultValue);
            SetCustomLineEditValue(key, trackedValue);
            registration.SetValue?.Invoke(trackedValue);

            getValue = () => GetOrCreateLineEditValue(key, defaultValue);
            setValue = value =>
            {
                string sanitized = value ?? string.Empty;
                SetCustomLineEditValue(key, sanitized);
                registration.SetValue?.Invoke(sanitized);
            };
        }
        else
        {
            RemoveCustomOptionValue(key);

            if (registration.GetValue == null || registration.SetValue == null)
                throw new InvalidOperationException("LineEdit with TrackInResource(false) requires getter and setter.");

            if (!_optionsLoadedFromDisk && registration.HasDefault)
            {
                registration.SetValue(registration.DefaultValue ?? string.Empty);
            }

            getValue = () => registration.GetValue() ?? string.Empty;
            setValue = value => registration.SetValue(value ?? string.Empty);
        }

        OptionsLineEditDefinition lineEdit = new(
            id,
            tab,
            registration.Label,
            getValue,
            setValue,
            registration.Placeholder,
            registration.Order);

        _customSliderOptions.Remove(id);
        _customDropdownOptions.Remove(id);
        _customLineEditOptions[id] = lineEdit;
        LineEditOptionRegistered?.Invoke(lineEdit);
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

    private static string GetCustomOptionKey(OptionsTab tab, string label)
    {
        return NormalizeCustomOptionName(label);
    }

    private float GetOrCreateSliderValue(string key, float defaultValue)
    {
        Dictionary<string, JsonElement> values = _options.CustomOptionValues ??= [];

        if (values.TryGetValue(key, out JsonElement element))
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetSingle(out float number))
                    return number;

                if (element.TryGetDouble(out double numberDouble))
                    return (float)numberDouble;
            }
            else if (element.ValueKind == JsonValueKind.String
                && float.TryParse(element.GetString(), out float parsed))
            {
                return parsed;
            }
        }

        SetCustomSliderValue(key, defaultValue);
        return defaultValue;
    }

    private int GetOrCreateDropdownValue(string key, int defaultValue)
    {
        Dictionary<string, JsonElement> values = _options.CustomOptionValues ??= [];

        if (values.TryGetValue(key, out JsonElement element))
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetInt32(out int number))
                    return number;

                if (element.TryGetDouble(out double numberDouble))
                    return (int)numberDouble;
            }
            else if (element.ValueKind == JsonValueKind.String
                && int.TryParse(element.GetString(), out int parsed))
            {
                return parsed;
            }
        }

        SetCustomDropdownValue(key, defaultValue);
        return defaultValue;
    }

    private string GetOrCreateLineEditValue(string key, string defaultValue)
    {
        Dictionary<string, JsonElement> values = _options.CustomOptionValues ??= [];

        if (values.TryGetValue(key, out JsonElement element))
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => defaultValue ?? string.Empty
            };
        }

        string sanitized = defaultValue ?? string.Empty;
        SetCustomLineEditValue(key, sanitized);
        return sanitized;
    }

    private void SetCustomSliderValue(string key, float value)
    {
        Dictionary<string, JsonElement> values = _options.CustomOptionValues ??= [];
        values[key] = JsonSerializer.SerializeToElement(value);
    }

    private void SetCustomDropdownValue(string key, int value)
    {
        Dictionary<string, JsonElement> values = _options.CustomOptionValues ??= [];
        values[key] = JsonSerializer.SerializeToElement(value);
    }

    private void SetCustomLineEditValue(string key, string value)
    {
        Dictionary<string, JsonElement> values = _options.CustomOptionValues ??= [];
        values[key] = JsonSerializer.SerializeToElement(value ?? string.Empty);
    }

    private void RemoveCustomOptionValue(string key)
    {
        _options.CustomOptionValues?.Remove(key);
    }

    private static string NormalizeCustomOptionName(string label)
    {
        string source = label ?? string.Empty;
        int legacyPrefixIndex = source.IndexOf(':');
        if (legacyPrefixIndex >= 0 && legacyPrefixIndex < source.Length - 1)
        {
            source = source[(legacyPrefixIndex + 1)..];
        }

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
            if (string.IsNullOrWhiteSpace(source))
                return string.Empty;

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

    private void ToggleFullscreen()
    {
        if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed)
        {
            SwitchToFullscreen();
        }
        else
        {
            SwitchToWindow();
        }
    }

    private void SaveOptions()
    {
        string json = JsonSerializer.Serialize(_options, _jsonOptions);
        using FileAccess file = FileAccess.Open(PathOptions, FileAccess.ModeFlags.Write);
        file.StoreString(json);
    }

    private void SaveHotkeys()
    {
        Error error = ResourceSaver.Save(_hotkeys, PathHotkeys);

        if (error != Error.Ok)
        {
            GD.Print($"Failed to save hotkeys: {error}");
        }
    }

    public void ResetHotkeys()
    {
        // Deep clone default hotkeys over
        _hotkeys.Actions = [];

        foreach (KeyValuePair<StringName, Godot.Collections.Array<InputEvent>> element in _defaultHotkeys)
        {
            Godot.Collections.Array<InputEvent> arr = [];

            foreach (InputEvent item in _defaultHotkeys[element.Key])
            {
                arr.Add((InputEvent)item.Duplicate());
            }

            _hotkeys.Actions.Add(element.Key, arr);
        }

        // Set input map
        LoadInputMap(_defaultHotkeys);
    }

    private void LoadOptions()
    {
        if (FileAccess.FileExists(PathOptions))
        {
            using FileAccess file = FileAccess.Open(PathOptions, FileAccess.ModeFlags.Read);
            _options = JsonSerializer.Deserialize<ResourceOptions>(file.GetAsText()) ?? new();
            _optionsLoadedFromDisk = true;
        }
        else
        {
            _options = new();
            _optionsLoadedFromDisk = false;
        }
    }

    private static void LoadInputMap(Godot.Collections.Dictionary<StringName, Godot.Collections.Array<InputEvent>> hotkeys)
    {
        Godot.Collections.Array<StringName> actions = InputMap.GetActions();

        foreach (StringName action in actions)
        {
            InputMap.EraseAction(action);
        }

        foreach (StringName action in hotkeys.Keys)
        {
            InputMap.AddAction(action);

            foreach (InputEvent @event in hotkeys[action])
            {
                InputMap.ActionAddEvent(action, @event);
            }
        }
    }

    private void GetDefaultHotkeys()
    {
        // Get all the default actions defined in the input map
        Godot.Collections.Dictionary<StringName, Godot.Collections.Array<InputEvent>> actions = [];

        foreach (StringName action in InputMap.GetActions())
        {
            actions.Add(action, []);

            foreach (InputEvent actionEvent in InputMap.ActionGetEvents(action))
            {
                actions[action].Add(actionEvent);
            }
        }

        _defaultHotkeys = actions;
    }

    private void LoadHotkeys()
    {
        if (FileAccess.FileExists(PathHotkeys))
        {
            string localResPath = ProjectSettings.LocalizePath(DirectoryUtils.FindFile("res://", "ResourceHotkeys.cs"));
            ValidateResourceFile(PathHotkeys, localResPath);
            _hotkeys = GD.Load<ResourceHotkeys>(PathHotkeys);

            // InputMap in project settings has changed so reset all saved hotkeys
            if (!ActionsAreEqual(_defaultHotkeys, _hotkeys.Actions))
            {
                _hotkeys = new();
                ResetHotkeys();
            }

            LoadInputMap(_hotkeys.Actions);
        }
        else
        {
            _hotkeys = new();
            ResetHotkeys();
        }
    }

    // *.tres files store the path to their script in res:// and as a result if that script is moved then the
    // path in *.tres will point to an invalid path and so this function corrects the path again.
    private static void ValidateResourceFile(string localUserPath, string localResPath)
    {
        string userGlobalPath = ProjectSettings.GlobalizePath(localUserPath);
        string content = File.ReadAllText(userGlobalPath);

        // Find current path in the resource file
        Match match = RegexUtils.ScriptPath().Match(content);

        if (!match.Success)
        {
            GD.PrintErr($"Script path not found in {localUserPath}");
            return;
        }

        string currentPath = match.Value;

        if (currentPath == localResPath)
            return; // Resource path is correct. No update needed.

        // Path is incorrect, proceed to rewrite.
        string updatedContent = RegexUtils.ScriptPath().Replace(content, localResPath);

        File.WriteAllText(userGlobalPath, updatedContent);

        GD.Print($"Script path in {Path.GetFileName(userGlobalPath)} was invalid and has been readjusted to the proper path: {localResPath}");
    }

    private static bool ActionsAreEqual(
        Godot.Collections.Dictionary<StringName, Godot.Collections.Array<InputEvent>> dict1,
        Godot.Collections.Dictionary<StringName, Godot.Collections.Array<InputEvent>> dict2)
    {
        if (dict1.Count != dict2.Count)
        {
            return false;
        }

        foreach (KeyValuePair<StringName, Godot.Collections.Array<InputEvent>> pair in dict1)
        {
            if (!dict2.TryGetValue(pair.Key, out Godot.Collections.Array<InputEvent> dict2Events))
            {
                return false;
            }

            if (!InputEventsAreEqual(pair.Value, dict2Events))
            {
                return false;
            }
        }

        return true;
    }

    private static bool InputEventsAreEqual(Godot.Collections.Array<InputEvent> events1, Godot.Collections.Array<InputEvent> events2)
    {
        if (events1.Count != events2.Count)
        {
            return false;
        }

        for (int index = 0; index < events1.Count; index++)
        {
            string event1 = events1[index].AsText();
            string event2 = events2[index].AsText();
            if (!event1.Equals(event2, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void SetWindowMode()
    {
        switch (_options.WindowMode)
        {
            case WindowMode.Windowed:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                break;
            case WindowMode.Borderless:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                break;
            case WindowMode.Fullscreen:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;
        }
    }

    private void SwitchToFullscreen()
    {
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
        _options.WindowMode = WindowMode.Fullscreen;
        WindowModeChanged?.Invoke(WindowMode.Fullscreen);
    }

    private void SwitchToWindow()
    {
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        _options.WindowMode = WindowMode.Windowed;
        WindowModeChanged?.Invoke(WindowMode.Windowed);
    }

    private void SetVSyncMode()
    {
        DisplayServer.WindowSetVsyncMode(_options.VSyncMode);
    }

    private void SetWinSize()
    {
        Vector2I windowSize = new(_options.WindowWidth, _options.WindowHeight);

        if (windowSize != Vector2I.Zero)
        {
            DisplayServer.WindowSetSize(windowSize);

            // center window
            Vector2I screenSize = DisplayServer.ScreenGetSize();
            Vector2I winSize = DisplayServer.WindowGetSize();
            DisplayServer.WindowSetPosition(screenSize / 2 - winSize / 2);
        }
    }

    private void SetMaxFPS()
    {
        if (DisplayServer.WindowGetVsyncMode() == DisplayServer.VSyncMode.Disabled)
        {
            Engine.MaxFps = _options.MaxFPS;
        }
    }

    private void SetLanguage()
    {
        TranslationServer.SetLocale(
        _options.Language.ToString()[..2].ToLower());
    }

    private void SetAntialiasing()
    {
        // Set both 2D and 3D settings to the same value
        ProjectSettings.SetSetting("rendering/anti_aliasing/quality/msaa_2d", _options.Antialiasing);
        ProjectSettings.SetSetting("rendering/anti_aliasing/quality/msaa_3d", _options.Antialiasing);
    }

    private Task SaveSettingsOnQuit()
    {
        SaveOptions();
        SaveHotkeys();

        return Task.CompletedTask;
    }
}


