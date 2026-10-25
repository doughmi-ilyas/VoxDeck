using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Win32;
using VoxDeck.App.Features.Player;
using VoxDeck.App.Features.Toasts;
using VoxDeck.App.Hotkeys;
using VoxDeck.Core.Audio;
using VoxDeck.Core.Library;

namespace VoxDeck.App.Features.Library;

public sealed class FilterChip(string label) : ViewModel
{
    public string Label => label;
    public int Count { get; set => Set(ref field, value); }
}

public sealed class LibraryViewModel : ViewModel, IPage
{
    const int TileBars = 32;

    readonly PlayerViewModel _player;
    readonly ToastsViewModel _toasts;
    readonly ListCollectionView _view;
    // Slider drags edit settings many times a second; write the file once they settle.
    readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    bool _computingPeaks;

    public LibraryViewModel(PlayerViewModel player, ToastsViewModel toasts)
    {
        _player = player;
        _toasts = toasts;
        Filters = [new FilterChip("All"), new FilterChip("Favorites"), new FilterChip("Recent")];
        Filter = Filters[0];
        Items = new(SoundLibrary.Load(SoundLibrary.DefaultPath).Select(Create));
        _view = (ListCollectionView)CollectionViewSource.GetDefaultView(Items);
        _view.Filter = Matches;
        Items.CollectionChanged += (_, _) => UpdateCounts();
        _saveTimer.Tick += (_, _) => SaveNow();
        player.Started += item => Edit(item, item.Model with
        {
            LastPlayed = DateTime.Now,
            DurationSeconds = item.Playback?.Duration.TotalSeconds ?? item.Model.DurationSeconds,
        });

        Play = new Command(p => _player.Play((SoundItem)p!));
        ToggleFavorite = new Command(p => OnToggleFavorite((SoundItem)p!));
        EditSound = new Command(p => EditRequested?.Invoke((SoundItem)p!));
        Remove = new Command(p => RemoveItem((SoundItem)p!));
        ImportFiles = new Command(() => Import(PickFiles()));
        ClearSearch = new Command(() => Search = "");
        UpdateCounts();
        ComputeMissingPeaks();
    }

    public event Action<SoundItem>? EditRequested;

    public string Title => "Library";
    public string Icon => "";
    public string Badge => Items.Count.ToString();

    public HotkeyManager? Hotkeys { get; set; }
    public ObservableCollection<SoundItem> Items { get; }
    public FilterChip[] Filters { get; }

    public FilterChip Filter
    {
        get;
        set
        {
            if (Set(ref field, value)) Refresh();
        }
    }

    public string Search
    {
        get;
        set
        {
            if (Set(ref field, value)) Refresh();
        }
    } = "";

    public string Subtitle
    {
        get
        {
            var parts = new List<string> { Items.Count == 1 ? "1 sound" : $"{Items.Count} sounds" };
            if (Filters[1].Count > 0) parts.Add($"{Filters[1].Count} favorite{(Filters[1].Count == 1 ? "" : "s")}");
            var withKeys = Items.Count(i => i.Model.Hotkey is not null);
            if (withKeys > 0) parts.Add($"{withKeys} with hotkeys");
            return string.Join("  ·  ", parts);
        }
    }

    public bool IsEmpty => Items.Count == 0;
    public bool NoResults => Items.Count > 0 && _view.IsEmpty;

    public Command Play { get; }
    public Command ToggleFavorite { get; }
    public Command EditSound { get; }
    public Command Remove { get; }
    public Command ImportFiles { get; }
    public Command ClearSearch { get; }

    public static string[] PickFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add sounds",
            Multiselect = true,
            Filter = $"Audio files|{string.Join(";", SoundLibrary.Extensions.Select(x => "*" + x))}",
        };
        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public void Import(IReadOnlyCollection<string> paths)
    {
        if (paths.Count == 0) return;
        var added = 0;
        var skipped = 0;
        foreach (var path in paths)
        {
            if (!SoundLibrary.IsSupported(path)) skipped++;
            else if (!Items.Any(i => string.Equals(i.Model.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                Items.Add(Create(new Sound(Path.GetFileNameWithoutExtension(path), path)));
                added++;
            }
        }
        SaveNow();
        if (added > 0) _toasts.Success(added == 1 ? "Added 1 sound." : $"Added {added} sounds.");
        else if (skipped == 0) _toasts.Info("Those sounds are already in your library.");
        if (skipped > 0)
            _toasts.Error($"Skipped {skipped} file{(skipped == 1 ? "" : "s")}. VoxDeck plays {string.Join(", ", SoundLibrary.Extensions)}.");
        ComputeMissingPeaks();
    }

    public void Edit(SoundItem item, Sound model)
    {
        item.Model = model;
        _saveTimer.Stop();
        _saveTimer.Start();
        UpdateCounts();
    }

    public void SaveNow()
    {
        _saveTimer.Stop();
        SoundLibrary.Save(SoundLibrary.DefaultPath, Items.Select(i => i.Model));
    }

    SoundItem Create(Sound sound) => new(sound, (item, fraction) => _player.Seek(item, fraction));

    bool Matches(object o)
    {
        var sound = ((SoundItem)o).Model;
        if (Filter == Filters[1] && !sound.Favorite) return false;
        if (Filter == Filters[2] && sound.LastPlayed is null) return false;
        return Search.Length == 0 || sound.Title.Contains(Search, StringComparison.OrdinalIgnoreCase);
    }

    void Refresh()
    {
        if (_view is null) return; // the first Filter assignment happens before the view exists
        using (_view.DeferRefresh())
        {
            _view.SortDescriptions.Clear();
            if (Filter == Filters[2]) _view.SortDescriptions.Add(new SortDescription("Model.LastPlayed", ListSortDirection.Descending));
        }
        Raise(nameof(NoResults));
    }

    void UpdateCounts()
    {
        Filters[0].Count = Items.Count;
        Filters[1].Count = Items.Count(i => i.Model.Favorite);
        Filters[2].Count = Items.Count(i => i.Model.LastPlayed is not null);
        Raise(nameof(Subtitle));
        Raise(nameof(Badge));
        Raise(nameof(IsEmpty));
        Raise(nameof(NoResults));
    }

    void OnToggleFavorite(SoundItem item)
    {
        Edit(item, item.Model with { Favorite = !item.Model.Favorite });
        if (Filter == Filters[1]) Refresh();
    }

    void RemoveItem(SoundItem item)
    {
        var index = Items.IndexOf(item);
        Items.Remove(item);
        SaveNow();
        Hotkeys?.Refresh();
        _toasts.Show(new Toast($"Removed “{item.Model.Title}”. The file is still on your PC.", "", "info", "Undo", () =>
        {
            Items.Insert(Math.Min(index, Items.Count), item);
            SaveNow();
            Hotkeys?.Refresh();
        }));
    }

    // Tile waveforms are computed once in the background and saved with the library.
    async void ComputeMissingPeaks()
    {
        if (_computingPeaks) return;
        _computingPeaks = true;
        try
        {
            while (Items.FirstOrDefault(i => i.Model.Peaks is null && !i.PeaksFailed) is { } item)
            {
                try
                {
                    var (peaks, duration) = await Task.Run(() => Waveform.Load(item.Model.Path, TileBars));
                    Edit(item, item.Model with
                    {
                        Peaks = [.. peaks.Select(p => (byte)Math.Round(p * 255))],
                        DurationSeconds = duration.TotalSeconds,
                    });
                }
                catch (Exception)
                {
                    item.PeaksFailed = true;
                    item.IsMissing = !File.Exists(item.Model.Path);
                }
            }
        }
        finally
        {
            _computingPeaks = false;
        }
    }
}
