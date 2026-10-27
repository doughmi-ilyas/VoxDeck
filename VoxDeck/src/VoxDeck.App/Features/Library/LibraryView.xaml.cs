using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VoxDeck.App.Controls;

namespace VoxDeck.App.Features.Library;

public partial class LibraryView : UserControl
{
    const double TileMinWidth = 176;
    const double TileGap = 12;

    UniformGrid? _grid;
    // Drag and drop: the tile pressed, where, and the tile currently under a dragged sound.
    SoundItem? _pressed;
    Point _pressedAt;
    SoundItem? _tileTarget;

    public LibraryView() => InitializeComponent();

    LibraryViewModel? Vm => DataContext as LibraryViewModel;

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

    // ---- Dragging a tile ----

    void OnTilesMouseDown(object sender, MouseButtonEventArgs e)
    {
        // The waveform is for seeking, so drags start anywhere else on the tile.
        var source = e.OriginalSource as DependencyObject;
        _pressed = Ancestor<WaveformView>(source) is null ? (source as FrameworkElement)?.DataContext as SoundItem : null;
        _pressedAt = e.GetPosition(this);
    }

    void OnTilesMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressed is not { } item || e.LeftButton != MouseButtonState.Pressed || Vm is not { } vm) return;
        var moved = e.GetPosition(this) - _pressedAt;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance * 2 && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance * 2) return;
        _pressed = null;
        Mouse.Capture(null); // the tile (a button) holds the mouse; hand it to the drag, so no click fires
        vm.IsDragging = true;
        try
        {
            DragDrop.DoDragDrop(Tiles, new DataObject(typeof(SoundItem), item), DragDropEffects.Copy | DragDropEffects.Move);
        }
        finally
        {
            vm.IsDragging = false;
            SetTileTarget(null);
            foreach (var chip in vm.Filters) chip.IsDropTarget = false;
            NewPlaylistButton.Tag = null;
        }
    }

    static SoundItem? Dragged(DragEventArgs e) => e.Data.GetData(typeof(SoundItem)) as SoundItem;

    // ---- Dropping on a tile: reorder inside a playlist ----

    void OnTilesDragOver(object sender, DragEventArgs e)
    {
        var target = (e.OriginalSource as FrameworkElement)?.DataContext as SoundItem;
        var ok = Vm is { IsPlaylistView: true } && Dragged(e) is { } dragged && target is not null && target != dragged;
        e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
        SetTileTarget(ok ? target : null);
        e.Handled = true;
    }

    void OnTilesDragLeave(object sender, DragEventArgs e) => SetTileTarget(null);

    void OnTilesDrop(object sender, DragEventArgs e)
    {
        if (_tileTarget is { } target && Dragged(e) is { } dragged) Vm?.DropBefore(dragged, target);
        SetTileTarget(null);
        e.Handled = true;
    }

    void SetTileTarget(SoundItem? target)
    {
        if (_tileTarget == target) return;
        if (_tileTarget is not null) _tileTarget.IsDropTarget = false;
        _tileTarget = target;
        if (target is not null) target.IsDropTarget = true;
    }

    // ---- Dropping on a playlist chip: add to it ----

    void OnChipDragOver(object sender, DragEventArgs e)
    {
        var chip = (FilterChip)((FrameworkElement)sender).DataContext;
        var ok = chip.IsPlaylist && Dragged(e) is not null;
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        chip.IsDropTarget = ok;
        e.Handled = true;
    }

    void OnChipDragLeave(object sender, DragEventArgs e) => ((FilterChip)((FrameworkElement)sender).DataContext).IsDropTarget = false;

    void OnChipDrop(object sender, DragEventArgs e)
    {
        var chip = (FilterChip)((FrameworkElement)sender).DataContext;
        chip.IsDropTarget = false;
        if (Dragged(e) is { } item) Vm?.DropOnPlaylist(chip, item);
        e.Handled = true;
    }

    // ---- Dropping on "New playlist": start one with this sound ----

    void OnNewPlaylistDragOver(object sender, DragEventArgs e)
    {
        var ok = Dragged(e) is not null;
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        NewPlaylistButton.Tag = ok ? "drop" : null;
        e.Handled = true;
    }

    void OnNewPlaylistDragLeave(object sender, DragEventArgs e) => NewPlaylistButton.Tag = null;

    void OnNewPlaylistDrop(object sender, DragEventArgs e)
    {
        NewPlaylistButton.Tag = null;
        if (Dragged(e) is { } item) Vm?.DropOnNewPlaylist(item);
        e.Handled = true;
    }

    static T? Ancestor<T>(DependencyObject? d) where T : DependencyObject
    {
        for (; d is not null; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is T found) return found;
        return null;
    }
}
