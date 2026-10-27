using System.Windows;
using System.Windows.Controls;

namespace VoxDeck.App.Features.Player;

public partial class PlayerBar : UserControl
{
    public PlayerBar() => InitializeComponent();

    // Narrow windows: drop the inline volume slider first (the ⋯ panel still has it), then the route's label (its icon stays).
    void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        VolumeSlider.Visibility = ActualWidth < 1020 ? Visibility.Collapsed : Visibility.Visible;
        RouteLabel.Visibility = ActualWidth < 880 ? Visibility.Collapsed : Visibility.Visible;
    }

    // Picking who hears your sounds closes the picker. The first selection, made when the list appears, has nothing removed.
    void OnRouteChosen(object sender, SelectionChangedEventArgs e)
    {
        if (e.RemovedItems.Count > 0) RouteButton.IsChecked = false;
    }
}
