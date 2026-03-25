using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IndexTTSStudio.Converters;

public class EmotionModeVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string mode && parameter is string targetMode)
            return mode == targetMode ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
