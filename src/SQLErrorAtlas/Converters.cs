using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SQLErrorAtlas;

public static class Converters
{
    /// <summary>Urgency string -> themed brush (falls back to the "Info" brush).</summary>
    public static readonly IValueConverter UrgencyToBrush = new UrgencyToBrushConverter();

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
}
