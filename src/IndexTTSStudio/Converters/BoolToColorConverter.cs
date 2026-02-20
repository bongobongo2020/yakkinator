using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace IndexTTSStudio.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue)
            return new SolidColorBrush(Colors.LimeGreen);
        return new SolidColorBrush(Colors.Red);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
