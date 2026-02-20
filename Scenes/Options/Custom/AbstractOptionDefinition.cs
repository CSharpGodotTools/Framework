using System.Text;

namespace Framework.UI;

/// <summary>
/// Base type for a custom option that can be shown in the Options UI.
/// Game projects create small classes that inherit from this.
/// </summary>
public abstract class AbstractOptionDefinition
{
    /// <summary>
    /// Which tab this option appears under (Gameplay, Audio, etc).
    /// </summary>
    public abstract OptionsTab Tab { get; }

    /// <summary>
    /// Display text key used in the options UI (for example: "MOUSE_SENSITIVITY").
    /// </summary>
    public abstract string Label { get; }

    /// <summary>
    /// Optional ordering inside the tab. Lower numbers are shown first.
    /// </summary>
    public virtual int Order => 0;

    /// <summary>
    /// Converts a label-like string into a PascalCase save key.
    /// Examples:
    /// "MOUSE_SENSITIVITY" => "MouseSensitivity"
    /// "player name" => "PlayerName"
    /// </summary>
    protected static string ToPascalCaseKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        bool hasSeparator = false;
        foreach (char character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                hasSeparator = true;
                break;
            }
        }

        if (!hasSeparator)
        {
            bool allUpper = true;
            foreach (char character in value)
            {
                if (char.IsLetter(character) && !char.IsUpper(character))
                {
                    allUpper = false;
                    break;
                }
            }

            if (allUpper)
            {
                string lowered = value.ToLowerInvariant();
                return char.ToUpperInvariant(lowered[0]) + lowered[1..];
            }

            return char.ToUpperInvariant(value[0]) + value[1..];
        }

        StringBuilder result = new();
        bool capitalizeNext = true;

        foreach (char character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                capitalizeNext = true;
                continue;
            }

            if (capitalizeNext)
            {
                result.Append(char.ToUpperInvariant(character));
                capitalizeNext = false;
            }
            else
            {
                result.Append(char.ToLowerInvariant(character));
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Backward-compatible alias for older code.
    /// </summary>
    protected static string NormalizePascalCaseKey(string value) => ToPascalCaseKey(value);
}
