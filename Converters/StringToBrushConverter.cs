using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LSPDFRManager.Converters;

[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { /* fall through */ }
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
