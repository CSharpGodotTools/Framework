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
/// <summary>
/// Central options service used by the framework and game scripts.
/// It loads/saves options, exposes current settings, and handles custom option registration.
/// </summary>
public partial class OptionsManager : IDisposable
{
    // Events
    public event Action<WindowMode> WindowModeChanged;
    internal event Action<RegisteredSliderOption> SliderOptionRegistered;
    internal event Action<RegisteredDropdownOption> DropdownOptionRegistered;
    internal event Action<RegisteredLineEditOption> LineEditOptionRegistered;

    // Constants
    private const string PathOptions = "user://options.json";
    private const string PathHotkeys = "user://hotkeys.tres";

    // Fields
    private Godot.Collections.Dictionary<StringName, Godot.Collections.Array<InputEvent>> _defaultHotkeys;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private ResourceOptions _options;
    private ResourceHotkeys _hotkeys;
    private readonly Dictionary<int, RegisteredSliderOption> _customSliderOptions = [];
    private readonly Dictionary<int, RegisteredDropdownOption> _customDropdownOptions = [];
    private readonly Dictionary<int, RegisteredLineEditOption> _customLineEditOptions = [];
    private readonly Dictionary<(OptionsTab Tab, string Label), int> _customOptionIds = [];
    private int _nextCustomOptionId;
    // True when options.json already existed and was loaded at startup.
    // False on first run (no options.json yet).
    private bool _loadedFromExistingOptionsFile;
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

    internal IEnumerable<RegisteredSliderOption> GetSliderOptions()
    {
        return _customSliderOptions.Values;
    }

    internal IEnumerable<RegisteredDropdownOption> GetDropdownOptions()
    {
        return _customDropdownOptions.Values;
    }

    internal IEnumerable<RegisteredLineEditOption> GetLineEditOptions()
    {
        return _customLineEditOptions.Values;
    }

    /// <summary>
    /// Registers a custom slider option class.
    /// </summary>
    public void AddSlider(SliderOptionDefinition option)
    {
        ArgumentNullException.ThrowIfNull(option);

        ValidateOptionLabel(option.Label, "Slider");
        ValidateSliderRange(option.MinValue, option.MaxValue, option.Step);

        int id = GetOrCreateOptionId(option.Tab, option.Label);
        string key = ResolveSaveKey(option.SaveKey, option.Label);

        Func<float> getValue;
        Action<float> setValue;

        float minValue = (float)option.MinValue;
        float maxValue = (float)option.MaxValue;
        float defaultValue = Mathf.Clamp(option.DefaultValue, minValue, maxValue);

        // Mode A: option manages its own value in inline custom JSON storage.
        if (option.SaveInCustomValues)
        {
            float trackedValue = Mathf.Clamp(GetOrCreateSliderValue(key, defaultValue), minValue, maxValue);
            SetCustomSliderValue(key, trackedValue);
            option.SetValue(trackedValue);

            getValue = () =>
            {
                float value = Mathf.Clamp(GetOrCreateSliderValue(key, defaultValue), minValue, maxValue);
                SetCustomSliderValue(key, value);
                return value;
            };

            setValue = value =>
            {
                float clampedValue = Mathf.Clamp(value, minValue, maxValue);
                SetCustomSliderValue(key, clampedValue);
                option.SetValue(clampedValue);
            };
        }
        else
        {
            // Mode B: option is bound to typed settings (for example ResourceOptions.MouseSensitivity).
            RemoveCustomOptionValue(key);

            // On first run, seed typed settings with default once.
            if (!_loadedFromExistingOptionsFile)
            {
                option.SetValue(defaultValue);
            }

            getValue = () =>
            {
                float value = option.GetValue();
                return Mathf.Clamp(value, minValue, maxValue);
            };

            setValue = value =>
            {
                float clampedValue = Mathf.Clamp(value, minValue, maxValue);
                option.SetValue(clampedValue);
            };
        }

        RegisteredSliderOption slider = new(id, option, getValue, setValue);
        _customDropdownOptions.Remove(id);
        _customLineEditOptions.Remove(id);
        _customSliderOptions[id] = slider;
        SliderOptionRegistered?.Invoke(slider);
    }

    /// <summary>
    /// Registers a custom dropdown option class.
    /// </summary>
    public void AddDropdown(DropdownOptionDefinition option)
    {
        ArgumentNullException.ThrowIfNull(option);

        ValidateOptionLabel(option.Label, "Dropdown");
        ValidateDropdownItems(option.Items);

        int id = GetOrCreateOptionId(option.Tab, option.Label);
        string key = ResolveSaveKey(option.SaveKey, option.Label);

        int maxIndex = option.Items.Count - 1;
        int defaultValue = Mathf.Clamp(option.DefaultValue, 0, maxIndex);

        Func<int> getValue;
        Action<int> setValue;

        // Mode A: option manages its own value in inline custom JSON storage.
        if (option.SaveInCustomValues)
        {
            int trackedValue = Mathf.Clamp(GetOrCreateDropdownValue(key, defaultValue), 0, maxIndex);
            SetCustomDropdownValue(key, trackedValue);
            option.SetValue(trackedValue);

            getValue = () =>
            {
                int value = Mathf.Clamp(GetOrCreateDropdownValue(key, defaultValue), 0, maxIndex);
                SetCustomDropdownValue(key, value);
                return value;
            };

            setValue = value =>
            {
                int clampedValue = Mathf.Clamp(value, 0, maxIndex);
                SetCustomDropdownValue(key, clampedValue);
                option.SetValue(clampedValue);
            };
        }
        else
        {
            // Mode B: option is bound to typed settings (for example ResourceOptions.Difficulty).
            RemoveCustomOptionValue(key);

            // On first run, seed typed settings with default once.
            if (!_loadedFromExistingOptionsFile)
            {
                option.SetValue(defaultValue);
            }

            getValue = () =>
            {
                int value = option.GetValue();
                return Mathf.Clamp(value, 0, maxIndex);
            };

            setValue = value =>
            {
                int clampedValue = Mathf.Clamp(value, 0, maxIndex);
                option.SetValue(clampedValue);
            };
        }

        RegisteredDropdownOption dropdown = new(id, option, getValue, setValue);
        _customSliderOptions.Remove(id);
        _customLineEditOptions.Remove(id);
        _customDropdownOptions[id] = dropdown;
        DropdownOptionRegistered?.Invoke(dropdown);
    }

    /// <summary>
    /// Registers a custom line edit option class.
    /// </summary>
    public void AddLineEdit(LineEditOptionDefinition option)
    {
        ArgumentNullException.ThrowIfNull(option);

        ValidateOptionLabel(option.Label, "LineEdit");

        int id = GetOrCreateOptionId(option.Tab, option.Label);
        string key = ResolveSaveKey(option.SaveKey, option.Label);
        string defaultValue = option.DefaultValue ?? string.Empty;

        Func<string> getValue;
        Action<string> setValue;

        // Mode A: option manages its own value in inline custom JSON storage.
        if (option.SaveInCustomValues)
        {
            string trackedValue = GetOrCreateLineEditValue(key, defaultValue);
            SetCustomLineEditValue(key, trackedValue);
            option.SetValue(trackedValue);

            getValue = () => GetOrCreateLineEditValue(key, defaultValue);
            setValue = value =>
            {
                string sanitized = value ?? string.Empty;
                SetCustomLineEditValue(key, sanitized);
                option.SetValue(sanitized);
            };
        }
        else
        {
            // Mode B: option is bound to typed settings (for example ResourceOptions.PlayerName).
            RemoveCustomOptionValue(key);

            // On first run, seed typed settings with default once.
            if (!_loadedFromExistingOptionsFile)
            {
                option.SetValue(defaultValue);
            }

            getValue = () => option.GetValue() ?? string.Empty;
            setValue = value => option.SetValue(value ?? string.Empty);
        }

        RegisteredLineEditOption lineEdit = new(id, option, getValue, setValue);
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
    /// Chooses which key string should be used to save a custom option.
    /// </summary>
    private static string ResolveSaveKey(string saveKey, string label)
    {
        string keySource = string.IsNullOrWhiteSpace(saveKey)
            ? label
            : saveKey;

        return ToPascalCaseKey(keySource);
    }

    // Read custom slider value from inline JSON storage.
    // If missing/invalid, seed with default and return default.
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

    // Read custom dropdown index from inline JSON storage.
    // If missing/invalid, seed with default and return default.
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

    // Read custom text value from inline JSON storage.
    // If missing/invalid, seed with default and return default.
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

    // Persist custom slider value under a single inline JSON key.
    private void SetCustomSliderValue(string key, float value)
    {
        Dictionary<string, JsonElement> values = _options.CustomOptionValues ??= [];
        values[key] = JsonSerializer.SerializeToElement(value);
    }

    // Persist custom dropdown index under a single inline JSON key.
    private void SetCustomDropdownValue(string key, int value)
    {
        Dictionary<string, JsonElement> values = _options.CustomOptionValues ??= [];
        values[key] = JsonSerializer.SerializeToElement(value);
    }

    // Persist custom text value under a single inline JSON key.
    private void SetCustomLineEditValue(string key, string value)
    {
        Dictionary<string, JsonElement> values = _options.CustomOptionValues ??= [];
        values[key] = JsonSerializer.SerializeToElement(value ?? string.Empty);
    }

    // Removes inline custom value when option is using typed/bound persistence instead.
    private void RemoveCustomOptionValue(string key)
    {
        _options.CustomOptionValues?.Remove(key);
    }

    /// <summary>
    /// Converts a label-like value into a JSON-friendly PascalCase key.
    /// Example: "MOUSE_SENSITIVITY" => "MouseSensitivity".
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
            _loadedFromExistingOptionsFile = true;
        }
        else
        {
            _options = new();
            _loadedFromExistingOptionsFile = false;
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
