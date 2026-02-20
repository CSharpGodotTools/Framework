using System.Collections.Generic;

namespace Framework.UI;

/// <summary>
/// Class-based definition for a custom dropdown option.
/// Implement this in game code, then register with:
/// GameFramework.Options.AddDropdown(new YourDropdownOption()).
/// </summary>
public abstract class DropdownOptionDefinition : AbstractOptionDefinition
{
    /// <summary>
    /// Display items for the dropdown, in index order.
    /// </summary>
    public abstract IReadOnlyList<string> Items { get; }

    /// <summary>
    /// Default selected index used when first created.
    /// </summary>
    public virtual int DefaultValue => 0;

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
    /// Reads the current selected item index from your game settings source.
    /// </summary>
    public abstract int GetValue();

    /// <summary>
    /// Writes the selected item index back to your game settings source.
    /// </summary>
    public abstract void SetValue(int value);
}
