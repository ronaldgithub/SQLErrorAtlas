using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SQLErrorAtlas;

public static partial class Converters
{
    /// <summary>Urgency string -> themed brush (falls back to the "Info" brush).</summary>
    public static readonly IValueConverter UrgencyToBrush = new UrgencyToBrushConverter();

    /// <summary>
    /// Puts each inline step of a numbered list ("1. … 2. … 3. …") on its own line,
    /// and indents lettered sub-steps ("a. … b. …"). No-op when the text has no such markers.
    /// </summary>
    public static readonly IValueConverter NumberedListToLines = new NumberedListConverter();

    private sealed class UrgencyToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            var key = (value as string)?.Trim().ToLowerInvariant() switch
            {
                "emergency" => "UrgencyEmergencyBrush",
                "high" => "UrgencyHighBrush",
                "medium" => "UrgencyMediumBrush",
                "low" => "UrgencyLowBrush",
                _ => "UrgencyInfoBrush",
            };
            if (Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var res) == true
                && res is IBrush brush)
                return brush;
            return Brushes.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
            => throw new NotSupportedException();
    }

    private sealed partial class NumberedListConverter : IValueConverter
    {
        // whitespace that sits between the end of one step and the "N." of the next
        [GeneratedRegex(@"(?<=\S)[ \t]+(?=\d{1,2}\.\s)")]
        private static partial Regex BeforeNumberedItem();

        // " a. Capital…" sub-steps (guarded against "e.g." / "i.e." / "vs." by requiring a
        // preceding space and a following capitalised word)
        [GeneratedRegex(@"(?<=\S)[ \t]+(?=[a-h]\.\s+[A-Z])")]
        private static partial Regex BeforeLetterItem();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            if (value is not string s || s.Length == 0)
                return value;

            s = BeforeNumberedItem().Replace(s, "\n");
            s = BeforeLetterItem().Replace(s, "\n    ");
            return s;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
            => throw new NotSupportedException();
    }
}
