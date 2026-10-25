using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using VoxDeck.App.Features.Player;
using VoxDeck.Core.Library;

namespace VoxDeck.App.Features.Library;

public sealed class LibraryViewModel : ViewModel
{
    readonly PlayerViewModel _player;
    readonly ICollectionView _view;
    // Slider drags edit settings many times a second; write the file once they settle.
    readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public LibraryViewModel(PlayerViewModel player)
    {
        _player = player;
        Items = new(SoundLibrary.Load(SoundLibrary.DefaultPath).Select(s => new SoundItem(s)));
        _view = CollectionViewSource.GetDefaultView(Items);
        _view.Filter = o => Search.Length == 0 || ((SoundItem)o).Model.Title.Contains(Search, StringComparison.OrdinalIgnoreCase);
        Items.CollectionChanged += (_, _) => Raise(nameof(CountText));
        _saveTimer.Tick += (_, _) => SaveNow();
        player.SoundEdited += () =>
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        };
        Play = new Command(p => _player.Play((SoundItem)p!));
        Remove = new Command(p => RemoveItem((SoundItem)p!));
    }

    public string Title => "Library";
    public string Icon => "";
    public ObservableCollection<SoundItem> Items { get; }
    public string CountText => Items.Count == 1 ? "1 sound" : $"{Items.Count} sounds";

    public string Search
    {
        get;
        set
        {
            if (Set(ref field, value)) _view.Refresh();
        }
    } = "";

    public string? Error { get; private set => Set(ref field, value); }

    public Command Play { get; }
    public Command Remove { get; }

    public void Import(IEnumerable<string> paths)
    {
        Error = null;
        var skipped = 0;
        foreach (var path in paths)
        {
            if (!SoundLibrary.IsSupported(path)) skipped++;
            else if (!Items.Any(i => string.Equals(i.Model.Path, path, StringComparison.OrdinalIgnoreCase)))
                Items.Add(new SoundItem(new Sound(Path.GetFileNameWithoutExtension(path), path)));
        }
        SaveNow();
        if (skipped > 0)
            Error = $"Skipped {skipped} unsupported file(s). Supported: {string.Join(", ", SoundLibrary.Extensions)}";
    }

    public void SaveNow()
    {
        _saveTimer.Stop();
        SoundLibrary.Save(SoundLibrary.DefaultPath, Items.Select(i => i.Model));
    }

    void RemoveItem(SoundItem item)
    {
        Items.Remove(item);
        _player.Forget(item);
        SaveNow();
    }
}
