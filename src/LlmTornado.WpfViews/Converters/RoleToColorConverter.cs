using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LlmTornado.WpfViews.Models;

namespace LlmTornado.WpfViews.Converters;

/// <summary>
/// Converts message role to background color.
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
/// Converts boolean or nullable object to visibility.
/// If the value is a bool, returns Visible if true, Collapsed if false.
/// If the value is null, returns Collapsed; otherwise Visible.
/// ConverterParameter can be set to "Invert" to reverse the behavior.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isVisible;
        
        if (value is bool boolValue)
        {
            isVisible = boolValue;
        }
        else
        {
            // For nullable objects, treat non-null as visible
            isVisible = value != null;
        }
        
        // Check for invert parameter
        if (parameter is string paramStr && paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            isVisible = !isVisible;
        }
        
        return isVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is System.Windows.Visibility visibility)
        {
            bool result = visibility == System.Windows.Visibility.Visible;
            
            if (parameter is string paramStr && paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                result = !result;
            }
            
            return result;
        }
        
        return false;
    }
}

/// <summary>
/// Converts message role to alignment.
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
/// Inverts a boolean value. Also handles nullable booleans, treating null as false.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        
        // Treat null as false, inverted = true
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

/// <summary>
/// Converts null/non-null values to boolean.
/// Returns false if value is null, true if not null.
/// Use ConverterParameter "Invert" to reverse this behavior.
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool result = value != null;
        
        if (parameter is string paramStr && paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            result = !result;
        }
        
        return result;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("NullToBoolConverter does not support ConvertBack.");
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

