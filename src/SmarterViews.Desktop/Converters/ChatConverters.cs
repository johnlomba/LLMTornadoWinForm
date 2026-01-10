using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SmarterViews.Desktop.Models.Chat;

namespace SmarterViews.Desktop.Converters;

/// <summary>
/// Converts message role to background color for chat bubbles.
/// </summary>
public class RoleToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MessageRole role)
        {
            return role switch
            {
                MessageRole.User => new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
                MessageRole.Assistant => new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E)),
                MessageRole.System => new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)),
                _ => new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts message role to horizontal alignment.
/// </summary>
public class RoleToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MessageRole role)
        {
            return role == MessageRole.User 
                ? System.Windows.HorizontalAlignment.Right 
                : System.Windows.HorizontalAlignment.Left;
        }
        return System.Windows.HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts count to visibility.
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int count = 0;
        if (value is int intValue)
        {
            count = intValue;
        }
        
        bool invert = parameter?.ToString() == "Invert";
        bool hasItems = count > 0;
        
        if (invert)
        {
            return hasItems ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }
        return hasItems ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts string array to/from newline-separated string.
/// </summary>
public class ArrayToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string[] array)
        {
            return string.Join(Environment.NewLine, array);
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            return str.Split(new[] { Environment.NewLine, "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }
        return Array.Empty<string>();
    }
}
