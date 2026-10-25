using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VoxDeck.App.Controls;

/// <summary>Visible for true, non-empty text, non-zero numbers and other non-null values. ConverterParameter "not" inverts.</summary>
public sealed class VisibleWhen : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            int i => i != 0,
            _ => true,
        };
        if (parameter is "not") visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
