using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace VoxDeck.App.Controls;

/// <summary>Focuses an element when it appears (and selects a text box's text), e.g. the name field of a dialog.</summary>
public static class AutoFocus
{
    public static readonly DependencyProperty OnLoadProperty =
        DependencyProperty.RegisterAttached("OnLoad", typeof(bool), typeof(AutoFocus), new PropertyMetadata(false, OnChanged));

    public static bool GetOnLoad(DependencyObject d) => (bool)d.GetValue(OnLoadProperty);
    public static void SetOnLoad(DependencyObject d, bool value) => d.SetValue(OnLoadProperty, value);

    static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && e.NewValue is true)
            element.Loaded += (_, _) => element.Dispatcher.InvokeAsync(() =>
            {
                element.Focus();
                (element as TextBox)?.SelectAll();
            }, DispatcherPriority.Input);
    }
}
