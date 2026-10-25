using System.Windows;
using System.Windows.Controls;

namespace VoxDeck.App.Features.AudioSetup;

public partial class AudioSetupView : UserControl
{
    // Held because DataContext may already be gone by the time Unloaded fires.
    AudioSetupViewModel? _vm;

    public AudioSetupView() => InitializeComponent();

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = (AudioSetupViewModel)DataContext;
        _vm.Start();
    }

    void OnUnloaded(object sender, RoutedEventArgs e) => _vm?.Stop();
}
