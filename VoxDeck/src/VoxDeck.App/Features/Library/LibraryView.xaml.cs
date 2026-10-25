using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VoxDeck.Core.Library;

namespace VoxDeck.App.Features.Library;

public partial class LibraryView : UserControl
{
    public LibraryView() => InitializeComponent();

    LibraryViewModel Vm => (LibraryViewModel)DataContext;

    void OnImport(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import sounds",
            Multiselect = true,
            Filter = $"Audio files|{string.Join(";", SoundLibrary.Extensions.Select(x => "*" + x))}",
        };
        if (dialog.ShowDialog() == true) Vm.Import(dialog.FileNames);
    }

    void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files) Vm.Import(files);
    }
}
