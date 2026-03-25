using System.Globalization;
using System.Windows.Data;

namespace IndexTTSStudio.Converters;

public class GenerateButtonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isGenerating && isGenerating)
            return "Generating...";
        return "Generate";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
