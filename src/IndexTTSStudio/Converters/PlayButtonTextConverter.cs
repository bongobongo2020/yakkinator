using System.Globalization;
using System.Windows.Data;

namespace IndexTTSStudio.Converters;

public class PlayButtonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPlaying && isPlaying)
            return "Stop";
        return "Play";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
