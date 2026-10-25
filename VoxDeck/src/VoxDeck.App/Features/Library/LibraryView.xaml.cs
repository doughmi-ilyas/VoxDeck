using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;

namespace VoxDeck.App.Features.Library;

public partial class LibraryView : UserControl
{
    const double TileMinWidth = 176;
    const double TileGap = 12;

    UniformGrid? _grid;

    public LibraryView() => InitializeComponent();

    void OnGridLoaded(object sender, RoutedEventArgs e)
    {
        _grid = (UniformGrid)sender;
        UpdateColumns();
    }

    void OnScrollerSizeChanged(object sender, SizeChangedEventArgs e) => UpdateColumns();

    // CSS-style auto-fill: as many columns as fit at the minimum tile width, tiles stretch to fill the row.
    void UpdateColumns()
    {
        if (_grid is null) return;
        _grid.Columns = Math.Max(1, (int)((Scroller.ActualWidth - 10) / (TileMinWidth + TileGap)));
    }
}
