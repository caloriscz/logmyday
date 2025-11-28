using System.Globalization;

namespace LogMyDay.App.Mobile.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            if (parameter?.ToString() == "text")
            {
                // Text color: dark for enabled (good contrast), light gray for disabled
                return boolValue ? Colors.DarkGreen : Colors.Gray;
            }

            // Background color: green for enabled, light gray for disabled
            return boolValue ? Colors.MediumSeaGreen : Colors.LightGray;
        }

        return Colors.LightGray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StringToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value?.ToString());
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
