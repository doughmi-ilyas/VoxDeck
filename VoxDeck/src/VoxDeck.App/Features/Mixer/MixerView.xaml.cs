using System.Windows;
using System.Windows.Controls;

namespace VoxDeck.App.Features.Mixer;

public partial class MixerView : UserControl
{
    // Held because DataContext may already be gone by the time Unloaded fires.
    MixerViewModel? _vm;

    public MixerView() => InitializeComponent();

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = (MixerViewModel)DataContext;
        _vm.SetPageVisible(true);
    }

    void OnUnloaded(object sender, RoutedEventArgs e) => _vm?.SetPageVisible(false);
}
