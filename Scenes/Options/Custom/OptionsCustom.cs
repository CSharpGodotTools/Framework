using Godot;
using System;
using System.Collections.Generic;

namespace Framework.UI;

public sealed class OptionsCustom : IDisposable
{
    private readonly Dictionary<int, IDisposable> _bindings = [];
    private readonly OptionsManager _optionsManager;
    private readonly OptionsNav _nav;

    public OptionsCustom(OptionsNav nav)
    {
        _nav = nav;
        _optionsManager = GameFramework.Options;

        List<CustomOptionDescriptor> options = [];

        foreach (OptionsSliderDefinition slider in _optionsManager.GetSliderOptions())
        {
            options.Add(new CustomOptionDescriptor(slider));
        }

        foreach (OptionsDropdownDefinition dropdown in _optionsManager.GetDropdownOptions())
        {
            options.Add(new CustomOptionDescriptor(dropdown));
        }

        foreach (OptionsLineEditDefinition lineEdit in _optionsManager.GetLineEditOptions())
        {
            options.Add(new CustomOptionDescriptor(lineEdit));
        }

        options.Sort(SortDefinitions);

        foreach (CustomOptionDescriptor option in options)
        {
            AddOrReplaceOption(option);
        }

        _optionsManager.SliderOptionRegistered += OnSliderOptionRegistered;
        _optionsManager.DropdownOptionRegistered += OnDropdownOptionRegistered;
        _optionsManager.LineEditOptionRegistered += OnLineEditOptionRegistered;
    }

    public void Dispose()
    {
        _optionsManager.SliderOptionRegistered -= OnSliderOptionRegistered;
        _optionsManager.DropdownOptionRegistered -= OnDropdownOptionRegistered;
        _optionsManager.LineEditOptionRegistered -= OnLineEditOptionRegistered;

        foreach (IDisposable binding in _bindings.Values)
        {
            binding.Dispose();
        }

        _bindings.Clear();
        GC.SuppressFinalize(this);
    }

    private void OnSliderOptionRegistered(OptionsSliderDefinition slider)
    {
        AddOrReplaceOption(new CustomOptionDescriptor(slider));
    }

    private void OnDropdownOptionRegistered(OptionsDropdownDefinition dropdown)
    {
        AddOrReplaceOption(new CustomOptionDescriptor(dropdown));
    }

    private void OnLineEditOptionRegistered(OptionsLineEditDefinition lineEdit)
    {
        AddOrReplaceOption(new CustomOptionDescriptor(lineEdit));
    }

    private void AddOrReplaceOption(CustomOptionDescriptor option)
    {
        if (!_nav.TryGetTabContainer(option.Tab, out VBoxContainer tabContainer))
            return;

        Button navButton = GetNavButton(option.Tab);
        if (navButton == null)
            return;

        if (_bindings.TryGetValue(option.Id, out IDisposable existing))
        {
            existing.Dispose();
            _bindings.Remove(option.Id);
        }

        IDisposable binding = option.Type switch
        {
            CustomOptionType.Slider => CreateSliderBinding(tabContainer, navButton, option.Slider),
            CustomOptionType.Dropdown => CreateDropdownBinding(tabContainer, navButton, option.Dropdown),
            CustomOptionType.LineEdit => CreateLineEditBinding(tabContainer, navButton, option.LineEdit),
            _ => null
        };

        if (binding == null)
            return;

        _bindings.Add(option.Id, binding);
    }

    private static int SortDefinitions(CustomOptionDescriptor left, CustomOptionDescriptor right)
    {
        int tabComparison = left.Tab.CompareTo(right.Tab);
        if (tabComparison != 0)
            return tabComparison;

        int orderComparison = left.Order.CompareTo(right.Order);
        if (orderComparison != 0)
            return orderComparison;

        return left.Id.CompareTo(right.Id);
    }

    private static SliderBinding CreateSliderBinding(
        VBoxContainer tabContainer,
        Button navButton,
        OptionsSliderDefinition sliderDef)
    {
        HBoxContainer row = new()
        {
            Name = $"CustomSlider_{sliderDef.Id}"
        };

        Label label = new()
        {
            Text = string.IsNullOrWhiteSpace(sliderDef.Label) ? $"SLIDER_{sliderDef.Id}" : sliderDef.Label,
            CustomMinimumSize = new Vector2(200, 0)
        };
        row.AddChild(label);

        HSlider slider = new()
        {
            CustomMinimumSize = new Vector2(250, 0),
            MinValue = sliderDef.MinValue,
            MaxValue = sliderDef.MaxValue,
            Step = sliderDef.Step
        };
        slider.FocusNeighborLeft = navButton.GetPath();
        row.AddChild(slider);

        tabContainer.AddChild(row);

        if (tabContainer.GetChildCount() == 1)
        {
            navButton.FocusNeighborRight = slider.GetPath();
        }

        float clampedValue = Mathf.Clamp(
            sliderDef.GetValue(),
            (float)sliderDef.MinValue,
            (float)sliderDef.MaxValue);
        sliderDef.SetValue(clampedValue);
        slider.Value = clampedValue;

        Godot.Range.ValueChangedEventHandler onValueChanged = v => sliderDef.SetValue((float)v);
        slider.ValueChanged += onValueChanged;

        return new SliderBinding(row, slider, onValueChanged);
    }

    private static DropdownBinding CreateDropdownBinding(
        VBoxContainer tabContainer,
        Button navButton,
        OptionsDropdownDefinition dropdownDef)
    {
        HBoxContainer row = new()
        {
            Name = $"CustomDropdown_{dropdownDef.Id}"
        };

        Label label = new()
        {
            Text = string.IsNullOrWhiteSpace(dropdownDef.Label) ? $"DROPDOWN_{dropdownDef.Id}" : dropdownDef.Label,
            CustomMinimumSize = new Vector2(200, 0)
        };
        row.AddChild(label);

        OptionButton dropdown = new()
        {
            CustomMinimumSize = new Vector2(250, 0)
        };

        for (int index = 0; index < dropdownDef.Items.Count; index++)
        {
            dropdown.AddItem(dropdownDef.Items[index], index);
        }

        dropdown.FocusNeighborLeft = navButton.GetPath();
        row.AddChild(dropdown);
        tabContainer.AddChild(row);

        if (tabContainer.GetChildCount() == 1)
        {
            navButton.FocusNeighborRight = dropdown.GetPath();
        }

        int maxIndex = dropdownDef.Items.Count - 1;
        int clampedValue = Mathf.Clamp(dropdownDef.GetValue(), 0, maxIndex);
        dropdownDef.SetValue(clampedValue);
        dropdown.Select(clampedValue);

        OptionButton.ItemSelectedEventHandler onItemSelected = index => dropdownDef.SetValue((int)index);
        dropdown.ItemSelected += onItemSelected;

        return new DropdownBinding(row, dropdown, onItemSelected);
    }

    private static LineEditBinding CreateLineEditBinding(
        VBoxContainer tabContainer,
        Button navButton,
        OptionsLineEditDefinition lineEditDef)
    {
        HBoxContainer row = new()
        {
            Name = $"CustomLineEdit_{lineEditDef.Id}"
        };

        Label label = new()
        {
            Text = string.IsNullOrWhiteSpace(lineEditDef.Label) ? $"LINE_EDIT_{lineEditDef.Id}" : lineEditDef.Label,
            CustomMinimumSize = new Vector2(200, 0)
        };
        row.AddChild(label);

        LineEdit lineEdit = new()
        {
            CustomMinimumSize = new Vector2(250, 0),
            PlaceholderText = lineEditDef.Placeholder
        };

        lineEdit.FocusNeighborLeft = navButton.GetPath();
        row.AddChild(lineEdit);
        tabContainer.AddChild(row);

        if (tabContainer.GetChildCount() == 1)
        {
            navButton.FocusNeighborRight = lineEdit.GetPath();
        }

        string value = lineEditDef.GetValue() ?? string.Empty;
        lineEditDef.SetValue(value);
        lineEdit.Text = value;

        LineEdit.TextChangedEventHandler onTextChanged = text => lineEditDef.SetValue(text ?? string.Empty);
        lineEdit.TextChanged += onTextChanged;

        return new LineEditBinding(row, lineEdit, onTextChanged);
    }

    private Button GetNavButton(OptionsTab tab)
    {
        return tab switch
        {
            OptionsTab.General => _nav.GeneralButton,
            OptionsTab.Gameplay => _nav.GameplayButton,
            OptionsTab.Display => _nav.DisplayButton,
            OptionsTab.Graphics => _nav.GraphicsButton,
            OptionsTab.Audio => _nav.AudioButton,
            OptionsTab.Input => _nav.InputButton,
            _ => null
        };
    }

    private sealed class SliderBinding : IDisposable
    {
        private readonly HBoxContainer _row;
        private readonly HSlider _slider;
        private readonly Godot.Range.ValueChangedEventHandler _onValueChanged;

        public SliderBinding(HBoxContainer row, HSlider slider, Godot.Range.ValueChangedEventHandler onValueChanged)
        {
            _row = row;
            _slider = slider;
            _onValueChanged = onValueChanged;
        }

        public void Dispose()
        {
            if (GodotObject.IsInstanceValid(_slider))
            {
                _slider.ValueChanged -= _onValueChanged;
            }

            if (GodotObject.IsInstanceValid(_row))
            {
                _row.QueueFree();
            }

            GC.SuppressFinalize(this);
        }
    }

    private sealed class DropdownBinding : IDisposable
    {
        private readonly HBoxContainer _row;
        private readonly OptionButton _dropdown;
        private readonly OptionButton.ItemSelectedEventHandler _onItemSelected;

        public DropdownBinding(
            HBoxContainer row,
            OptionButton dropdown,
            OptionButton.ItemSelectedEventHandler onItemSelected)
        {
            _row = row;
            _dropdown = dropdown;
            _onItemSelected = onItemSelected;
        }

        public void Dispose()
        {
            if (GodotObject.IsInstanceValid(_dropdown))
            {
                _dropdown.ItemSelected -= _onItemSelected;
            }

            if (GodotObject.IsInstanceValid(_row))
            {
                _row.QueueFree();
            }

            GC.SuppressFinalize(this);
        }
    }

    private sealed class LineEditBinding : IDisposable
    {
        private readonly HBoxContainer _row;
        private readonly LineEdit _lineEdit;
        private readonly LineEdit.TextChangedEventHandler _onTextChanged;

        public LineEditBinding(
            HBoxContainer row,
            LineEdit lineEdit,
            LineEdit.TextChangedEventHandler onTextChanged)
        {
            _row = row;
            _lineEdit = lineEdit;
            _onTextChanged = onTextChanged;
        }

        public void Dispose()
        {
            if (GodotObject.IsInstanceValid(_lineEdit))
            {
                _lineEdit.TextChanged -= _onTextChanged;
            }

            if (GodotObject.IsInstanceValid(_row))
            {
                _row.QueueFree();
            }

            GC.SuppressFinalize(this);
        }
    }

    private enum CustomOptionType
    {
        Slider,
        Dropdown,
        LineEdit
    }

    private readonly struct CustomOptionDescriptor
    {
        public CustomOptionDescriptor(OptionsSliderDefinition slider)
        {
            Id = slider.Id;
            Tab = slider.Tab;
            Order = slider.Order;
            Slider = slider;
            Dropdown = null;
            LineEdit = null;
            Type = CustomOptionType.Slider;
        }

        public CustomOptionDescriptor(OptionsDropdownDefinition dropdown)
        {
            Id = dropdown.Id;
            Tab = dropdown.Tab;
            Order = dropdown.Order;
            Slider = null;
            Dropdown = dropdown;
            LineEdit = null;
            Type = CustomOptionType.Dropdown;
        }

        public CustomOptionDescriptor(OptionsLineEditDefinition lineEdit)
        {
            Id = lineEdit.Id;
            Tab = lineEdit.Tab;
            Order = lineEdit.Order;
            Slider = null;
            Dropdown = null;
            LineEdit = lineEdit;
            Type = CustomOptionType.LineEdit;
        }

        public int Id { get; }
        public OptionsTab Tab { get; }
        public int Order { get; }
        public OptionsSliderDefinition Slider { get; }
        public OptionsDropdownDefinition Dropdown { get; }
        public OptionsLineEditDefinition LineEdit { get; }
        public CustomOptionType Type { get; }
    }
}
