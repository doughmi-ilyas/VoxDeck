using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VoxDeck.App.Controls;

namespace VoxDeck.App.Features.Palette;

public partial class PaletteView : UserControl
{
    public PaletteView() => InitializeComponent();

    PaletteViewModel Vm => (PaletteViewModel)DataContext;

    void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;
        Motion.Pop(Box);
        Dispatcher.BeginInvoke(() => Input.Focus(), DispatcherPriority.Input);
    }

    void OnInputKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                Vm.Move(1);
                break;
            case Key.Up:
                Vm.Move(-1);
                break;
            case Key.Enter:
                Vm.RunSelected();
                break;
            default:
                return;
        }
        if (Vm.Selected is not null) List.ScrollIntoView(Vm.Selected);
        e.Handled = true;
    }

    void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is not ListBoxItem) continue;
            Vm.RunSelected();
            return;
        }
    }

    void OnBackdropClick(object sender, MouseButtonEventArgs e) => Vm.Close();
}
