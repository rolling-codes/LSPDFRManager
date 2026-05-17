using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LSPDFRManager.Converters;

/// <summary>
/// Two-way converter for binding RadioButton.IsChecked to an int index.
/// ConverterParameter is the index this button represents (as a string).
/// Convert: returns true when value == parameter.
/// ConvertBack: returns the parameter int when IsChecked is true; UnsetValue otherwise.
/// </summary>
public class IntEqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int i && parameter is string s && int.TryParse(s, out var p) && i == p;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string s && int.TryParse(s, out var p))
            return p;
        return DependencyProperty.UnsetValue;
    }
}
