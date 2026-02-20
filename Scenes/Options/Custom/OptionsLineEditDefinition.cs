namespace Framework.UI;

/// <summary>
/// Class-based definition for a custom line edit option.
/// Implement this in game code, then register with:
/// GameFramework.Options.AddLineEdit(new YourLineEditOption()).
/// </summary>
public abstract class LineEditOptionDefinition : AbstractOptionDefinition
{
    /// <summary>
    /// Placeholder text shown in the input field when empty.
    /// </summary>
    public virtual string Placeholder => string.Empty;

    /// <summary>
    /// Default text used when first created.
    /// </summary>
    public virtual string DefaultValue => string.Empty;

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
    /// Reads the current text from your game settings source.
    /// </summary>
    public abstract string GetValue();

    /// <summary>
    /// Writes text back to your game settings source.
    /// </summary>
    public abstract void SetValue(string value);
}
