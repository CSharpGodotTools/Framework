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

/// <summary>
/// Class-based definition for a custom slider option.
/// Implement this in game code, then register with:
/// GameFramework.Options.AddSlider(new YourSliderOption()).
/// </summary>
public abstract class SliderOptionDefinition : AbstractOptionDefinition
{
    /// <summary>
    /// Minimum value allowed by the UI slider.
    /// </summary>
    public abstract double MinValue { get; }

    /// <summary>
    /// Maximum value allowed by the UI slider.
    /// </summary>
    public abstract double MaxValue { get; }

    /// <summary>
    /// Increment step used by the UI slider.
    /// </summary>
    public virtual double Step => 1.0;

    /// <summary>
    /// Default value used when the option is first created.
    /// </summary>
    public virtual float DefaultValue => 0.0f;

    /// <summary>
    /// True: value is auto-saved as an inline custom key in options.json.
    /// False: value is read/written only through GetValue/SetValue (typed property binding).
    /// </summary>
    public virtual bool UseCustomPersistence => true;

    /// <summary>
    /// Beginner-friendly alias for <see cref="UseCustomPersistence"/>.
    /// </summary>
    public virtual bool SaveInCustomValues => UseCustomPersistence;

    /// <summary>
    /// Key used in options.json when custom persistence is enabled.
    /// Default is a PascalCase version of Label.
    /// </summary>
    public virtual string PersistenceKey => ToPascalCaseKey(Label);

    /// <summary>
    /// Beginner-friendly alias for <see cref="PersistenceKey"/>.
    /// </summary>
    public virtual string SaveKey => PersistenceKey;

    /// <summary>
    /// Reads the current value from your game settings source.
    /// </summary>
    public abstract float GetValue();

    /// <summary>
    /// Writes the value back to your game settings source.
    /// </summary>
    public abstract void SetValue(float value);
}
