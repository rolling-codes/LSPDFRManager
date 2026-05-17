using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LSPDFRManager.Converters;

public class AnyBoolToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.OfType<bool>().Any(value => value)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
