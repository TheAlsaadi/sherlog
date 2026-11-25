using System.Globalization;

namespace Sherlog.Core.Evidence;

/// <summary>
/// One recorded crash, parsed from a raw Windows record.
/// Codes are normalized to lowercase "0x" + 8 hex digits; names are file names without paths; times are UTC.
/// </summary>
public sealed record CrashEvent(
    string Id,
    DateTimeOffset Time,
    string Source,
    string? Provider,
    int? EventId,
    string? AppName,
    string? AppVersion,
    string? FaultingModule,
    string? ModuleVersion,
    string? ExceptionCode,
    string? BugCheckCode,
    string? EventName,
    string? Message)
{
    /// <summary>
    /// Normalizes an exception or bug-check code to "0x" + 8 lowercase hex digits.
    /// Returns null when the value is missing or not a number.
    /// </summary>
    public static string? NormalizeCode(string? raw, bool fromDecimal = false)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        uint value;
        if (fromDecimal)
        {
            if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value))
            {
                return null;
            }
        }
        else
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                text = text[2..];
            }

            if (!uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value))
            {
                return null;
            }
        }

        return "0x" + value.ToString("x8", CultureInfo.InvariantCulture);
    }
}
